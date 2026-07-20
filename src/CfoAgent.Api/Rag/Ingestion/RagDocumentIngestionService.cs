using System.Security.Cryptography;
using System.Text;
using CfoAgent.Api.Configuration;
using CfoAgent.Api.Rag.Chroma;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace CfoAgent.Api.Rag.Ingestion;

public sealed class RagDocumentIngestionService(
    ChromaClient chromaClient,
    IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
    IOptions<RagOptions> options)
{
    private readonly RagOptions _options = options.Value;

    public async Task<RagIngestionResult> IngestAsync(CancellationToken cancellationToken = default)
    {
        var root = Path.GetFullPath(_options.KnowledgeFilesRoot);
        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException($"Knowledge document directory was not found: {root}");
        }

        var files = Directory.EnumerateFiles(root, "*.md", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        var collection = await chromaClient.GetOrCreateCollectionAsync(cancellationToken: cancellationToken);
        var chunksAddedOrUpdated = 0;
        var skipped = 0;
        var failures = new List<RagIngestionFailure>();

        foreach (var file in files)
        {
            try
            {
                var markdown = await File.ReadAllTextAsync(file, cancellationToken);
                var document = ParseDocument(markdown, file);
                var overlapSize = _options.GetChunkOverlapSize();
                var chunks = BuildChunks(document, _options.MaxChunkCharacters, overlapSize);

                if (chunks.Count == 0)
                {
                    await chromaClient.DeleteBySourcePathAsync(collection, document.SourcePath, cancellationToken);
                    skipped++;
                    continue;
                }

                var records = await CreateRecordsAsync(document, chunks, overlapSize, cancellationToken);
                await chromaClient.DeleteBySourcePathAsync(collection, document.SourcePath, cancellationToken);
                await chromaClient.UpsertAsync(collection, records, cancellationToken);
                chunksAddedOrUpdated += records.Count;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                failures.Add(new RagIngestionFailure(file, exception.Message));
            }
        }

        return new RagIngestionResult(files.Length, chunksAddedOrUpdated, skipped, failures.Count, failures);
    }

    private async Task<IReadOnlyCollection<ChromaRecord>> CreateRecordsAsync(
        ParsedDocument document,
        IReadOnlyList<DocumentChunk> chunks,
        int overlapSize,
        CancellationToken cancellationToken)
    {
        var records = new List<ChromaRecord>(chunks.Count);
        var recordIds = new HashSet<string>(StringComparer.Ordinal);
        var normalizedContent = new HashSet<string>(StringComparer.Ordinal);

        foreach (var chunk in chunks)
        {
            if (!normalizedContent.Add(NormalizeContent(chunk.Content)))
            {
                continue;
            }

            var generated = await embeddingGenerator.GenerateAsync([chunk.Content], cancellationToken: cancellationToken);
            var embedding = generated.Single().Vector.ToArray();
            var id = CreateChunkId(document.SourcePath, chunk.Section, chunk.Start, chunk.End, chunk.Content);
            if (!recordIds.Add(id))
            {
                continue;
            }

            var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["document_id"] = document.DocumentId,
                ["document_name"] = document.DocumentName,
                ["document_type"] = document.DocumentType,
                ["period"] = document.Period,
                ["section"] = chunk.Section,
                ["source_path"] = document.SourcePath,
                ["chunk_index"] = chunk.Index.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["chunk_start"] = chunk.Start.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["chunk_end"] = chunk.End.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["chunk_overlap_percentage"] = _options.ChunkOverlapPercentage.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["chunk_overlap_size"] = overlapSize.ToString(System.Globalization.CultureInfo.InvariantCulture)
            };

            records.Add(new ChromaRecord(id, chunk.Content, embedding, metadata));
        }

        return records;
    }

    private static ParsedDocument ParseDocument(string markdown, string filePath)
    {
        var normalized = markdown.Replace("\r\n", "\n", StringComparison.Ordinal);
        if (!normalized.StartsWith("---\n", StringComparison.Ordinal))
        {
            throw new InvalidDataException("Markdown front matter is required.");
        }

        var closingDelimiter = normalized.IndexOf("\n---\n", StringComparison.Ordinal);
        if (closingDelimiter < 0)
        {
            throw new InvalidDataException("Markdown front matter closing delimiter is required.");
        }

        var values = normalized[4..closingDelimiter]
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.Split(':', 2, StringSplitOptions.TrimEntries))
            .Where(parts => parts.Length == 2)
            .ToDictionary(parts => parts[0], parts => parts[1], StringComparer.OrdinalIgnoreCase);

        string GetRequired(string name) => values.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new InvalidDataException($"Front matter field '{name}' is required.");

        return new ParsedDocument(
            GetRequired("document_id"),
            GetRequired("document_name"),
            GetRequired("document_type"),
            GetRequired("period"),
            GetRequired("section"),
            GetRequired("source_path"),
            normalized[(closingDelimiter + 5)..]);
    }

    private static IReadOnlyList<DocumentChunk> BuildChunks(
        ParsedDocument document,
        int maxChunkCharacters,
        int overlapSize)
    {
        if (maxChunkCharacters <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxChunkCharacters), "Chunk size must be greater than zero.");
        }

        if (overlapSize < 0 || overlapSize >= maxChunkCharacters)
        {
            throw new ArgumentOutOfRangeException(nameof(overlapSize), "Chunk overlap must be at least zero and smaller than the chunk size.");
        }

        var chunks = new List<DocumentChunk>();
        var section = document.DefaultSection;
        var sectionContent = new StringBuilder();

        void FlushSection()
        {
            var text = sectionContent.ToString().Trim();
            sectionContent.Clear();
            if (text.Length == 0)
            {
                return;
            }

            var stepSize = maxChunkCharacters - overlapSize;
            var start = 0;
            while (start < text.Length)
            {
                var end = Math.Min(start + maxChunkCharacters, text.Length);
                var content = text[start..end];
                if (!string.IsNullOrWhiteSpace(content))
                {
                    chunks.Add(new DocumentChunk(chunks.Count, section, start, end, content));
                }

                if (end == text.Length)
                {
                    break;
                }

                start += stepSize;
            }
        }

        foreach (var line in document.Body.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith('#'))
            {
                FlushSection();
                section = trimmed.TrimStart('#').Trim();
                continue;
            }

            sectionContent.AppendLine(line);
        }

        FlushSection();
        return chunks;
    }

    private static string CreateChunkId(string sourcePath, string section, int start, int end, string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{sourcePath}\n{section}\n{start}\n{end}\n{content}"));
        return $"chunk-{Convert.ToHexString(bytes).ToLowerInvariant()}";
    }

    private static string NormalizeContent(string content) =>
        string.Join(' ', content.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private sealed record ParsedDocument(
        string DocumentId,
        string DocumentName,
        string DocumentType,
        string Period,
        string DefaultSection,
        string SourcePath,
        string Body);

    private sealed record DocumentChunk(int Index, string Section, int Start, int End, string Content);
}
