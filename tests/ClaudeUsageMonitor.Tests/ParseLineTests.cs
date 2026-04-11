using ClaudeUsageMonitor.Services;
using Xunit;

namespace ClaudeUsageMonitor.Tests;

public class ParseLineTests
{
    private readonly SessionParser _parser = new();

    // --- Type / structure filters ----------------------------------------

    [Fact]
    public void NonAssistantType_ReturnsNull()
    {
        var line = """{"type":"user","message":{"usage":{"input_tokens":100}}}""";
        Assert.Null(_parser.ParseLine(line, "session", "project"));
    }

    [Fact]
    public void MissingType_ReturnsNull()
    {
        var line = """{"message":{"usage":{"input_tokens":100}}}""";
        Assert.Null(_parser.ParseLine(line, "session", "project"));
    }

    [Fact]
    public void MissingMessage_ReturnsNull()
    {
        var line = """{"type":"assistant"}""";
        Assert.Null(_parser.ParseLine(line, "session", "project"));
    }

    [Fact]
    public void MissingUsage_ReturnsNull()
    {
        var line = """{"type":"assistant","message":{"model":"claude-sonnet-4-6"}}""";
        Assert.Null(_parser.ParseLine(line, "session", "project"));
    }

    // --- Token field extraction ------------------------------------------

    [Fact]
    public void ValidAssistantRecord_ParsesAllTokenFields()
    {
        var line = """
            {
              "type": "assistant",
              "timestamp": "2026-04-01T10:30:00Z",
              "message": {
                "model": "claude-sonnet-4-6",
                "usage": {
                  "input_tokens": 1000,
                  "output_tokens": 500,
                  "cache_creation_input_tokens": 200,
                  "cache_read_input_tokens": 100
                }
              }
            }
            """;

        var record = _parser.ParseLine(line, "session-a", "test-project");

        Assert.NotNull(record);
        Assert.Equal("claude-sonnet-4-6", record.Model);
        Assert.Equal(1000, record.InputTokens);
        Assert.Equal(500, record.OutputTokens);
        Assert.Equal(200, record.CacheCreationTokens);
        Assert.Equal(100, record.CacheReadTokens);
        Assert.Equal("session-a", record.SessionId);
    }

    [Fact]
    public void MissingTokenFields_DefaultToZero()
    {
        var line = """{"type":"assistant","message":{"model":"claude-sonnet-4-6","usage":{}}}""";
        var record = _parser.ParseLine(line, "s", "p");

        Assert.NotNull(record);
        Assert.Equal(0, record.InputTokens);
        Assert.Equal(0, record.OutputTokens);
        Assert.Equal(0, record.CacheCreationTokens);
        Assert.Equal(0, record.CacheReadTokens);
    }

    [Fact]
    public void MissingModel_DefaultsToUnknown()
    {
        var line = """{"type":"assistant","message":{"usage":{"input_tokens":1}}}""";
        var record = _parser.ParseLine(line, "s", "p");

        Assert.NotNull(record);
        Assert.Equal("unknown", record.Model);
    }

    // --- cache_creation handling: flat vs ephemeral ----------------------

    [Fact]
    public void FlatCacheCreation_IsUsedWhenPresent()
    {
        var line = """
            {"type":"assistant","message":{"model":"m","usage":{
                "cache_creation_input_tokens": 750
            }}}
            """;
        var record = _parser.ParseLine(line, "s", "p");

        Assert.NotNull(record);
        Assert.Equal(750, record.CacheCreationTokens);
    }

    [Fact]
    public void EphemeralCacheCreation_SumsBothEphemeralFields()
    {
        var line = """
            {"type":"assistant","message":{"model":"m","usage":{
                "cache_creation": {
                    "ephemeral_1h_input_tokens": 100,
                    "ephemeral_5m_input_tokens": 200
                }
            }}}
            """;
        var record = _parser.ParseLine(line, "s", "p");

        Assert.NotNull(record);
        Assert.Equal(300, record.CacheCreationTokens);
    }

    [Fact]
    public void EphemeralCacheCreation_WithOnlyOneFieldPresent_SumsOnlyThatField()
    {
        var line = """
            {"type":"assistant","message":{"model":"m","usage":{
                "cache_creation": { "ephemeral_1h_input_tokens": 100 }
            }}}
            """;
        var record = _parser.ParseLine(line, "s", "p");

        Assert.NotNull(record);
        Assert.Equal(100, record.CacheCreationTokens);
    }

    [Fact]
    public void FlatCacheCreation_TakesPrecedenceOverEphemeralWhenBothPresent()
    {
        // Current implementation: the `if` branch on cache_creation_input_tokens
        // runs first, and the ephemeral block is in the else-if branch so it's
        // ignored entirely when both are present.
        var line = """
            {"type":"assistant","message":{"model":"m","usage":{
                "cache_creation_input_tokens": 500,
                "cache_creation": {
                    "ephemeral_1h_input_tokens": 100,
                    "ephemeral_5m_input_tokens": 200
                }
            }}}
            """;
        var record = _parser.ParseLine(line, "s", "p");

        Assert.NotNull(record);
        Assert.Equal(500, record.CacheCreationTokens);
    }

    // --- Timestamp handling ----------------------------------------------

    [Fact]
    public void Timestamp_ParsedFromIso8601AndConvertedToLocal()
    {
        // A fixed UTC timestamp. After ParseLine calls ToLocalTime() we can
        // compare the UTC round-trip instead of hard-coding the local value,
        // since the test may run in any timezone.
        var line = """
            {
              "type": "assistant",
              "timestamp": "2026-04-01T10:30:00Z",
              "message": {"model":"m","usage":{"input_tokens":1}}
            }
            """;
        var record = _parser.ParseLine(line, "s", "p");

        Assert.NotNull(record);
        // After ToLocalTime(), the Kind is Local. Converting back to UTC must
        // match the original instant.
        Assert.Equal(DateTimeKind.Local, record.Timestamp.Kind);
        Assert.Equal(new DateTime(2026, 4, 1, 10, 30, 0, DateTimeKind.Utc),
                     record.Timestamp.ToUniversalTime());
    }

    [Fact]
    public void MissingTimestamp_UsesCurrentTime()
    {
        var before = DateTime.Now.AddSeconds(-1);
        var line = """{"type":"assistant","message":{"model":"m","usage":{"input_tokens":1}}}""";
        var record = _parser.ParseLine(line, "s", "p");
        var after = DateTime.Now.AddSeconds(1);

        Assert.NotNull(record);
        Assert.InRange(record.Timestamp, before, after);
    }

    // --- SessionId / Project propagation ---------------------------------

    [Fact]
    public void SessionIdAndProject_AreTakenFromArguments()
    {
        var line = """{"type":"assistant","message":{"model":"m","usage":{"input_tokens":1}}}""";
        var record = _parser.ParseLine(line, "my-session-id", "plain-project");

        Assert.NotNull(record);
        Assert.Equal("my-session-id", record.SessionId);
        // "plain-project" is not a drive-letter pattern, so FormatProjectName
        // just replaces dashes with backslashes.
        Assert.Equal("plain\\project", record.Project);
    }

    [Fact]
    public void ProjectName_IsFormattedThroughFormatProjectName()
    {
        var line = """{"type":"assistant","message":{"model":"m","usage":{"input_tokens":1}}}""";
        var record = _parser.ParseLine(line, "s", "D--PVR-ClaudeUsage");

        Assert.NotNull(record);
        Assert.Equal("D:\\PVR\\ClaudeUsage", record.Project);
    }
}
