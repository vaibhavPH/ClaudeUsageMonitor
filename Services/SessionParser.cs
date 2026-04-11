using System.Runtime.CompilerServices;
using System.Text.Json;
using ClaudeUsageMonitor.Models;

[assembly: InternalsVisibleTo("ClaudeUsageMonitor.Tests")]

namespace ClaudeUsageMonitor.Services;

public class SessionParser
{
    private static readonly string ClaudeDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude");

    private static readonly string ProjectsDir = Path.Combine(ClaudeDir, "projects");

    public List<UsageRecord> ParseAllSessions()
    {
        var records = new List<UsageRecord>();

        if (!Directory.Exists(ProjectsDir))
            return records;

        foreach (var projectDir in Directory.GetDirectories(ProjectsDir))
        {
            var projectName = Path.GetFileName(projectDir);
            foreach (var jsonlFile in Directory.GetFiles(projectDir, "*.jsonl"))
            {
                try
                {
                    records.AddRange(ParseSessionFile(jsonlFile, projectName));
                }
                catch
                {
                    // Skip corrupted files
                }
            }
        }

        return records.OrderBy(r => r.Timestamp).ToList();
    }

    private IEnumerable<UsageRecord> ParseSessionFile(string filePath, string projectName)
    {
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);

        var sessionId = Path.GetFileNameWithoutExtension(filePath);
        string? line;

        while ((line = reader.ReadLine()) != null)
        {
            UsageRecord? record = null;
            try
            {
                record = ParseLine(line, sessionId, projectName);
            }
            catch
            {
                // Skip malformed lines
            }

            if (record != null)
                yield return record;
        }
    }

    internal UsageRecord? ParseLine(string line, string sessionId, string projectName)
    {
        using var doc = JsonDocument.Parse(line);
        var root = doc.RootElement;

        if (!root.TryGetProperty("type", out var typeProp) || typeProp.GetString() != "assistant")
            return null;

        if (!root.TryGetProperty("message", out var message))
            return null;

        if (!message.TryGetProperty("usage", out var usage))
            return null;

        var model = message.TryGetProperty("model", out var modelProp) ? modelProp.GetString() ?? "unknown" : "unknown";

        var inputTokens = usage.TryGetProperty("input_tokens", out var it) ? it.GetInt32() : 0;
        var outputTokens = usage.TryGetProperty("output_tokens", out var ot) ? ot.GetInt32() : 0;
        var cacheCreation = 0;
        var cacheRead = usage.TryGetProperty("cache_read_input_tokens", out var crt) ? crt.GetInt32() : 0;

        if (usage.TryGetProperty("cache_creation_input_tokens", out var cct))
        {
            cacheCreation = cct.GetInt32();
        }
        else if (usage.TryGetProperty("cache_creation", out var cc))
        {
            if (cc.TryGetProperty("ephemeral_1h_input_tokens", out var e1h))
                cacheCreation += e1h.GetInt32();
            if (cc.TryGetProperty("ephemeral_5m_input_tokens", out var e5m))
                cacheCreation += e5m.GetInt32();
        }

        var timestamp = root.TryGetProperty("timestamp", out var ts)
            ? DateTime.Parse(ts.GetString()!).ToLocalTime()
            : DateTime.Now;

        return new UsageRecord
        {
            Timestamp = timestamp,
            Model = model,
            SessionId = sessionId,
            Project = FormatProjectName(projectName),
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            CacheCreationTokens = cacheCreation,
            CacheReadTokens = cacheRead
        };
    }

    internal static string FormatProjectName(string raw)
    {
        // Convert "D--PVR-Birch" to "D:\PVR\Birch"
        if (raw.Length >= 2 && raw[1] == '-' && raw[2] == '-')
        {
            return raw[0] + ":\\" + raw[3..].Replace('-', '\\');
        }
        return raw.Replace('-', '\\');
    }

    public List<SessionSummary> GetSessionSummaries(List<UsageRecord> records)
    {
        return records
            .GroupBy(r => r.SessionId)
            .Select(g => new SessionSummary
            {
                SessionId = g.Key,
                Project = g.First().Project,
                StartTime = g.Min(r => r.Timestamp),
                LastActivity = g.Max(r => r.Timestamp),
                TotalInputTokens = g.Sum(r => r.InputTokens),
                TotalOutputTokens = g.Sum(r => r.OutputTokens),
                TotalCacheCreationTokens = g.Sum(r => r.CacheCreationTokens),
                TotalCacheReadTokens = g.Sum(r => r.CacheReadTokens),
                ApiCalls = g.Count(),
                TotalCostUsd = g.Sum(r => r.EstimatedCostUsd),
                PrimaryModel = g.GroupBy(r => r.Model).OrderByDescending(mg => mg.Count()).First().Key
            })
            .OrderByDescending(s => s.LastActivity)
            .ToList();
    }

    public List<DailyUsage> GetDailyUsage(List<UsageRecord> records)
    {
        return records
            .GroupBy(r => DateOnly.FromDateTime(r.Timestamp))
            .Select(g => new DailyUsage
            {
                Date = g.Key,
                TotalTokens = g.Sum(r => r.TotalTokens),
                TotalCostUsd = g.Sum(r => r.EstimatedCostUsd),
                ApiCalls = g.Count()
            })
            .OrderBy(d => d.Date)
            .ToList();
    }
}
