using ClaudeUsageMonitor.Models;
using Xunit;

namespace ClaudeUsageMonitor.Tests;

public class UsageRecordTests
{
    // --- TotalTokens -----------------------------------------------------

    [Fact]
    public void TotalTokens_SumsAllFourTokenFields()
    {
        var record = new UsageRecord
        {
            InputTokens = 100,
            OutputTokens = 200,
            CacheCreationTokens = 300,
            CacheReadTokens = 400
        };

        Assert.Equal(1000, record.TotalTokens);
    }

    [Fact]
    public void TotalTokens_IsZeroForEmptyRecord()
    {
        var record = new UsageRecord();
        Assert.Equal(0, record.TotalTokens);
    }

    [Theory]
    [InlineData(0, 0, 0, 0, 0)]
    [InlineData(1, 0, 0, 0, 1)]
    [InlineData(0, 0, 0, 1, 1)]
    [InlineData(1_000_000, 500_000, 250_000, 125_000, 1_875_000)]
    public void TotalTokens_Theory(int input, int output, int cacheCreate, int cacheRead, int expected)
    {
        var record = new UsageRecord
        {
            InputTokens = input,
            OutputTokens = output,
            CacheCreationTokens = cacheCreate,
            CacheReadTokens = cacheRead
        };

        Assert.Equal(expected, record.TotalTokens);
    }

    // --- EstimatedCostUsd: Opus ------------------------------------------

    [Fact]
    public void EstimatedCost_Opus_AppliesOpusPricing()
    {
        // Opus: $15 input / $75 output / $18.75 cache-create / $1.50 cache-read per 1M
        // 1M of each = 15 + 75 + 18.75 + 1.50 = 110.25
        var record = new UsageRecord
        {
            Model = "claude-opus-4-6",
            InputTokens = 1_000_000,
            OutputTokens = 1_000_000,
            CacheCreationTokens = 1_000_000,
            CacheReadTokens = 1_000_000
        };

        Assert.Equal(110.25m, record.EstimatedCostUsd);
    }

    [Fact]
    public void EstimatedCost_Opus_InputOnly()
    {
        var record = new UsageRecord
        {
            Model = "claude-opus-4-1-20250805",
            InputTokens = 2_000_000
        };

        // 2M × $15 / 1M = $30
        Assert.Equal(30m, record.EstimatedCostUsd);
    }

    // --- EstimatedCostUsd: Sonnet ----------------------------------------

    [Fact]
    public void EstimatedCost_Sonnet_AppliesSonnetPricing()
    {
        // Sonnet: $3 / $15 / $3.75 / $0.30 per 1M → 1M of each = 22.05
        var record = new UsageRecord
        {
            Model = "claude-sonnet-4-6",
            InputTokens = 1_000_000,
            OutputTokens = 1_000_000,
            CacheCreationTokens = 1_000_000,
            CacheReadTokens = 1_000_000
        };

        Assert.Equal(22.05m, record.EstimatedCostUsd);
    }

    [Fact]
    public void EstimatedCost_Sonnet_OutputDominant()
    {
        var record = new UsageRecord
        {
            Model = "claude-sonnet-4-5",
            OutputTokens = 500_000
        };

        // 500k × $15 / 1M = $7.50
        Assert.Equal(7.5m, record.EstimatedCostUsd);
    }

    // --- EstimatedCostUsd: Haiku -----------------------------------------

    [Fact]
    public void EstimatedCost_Haiku_AppliesHaikuPricing()
    {
        // Haiku: $0.80 / $4 / $1 / $0.08 per 1M → 1M of each = 5.88
        var record = new UsageRecord
        {
            Model = "claude-haiku-4-5-20251001",
            InputTokens = 1_000_000,
            OutputTokens = 1_000_000,
            CacheCreationTokens = 1_000_000,
            CacheReadTokens = 1_000_000
        };

        Assert.Equal(5.88m, record.EstimatedCostUsd);
    }

    // --- EstimatedCostUsd: Unknown model falls back to Sonnet ------------

    [Fact]
    public void EstimatedCost_UnknownModel_FallsBackToSonnetPricing()
    {
        var unknown = new UsageRecord
        {
            Model = "some-future-model-xyz",
            InputTokens = 1_000_000
        };
        var sonnet = new UsageRecord
        {
            Model = "claude-sonnet-4-6",
            InputTokens = 1_000_000
        };

        Assert.Equal(sonnet.EstimatedCostUsd, unknown.EstimatedCostUsd);
        Assert.Equal(3m, unknown.EstimatedCostUsd);
    }

    [Fact]
    public void EstimatedCost_EmptyModel_FallsBackToSonnetPricing()
    {
        var record = new UsageRecord
        {
            Model = "",
            OutputTokens = 1_000_000
        };

        // Sonnet output = $15/M
        Assert.Equal(15m, record.EstimatedCostUsd);
    }

    // --- EstimatedCostUsd: zero tokens → zero cost ----------------------

    [Theory]
    [InlineData("claude-opus-4-6")]
    [InlineData("claude-sonnet-4-6")]
    [InlineData("claude-haiku-4-5")]
    [InlineData("mystery-model")]
    public void EstimatedCost_ZeroTokens_IsZero(string model)
    {
        var record = new UsageRecord { Model = model };
        Assert.Equal(0m, record.EstimatedCostUsd);
    }

    // --- Cross-model sanity: Opus is strictly more expensive than Sonnet

    [Fact]
    public void EstimatedCost_OpusMoreExpensiveThanSonnet_ForSameTokens()
    {
        var opus = new UsageRecord
        {
            Model = "claude-opus-4-6",
            InputTokens = 100_000,
            OutputTokens = 50_000
        };
        var sonnet = new UsageRecord
        {
            Model = "claude-sonnet-4-6",
            InputTokens = 100_000,
            OutputTokens = 50_000
        };

        Assert.True(opus.EstimatedCostUsd > sonnet.EstimatedCostUsd);
    }

    [Fact]
    public void EstimatedCost_HaikuCheaperThanSonnet_ForSameTokens()
    {
        var haiku = new UsageRecord
        {
            Model = "claude-haiku-4-5",
            InputTokens = 100_000,
            OutputTokens = 50_000
        };
        var sonnet = new UsageRecord
        {
            Model = "claude-sonnet-4-6",
            InputTokens = 100_000,
            OutputTokens = 50_000
        };

        Assert.True(haiku.EstimatedCostUsd < sonnet.EstimatedCostUsd);
    }

    // --- EstimatedCostUsd: model-matching is case-sensitive --------------
    //
    // CalculateCost() uses `m.Contains("opus")` etc. — lowercase, case-sensitive.
    // These tests pin that behavior so a future refactor (e.g. switching to
    // StringComparison.OrdinalIgnoreCase) is an intentional decision, not a
    // silent regression.

    [Theory]
    [InlineData("claude-Opus-4-6")]
    [InlineData("CLAUDE-OPUS-4-6")]
    [InlineData("Claude-Sonnet-4-6")]
    [InlineData("claude-Haiku-4-5")]
    public void EstimatedCost_MatchingIsCaseSensitive_MixedCaseFallsBackToSonnet(string mixedCaseModel)
    {
        // 1M input tokens: Sonnet price = $3, Opus = $15, Haiku = $0.80
        // If the match were case-insensitive, mixed-case names would pick
        // the real tier and the cost would not equal $3.
        var record = new UsageRecord
        {
            Model = mixedCaseModel,
            InputTokens = 1_000_000
        };

        Assert.Equal(3m, record.EstimatedCostUsd);
    }

    // --- EstimatedCostUsd: switch precedence -----------------------------
    //
    // The switch order is opus → sonnet → haiku → default. A contrived model
    // string containing multiple keywords must resolve to the first match.

    [Fact]
    public void EstimatedCost_ModelWithBothOpusAndHaiku_UsesOpusPricing()
    {
        var record = new UsageRecord
        {
            Model = "opus-haiku-eval",
            InputTokens = 1_000_000
        };

        // Opus input = $15/M; Haiku input = $0.80/M
        Assert.Equal(15m, record.EstimatedCostUsd);
    }

    [Fact]
    public void EstimatedCost_ModelWithBothSonnetAndHaiku_UsesSonnetPricing()
    {
        var record = new UsageRecord
        {
            Model = "sonnet-haiku-bench",
            InputTokens = 1_000_000
        };

        // Sonnet input = $3/M; Haiku input = $0.80/M
        Assert.Equal(3m, record.EstimatedCostUsd);
    }

    // --- EstimatedCostUsd: per-component isolation -----------------------
    //
    // Each pricing tier has four coefficients (input / output / cacheCreate /
    // cacheRead). Previous tests either use one component or all four equally
    // — a typo in just one coefficient would slip through. This parametric
    // theory isolates each component for each tier so a regression on any
    // single coefficient fails loudly with a specific name.

    [Theory]
    // Opus: $15 / $75 / $18.75 / $1.50 per 1M
    [InlineData("claude-opus-4-6",    1_000_000, 0,         0,         0,          15.00)]
    [InlineData("claude-opus-4-6",    0,         1_000_000, 0,         0,          75.00)]
    [InlineData("claude-opus-4-6",    0,         0,         1_000_000, 0,          18.75)]
    [InlineData("claude-opus-4-6",    0,         0,         0,         1_000_000,   1.50)]
    // Sonnet: $3 / $15 / $3.75 / $0.30 per 1M
    [InlineData("claude-sonnet-4-6",  1_000_000, 0,         0,         0,           3.00)]
    [InlineData("claude-sonnet-4-6",  0,         1_000_000, 0,         0,          15.00)]
    [InlineData("claude-sonnet-4-6",  0,         0,         1_000_000, 0,           3.75)]
    [InlineData("claude-sonnet-4-6",  0,         0,         0,         1_000_000,   0.30)]
    // Haiku: $0.80 / $4 / $1 / $0.08 per 1M
    [InlineData("claude-haiku-4-5",   1_000_000, 0,         0,         0,           0.80)]
    [InlineData("claude-haiku-4-5",   0,         1_000_000, 0,         0,           4.00)]
    [InlineData("claude-haiku-4-5",   0,         0,         1_000_000, 0,           1.00)]
    [InlineData("claude-haiku-4-5",   0,         0,         0,         1_000_000,   0.08)]
    public void EstimatedCost_PerComponent_PerTier(
        string model, int input, int output, int cacheCreate, int cacheRead, double expectedUsd)
    {
        var record = new UsageRecord
        {
            Model = model,
            InputTokens = input,
            OutputTokens = output,
            CacheCreationTokens = cacheCreate,
            CacheReadTokens = cacheRead
        };

        Assert.Equal((decimal)expectedUsd, record.EstimatedCostUsd);
    }

    // --- EstimatedCostUsd: large-number sanity ---------------------------
    //
    // decimal has ample precision for these magnitudes, but this guards
    // against any future refactor accidentally routing the math through
    // int/long intermediates.

    [Fact]
    public void EstimatedCost_LargeTokenCounts_ProducesExpectedDecimalPrecision()
    {
        var record = new UsageRecord
        {
            Model = "claude-sonnet-4-6",
            InputTokens = int.MaxValue,  // ~2.147 B
            OutputTokens = int.MaxValue
        };

        // Expected: 2_147_483_647 × ($3 + $15) / 1_000_000
        //         = 2_147_483_647 × 18 / 1_000_000
        //         = 38_654.705646
        Assert.Equal(38_654.705646m, record.EstimatedCostUsd);
    }
}
