namespace ClaudeUsageMonitor.Models;

public record UsageRecord
{
    public DateTime Timestamp { get; init; }
    public string Model { get; init; } = "";
    public string SessionId { get; init; } = "";
    public string Project { get; init; } = "";
    public int InputTokens { get; init; }
    public int OutputTokens { get; init; }
    public int CacheCreationTokens { get; init; }
    public int CacheReadTokens { get; init; }
    public int TotalTokens => InputTokens + OutputTokens + CacheCreationTokens + CacheReadTokens;

    public decimal EstimatedCostUsd => CalculateCost();

    private decimal CalculateCost()
    {
        // Pricing per million tokens (as of 2025-2026)
        return Model switch
        {
            var m when m.Contains("opus") =>
                (InputTokens * 15m + OutputTokens * 75m + CacheCreationTokens * 18.75m + CacheReadTokens * 1.5m) / 1_000_000m,
            var m when m.Contains("sonnet") =>
                (InputTokens * 3m + OutputTokens * 15m + CacheCreationTokens * 3.75m + CacheReadTokens * 0.30m) / 1_000_000m,
            var m when m.Contains("haiku") =>
                (InputTokens * 0.80m + OutputTokens * 4m + CacheCreationTokens * 1m + CacheReadTokens * 0.08m) / 1_000_000m,
            _ =>
                (InputTokens * 3m + OutputTokens * 15m + CacheCreationTokens * 3.75m + CacheReadTokens * 0.30m) / 1_000_000m,
        };
    }
}

public record SessionSummary
{
    public string SessionId { get; init; } = "";
    public string Project { get; init; } = "";
    public DateTime StartTime { get; init; }
    public DateTime LastActivity { get; init; }
    public int TotalInputTokens { get; init; }
    public int TotalOutputTokens { get; init; }
    public int TotalCacheCreationTokens { get; init; }
    public int TotalCacheReadTokens { get; init; }
    public int ApiCalls { get; init; }
    public decimal TotalCostUsd { get; init; }
    public string PrimaryModel { get; init; } = "";
}

public record DailyUsage
{
    public DateOnly Date { get; init; }
    public int TotalTokens { get; init; }
    public decimal TotalCostUsd { get; init; }
    public int ApiCalls { get; init; }
}
