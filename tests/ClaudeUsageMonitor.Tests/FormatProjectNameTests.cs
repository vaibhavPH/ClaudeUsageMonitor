using ClaudeUsageMonitor.Services;
using Xunit;

namespace ClaudeUsageMonitor.Tests;

public class FormatProjectNameTests
{
    // --- Drive-letter pattern ("X--rest-of-path") ------------------------

    [Theory]
    [InlineData("D--PVR-ClaudeUsage",    @"D:\PVR\ClaudeUsage")]
    [InlineData("C--Users-vibs2-src",    @"C:\Users\vibs2\src")]
    [InlineData("E--",                   @"E:\")]
    [InlineData("D--PVR",                @"D:\PVR")]
    public void DriveLetterPattern_ConvertsToColonAndBackslashes(string raw, string expected)
    {
        Assert.Equal(expected, SessionParser.FormatProjectName(raw));
    }

    // --- Non-drive pattern: dashes become backslashes --------------------

    [Theory]
    [InlineData("my-project-name", @"my\project\name")]
    [InlineData("singleword",      "singleword")]
    [InlineData("a-b",             @"a\b")]
    [InlineData("ab--c",           @"ab\\c")]
    public void NonDrivePattern_ReplacesDashesWithBackslashes(string raw, string expected)
    {
        Assert.Equal(expected, SessionParser.FormatProjectName(raw));
    }

    [Fact]
    public void SingleCharDashPattern_FallsThroughBecauseSecondCharIsNotDash()
    {
        // "a-b-c": raw[1] == '-', raw[2] == 'b' — not both dashes, so the
        // drive-letter branch is skipped and every dash becomes a backslash.
        Assert.Equal(@"a\b\c", SessionParser.FormatProjectName("a-b-c"));
    }
}
