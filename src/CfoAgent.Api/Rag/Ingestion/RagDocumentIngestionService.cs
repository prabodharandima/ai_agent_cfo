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
                var chunks = BuildChunks(document, _options.MaxChunkCharacters);

                if (chunks.Count == 0)
                {
                    skipped++;
                    continue;
                }

                var records = await CreateRecordsAsync(document, chunks, cancellationToken);
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
        CancellationToken cancellationToken)
    {
        var records = new List<ChromaRecord>(chunks.Count);

        foreach (var chunk in chunks)
        {
            var generated = await embeddingGenerator.GenerateAsync([chunk.Content], cancellationToken: cancellationToken);
            var embedding = generated.Single().Vector.ToArray();
            var id = CreateChunkId(document.SourcePath, chunk.Section, chunk.Content);
            var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["document_id"] = document.DocumentId,
                ["document_name"] = document.DocumentName,
                ["document_type"] = document.DocumentType,
                ["period"] = document.Period,
                ["section"] = chunk.Section,
                ["source_path"] = document.SourcePath,
                ["chunk_index"] = chunk.Index.ToString(System.Globalization.CultureInfo.InvariantCulture)
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

    private static IReadOnlyList<DocumentChunk> BuildChunks(ParsedDocument document, int maxChunkCharacters)
    {
        var chunks = new List<DocumentChunk>();
        var section = document.DefaultSection;
        var paragraphs = new List<string>();

        void FlushSection()
        {
            if (paragraphs.Count == 0)
            {
                return;
            }

            var current = new StringBuilder();
            foreach (var paragraph in paragraphs.SelectMany(paragraph => SplitParagraph(paragraph, maxChunkCharacters)))
            {
                if (current.Length > 0 && current.Length + paragraph.Length + 2 > maxChunkCharacters)
                {
                    chunks.Add(new DocumentChunk(chunks.Count, section, $"{section}\n\n{current}"));
                    current.Clear();
                }

                if (current.Length > 0)
                {
                    current.AppendLine().AppendLine();
                }

                current.Append(paragraph);
            }

            if (current.Length > 0)
            {
                chunks.Add(new DocumentChunk(chunks.Count, section, $"{section}\n\n{current}"));
            }

            paragraphs.Clear();
        }

        foreach (var block in document.Body.Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var trimmed = block.Trim();
            if (trimmed.StartsWith('#'))
            {
                FlushSection();
                section = trimmed.TrimStart('#').Trim();
                continue;
            }

            paragraphs.Add(trimmed);
        }

        FlushSection();
        return chunks;
    }

    private static IEnumerable<string> SplitParagraph(string paragraph, int maxChunkCharacters)
    {
        if (paragraph.Length <= maxChunkCharacters)
        {
            yield return paragraph;
            yield break;
        }

        var remaining = paragraph;
        while (remaining.Length > maxChunkCharacters)
        {
            var boundary = remaining.LastIndexOf(". ", maxChunkCharacters - 1, StringComparison.Ordinal);
            if (boundary <= 0)
            {
                boundary = remaining.LastIndexOf(' ', maxChunkCharacters - 1);
            }

            if (boundary <= 0)
            {
                throw new InvalidDataException("A paragraph contains a token longer than the configured chunk size.");
            }

            yield return remaining[..(boundary + 1)].Trim();
            remaining = remaining[(boundary + 1)..].TrimStart();
        }

        if (remaining.Length > 0)
        {
            yield return remaining;
        }
    }

    private static string CreateChunkId(string sourcePath, string section, string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{sourcePath}\n{section}\n{content}"));
        return $"chunk-{Convert.ToHexString(bytes).ToLowerInvariant()}";
    }

    private sealed record ParsedDocument(
        string DocumentId,
        string DocumentName,
        string DocumentType,
        string Period,
        string DefaultSection,
        string SourcePath,
        string Body);

    private sealed record DocumentChunk(int Index, string Section, string Content);
}
