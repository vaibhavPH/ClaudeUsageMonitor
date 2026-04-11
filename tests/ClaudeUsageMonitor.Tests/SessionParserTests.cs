using ClaudeUsageMonitor.Models;
using ClaudeUsageMonitor.Services;
using Xunit;

namespace ClaudeUsageMonitor.Tests;

public class SessionParserTests
{
    private static UsageRecord R(
        string sessionId,
        string model,
        DateTime timestamp,
        int input = 0,
        int output = 0,
        int cacheCreate = 0,
        int cacheRead = 0,
        string project = "test-project") => new()
    {
        SessionId = sessionId,
        Project = project,
        Model = model,
        Timestamp = timestamp,
        InputTokens = input,
        OutputTokens = output,
        CacheCreationTokens = cacheCreate,
        CacheReadTokens = cacheRead
    };

    // --- GetSessionSummaries ---------------------------------------------

    [Fact]
    public void GetSessionSummaries_EmptyInput_ReturnsEmptyList()
    {
        var parser = new SessionParser();
        var summaries = parser.GetSessionSummaries([]);
        Assert.Empty(summaries);
    }

    [Fact]
    public void GetSessionSummaries_SingleRecord_ProducesOneSummaryWithMatchingTotals()
    {
        var parser = new SessionParser();
        var ts = new DateTime(2026, 4, 1, 10, 0, 0);
        var records = new List<UsageRecord>
        {
            R("session-a", "claude-sonnet-4-6", ts, input: 1000, output: 500, cacheCreate: 200, cacheRead: 100)
        };

        var summaries = parser.GetSessionSummaries(records);

        var s = Assert.Single(summaries);
        Assert.Equal("session-a", s.SessionId);
        Assert.Equal(1000, s.TotalInputTokens);
        Assert.Equal(500, s.TotalOutputTokens);
        Assert.Equal(200, s.TotalCacheCreationTokens);
        Assert.Equal(100, s.TotalCacheReadTokens);
        Assert.Equal(1, s.ApiCalls);
        Assert.Equal(ts, s.StartTime);
        Assert.Equal(ts, s.LastActivity);
        Assert.Equal("claude-sonnet-4-6", s.PrimaryModel);
        Assert.Equal(records[0].EstimatedCostUsd, s.TotalCostUsd);
    }

    [Fact]
    public void GetSessionSummaries_MultipleRecordsInSameSession_AggregatesTokensAndCost()
    {
        var parser = new SessionParser();
        var t1 = new DateTime(2026, 4, 1, 9, 0, 0);
        var t2 = new DateTime(2026, 4, 1, 9, 30, 0);
        var t3 = new DateTime(2026, 4, 1, 10, 0, 0);
        var records = new List<UsageRecord>
        {
            R("session-b", "claude-sonnet-4-6", t1, input: 1000, output: 500),
            R("session-b", "claude-sonnet-4-6", t2, input: 2000, output: 1000),
            R("session-b", "claude-sonnet-4-6", t3, input: 3000, output: 1500)
        };

        var summaries = parser.GetSessionSummaries(records);

        var s = Assert.Single(summaries);
        Assert.Equal(3, s.ApiCalls);
        Assert.Equal(6000, s.TotalInputTokens);
        Assert.Equal(3000, s.TotalOutputTokens);
        Assert.Equal(t1, s.StartTime);
        Assert.Equal(t3, s.LastActivity);
        // Cost = 3 records × (input*3 + output*15) / 1M (sonnet pricing)
        // = (6000*3 + 3000*15) / 1M = (18000 + 45000) / 1M = 0.063
        Assert.Equal(0.063m, s.TotalCostUsd);
    }

    [Fact]
    public void GetSessionSummaries_GroupsBySessionId()
    {
        var parser = new SessionParser();
        var records = new List<UsageRecord>
        {
            R("session-a", "claude-sonnet-4-6", new DateTime(2026, 4, 1), input: 100),
            R("session-b", "claude-opus-4-6", new DateTime(2026, 4, 2), input: 200),
            R("session-a", "claude-sonnet-4-6", new DateTime(2026, 4, 3), input: 300),
        };

        var summaries = parser.GetSessionSummaries(records);

        Assert.Equal(2, summaries.Count);
        var a = summaries.Single(s => s.SessionId == "session-a");
        var b = summaries.Single(s => s.SessionId == "session-b");
        Assert.Equal(400, a.TotalInputTokens);
        Assert.Equal(200, b.TotalInputTokens);
        Assert.Equal(2, a.ApiCalls);
        Assert.Equal(1, b.ApiCalls);
    }

    [Fact]
    public void GetSessionSummaries_OrderedByMostRecentLastActivityFirst()
    {
        var parser = new SessionParser();
        var records = new List<UsageRecord>
        {
            R("oldest",   "claude-sonnet-4-6", new DateTime(2026, 3, 1)),
            R("newest",   "claude-sonnet-4-6", new DateTime(2026, 4, 1)),
            R("middle",   "claude-sonnet-4-6", new DateTime(2026, 3, 15))
        };

        var summaries = parser.GetSessionSummaries(records);

        Assert.Equal("newest", summaries[0].SessionId);
        Assert.Equal("middle", summaries[1].SessionId);
        Assert.Equal("oldest", summaries[2].SessionId);
    }

    [Fact]
    public void GetSessionSummaries_PrimaryModelIsMostFrequentlyUsed()
    {
        var parser = new SessionParser();
        var ts = new DateTime(2026, 4, 1);
        var records = new List<UsageRecord>
        {
            R("s", "claude-sonnet-4-6", ts.AddMinutes(1)),
            R("s", "claude-sonnet-4-6", ts.AddMinutes(2)),
            R("s", "claude-opus-4-6",   ts.AddMinutes(3)),
        };

        var summary = Assert.Single(parser.GetSessionSummaries(records));
        Assert.Equal("claude-sonnet-4-6", summary.PrimaryModel);
    }

    // --- GetDailyUsage ---------------------------------------------------

    [Fact]
    public void GetDailyUsage_EmptyInput_ReturnsEmptyList()
    {
        var parser = new SessionParser();
        Assert.Empty(parser.GetDailyUsage([]));
    }

    [Fact]
    public void GetDailyUsage_GroupsByDateAndSumsTokens()
    {
        var parser = new SessionParser();
        var records = new List<UsageRecord>
        {
            R("s1", "claude-sonnet-4-6", new DateTime(2026, 4, 1, 9, 0, 0),  input: 1000, output: 500),
            R("s1", "claude-sonnet-4-6", new DateTime(2026, 4, 1, 15, 0, 0), input: 2000, output: 1000),
            R("s2", "claude-sonnet-4-6", new DateTime(2026, 4, 2, 9, 0, 0),  input: 500,  output: 250),
        };

        var daily = parser.GetDailyUsage(records);

        Assert.Equal(2, daily.Count);
        var day1 = daily.Single(d => d.Date == new DateOnly(2026, 4, 1));
        var day2 = daily.Single(d => d.Date == new DateOnly(2026, 4, 2));

        // Day 1: 2 records, 1000+500 + 2000+1000 = 4500 tokens
        Assert.Equal(4500, day1.TotalTokens);
        Assert.Equal(2, day1.ApiCalls);

        // Day 2: 1 record, 500+250 = 750 tokens
        Assert.Equal(750, day2.TotalTokens);
        Assert.Equal(1, day2.ApiCalls);
    }

    [Fact]
    public void GetDailyUsage_SumsCostAcrossAllRecordsInDay()
    {
        var parser = new SessionParser();
        var records = new List<UsageRecord>
        {
            // Sonnet: $3 input, $15 output per 1M
            R("s", "claude-sonnet-4-6", new DateTime(2026, 4, 1, 9, 0, 0),  input: 1_000_000, output: 0),      // $3
            R("s", "claude-sonnet-4-6", new DateTime(2026, 4, 1, 15, 0, 0), input: 0,         output: 200_000) // $3
        };

        var daily = Assert.Single(parser.GetDailyUsage(records));
        Assert.Equal(6m, daily.TotalCostUsd);
    }

    [Fact]
    public void GetDailyUsage_OrderedByDateAscending()
    {
        var parser = new SessionParser();
        var records = new List<UsageRecord>
        {
            R("s", "claude-sonnet-4-6", new DateTime(2026, 4, 5)),
            R("s", "claude-sonnet-4-6", new DateTime(2026, 4, 1)),
            R("s", "claude-sonnet-4-6", new DateTime(2026, 4, 3)),
        };

        var daily = parser.GetDailyUsage(records);

        Assert.Equal(new DateOnly(2026, 4, 1), daily[0].Date);
        Assert.Equal(new DateOnly(2026, 4, 3), daily[1].Date);
        Assert.Equal(new DateOnly(2026, 4, 5), daily[2].Date);
    }

    [Fact]
    public void GetDailyUsage_TotalTokensIncludesAllFourTokenCategories()
    {
        var parser = new SessionParser();
        var records = new List<UsageRecord>
        {
            R("s", "claude-sonnet-4-6", new DateTime(2026, 4, 1),
              input: 100, output: 200, cacheCreate: 300, cacheRead: 400)
        };

        var daily = Assert.Single(parser.GetDailyUsage(records));
        Assert.Equal(1000, daily.TotalTokens);
    }
}
