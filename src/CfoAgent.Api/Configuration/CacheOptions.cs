namespace CfoAgent.Api.Configuration;

public sealed class CacheOptions
{
    public const string SectionName = "Cache";

    public bool Enabled { get; init; } = true;

    public bool UseDistributedCache { get; init; }

    public FinanceCacheOptions Finance { get; init; } = new();

    public RagCacheOptions Rag { get; init; } = new();

    public EmbeddingCacheOptions Embeddings { get; init; } = new();
}

public sealed class RagCacheOptions
{
    public int RetrievalTtlSeconds { get; init; } = 300;
}

public sealed class EmbeddingCacheOptions
{
    public int TtlSeconds { get; init; } = 3600;

    public string Version { get; init; } = "v1";
}

public sealed class FinanceCacheOptions
{
    public int SalesSummaryTtlSeconds { get; init; } = 60;

    public int CurrentWeekSummaryTtlSeconds { get; init; } = 60;

    public int WeekOverWeekComparisonTtlSeconds { get; init; } = 60;

    public int CurrentMonthTopProductsTtlSeconds { get; init; } = 300;

    public int HistoricalYearlyTotalsTtlSeconds { get; init; } = 3600;

    public int BudgetTargetTtlSeconds { get; init; } = 300;

    public IEnumerable<int> AllTtlSeconds()
    {
        yield return SalesSummaryTtlSeconds;
        yield return CurrentWeekSummaryTtlSeconds;
        yield return WeekOverWeekComparisonTtlSeconds;
        yield return CurrentMonthTopProductsTtlSeconds;
        yield return HistoricalYearlyTotalsTtlSeconds;
        yield return BudgetTargetTtlSeconds;
    }
}
