# Claude Usage Monitor

A Windows desktop dashboard that tracks your [Claude Code](https://docs.anthropic.com/en/docs/claude-code) API token usage and estimated costs in real time.

![.NET](https://img.shields.io/badge/.NET_10-512BD4?style=flat&logo=dotnet&logoColor=white)
![Windows](https://img.shields.io/badge/Windows_10+-0078D6?style=flat&logo=windows&logoColor=white)
![License](https://img.shields.io/badge/license-MIT-green)

![Claude Usage Monitor Dashboard](screenshot.png)

## Download (No Setup Required)

**Don't want to build from source?** Download the pre-built executable — no .NET installation needed:

[**Download ClaudeUsageMonitor v1.0.5 (Windows x64)**](https://github.com/vaibhavPH/ClaudeUsageMonitor/releases/download/v1.0.5/ClaudeUsageMonitor-v1.0.5-win-x64.zip)

1. Download and extract the zip
2. Double-click `ClaudeUsageMonitor.exe`
3. Done — the dashboard will show your Claude Code usage

> **Requirements:** Windows 10+ (x64) and [Claude Code](https://docs.anthropic.com/en/docs/claude-code) installed.

See all releases: [GitHub Releases](https://github.com/vaibhavPH/ClaudeUsageMonitor/releases)

---

## What It Does

Claude Code stores session data as JSONL files in `~/.claude/projects/`. This app parses those files and gives you a live dashboard showing:

- **Total tokens used** (input, output, cache read, cache creation)
- **Estimated cost in USD** per model (Opus, Sonnet, Haiku)
- **Daily cost bar chart** — see spending trends over time
- **Daily token usage chart** — stacked input vs. output tokens
- **Cost by model pie chart** — which models are costing the most
- **Session browser** — every Claude Code session with project, model, token counts, and cost
- **Detailed log** — individual API call records with full token breakdown

The app runs in the system tray, auto-refreshes, and can optionally start with Windows.

---

## Prerequisites

| Requirement | Version |
|---|---|
| **Windows** | 10 or later |
| **.NET SDK** | [10.0](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) (LTS) |
| **Claude Code** | Any version — just needs `~/.claude/projects/` with session files |

> **Note:** .NET 10 is a Long-Term Support (LTS) release (released November 11, 2025). If you prefer to build against .NET 9, change `net10.0-windows` to `net9.0-windows` in the `.csproj` file.

---

## Quick Start

### 1. Clone the repo

```bash
git clone https://github.com/vaibhavPH/ClaudeUsageMonitor.git
cd ClaudeUsageMonitor
```

### 2. Build and run

```bash
dotnet run
```

That's it. The dashboard window will appear and start reading your Claude Code session data.

### 3. (Optional) Publish a standalone executable

```bash
dotnet publish -c Release -r win-x64 --self-contained
```

The output will be in `bin/Release/net10.0-windows/win-x64/publish/`.

---

## How It Works

### Data Source

Claude Code writes a JSONL file for each session at:

```
%USERPROFILE%\.claude\projects\<project-name>\<session-id>.jsonl
```

Each line is a JSON object. The app looks for `"type": "assistant"` messages that contain a `message.usage` block with token counts:

```json
{
  "type": "assistant",
  "message": {
    "model": "claude-opus-4-6-20250414",
    "usage": {
      "input_tokens": 1234,
      "output_tokens": 567,
      "cache_read_input_tokens": 890,
      "cache_creation_input_tokens": 100
    }
  },
  "timestamp": "2026-04-09T10:30:00Z"
}
```

### Understanding Token Counts

> **Why does "Total Tokens" look so high?** This is normal. Don't panic.

The dashboard shows **total tokens across all four categories**:

```
Total Tokens = Input + Output + Cache Read + Cache Creation
```

In practice, **cache read tokens make up ~95% of the total**. Here's a real-world example:

| Token Type | Count | % of Total | What It Means |
|---|---|---|---|
| **Cache Read** | 1,322M | 95.7% | Conversation context re-read from cache on every API call |
| **Cache Creation** | 53M | 3.9% | New context written into cache |
| **Output** | 4.2M | 0.3% | Claude's actual responses |
| **Input** | 1.2M | 0.1% | Your prompts/messages |

**Why are cache reads so large?**

Every time Claude Code makes an API call, it sends your entire conversation history as context. Anthropic caches this context so it doesn't need to be re-processed from scratch each time. A typical Claude Code session has 100K-500K tokens of context, and with thousands of API calls, the cache read count grows into the hundreds of millions.

**The key insight: cache reads are very cheap.** For Opus, cache reads cost **$1.50 per million tokens** — that's 10x cheaper than regular input ($15/M) and 50x cheaper than output ($75/M). So even though the raw token count looks enormous, the actual cost is reasonable.

**Example:** 1,322M cache read tokens on Opus = ~$1,983. The same volume as regular input would cost $19,833.

### Understanding API Calls

The **API Calls** number on the dashboard counts how many times Claude Code contacted the Claude API. This is **not** the number of messages you typed — it's usually much higher. Here's why.

#### What triggers an API call?

When you use Claude Code, a single interaction from you can trigger **multiple** API calls behind the scenes:

| Your Action | What Happens Behind the Scenes | API Calls |
|---|---|---|
| You type a message / ask a question | Claude Code sends your message + full conversation history to Claude | **1** |
| Claude decides to read a file | Claude calls the Read tool, gets the result, then sends it back to Claude for processing | **+1** |
| Claude decides to edit a file | Claude calls the Edit tool, gets confirmation, sends it back | **+1** |
| Claude runs a terminal command | Claude calls the Bash tool, gets output, sends it back | **+1** |
| Claude searches for files | Claude calls Grep/Glob, gets results, sends them back | **+1** |
| Claude uses any other tool | Same pattern — each tool use is a round-trip | **+1** |

#### A typical example

Say you ask: *"Fix the bug in the login page"*

Claude might:
1. **Call 1** — Reads your message, decides it needs to find the login file
2. **Call 2** — Uses Grep to search for "login" across the codebase, gets results
3. **Call 3** — Reads `login.tsx` to understand the code
4. **Call 4** — Reads a related file `auth.ts` for context
5. **Call 5** — Edits `login.tsx` with the fix
6. **Call 6** — Runs `npm test` to verify the fix works
7. **Call 7** — Reads the test output and responds to you with a summary

That's **7 API calls from a single message**. Each call sends the growing conversation context (including all previous tool results) back to Claude, which is why cache read tokens accumulate so fast.

#### Why the number might seem high

| Scenario | Typical API Calls |
|---|---|
| Simple question ("what does this function do?") | 1-3 |
| Bug fix with file edits | 5-15 |
| New feature implementation | 20-50+ |
| Large refactoring across multiple files | 50-100+ |
| Full day of active Claude Code use | 200-500+ |

Over weeks of use, thousands of API calls is completely normal.

#### How this app counts API calls

The app counts every `"type": "assistant"` message in the JSONL session files that includes a `usage` block. Each of these represents one round-trip to the Claude API — one request sent, one response received.

```
API Calls = number of assistant messages with usage data across all sessions
```

### Cost Calculation

#### What does "Est. Cost" mean?

The **Est. Cost (USD)** shown on the dashboard is an **estimate** of what your usage would cost at Anthropic's public API pricing. This is useful to understand the value of the compute you're consuming.

> **Important: This is NOT your actual bill.**
> - If you're on **Claude Pro** ($20/month) or **Claude Max** ($100/$200/month), you pay a flat subscription — not per-token. The estimated cost helps you understand how much value you're getting from your subscription.
> - If you're using Claude Code with an **API key**, then this estimate closely reflects your actual charges on the [Anthropic billing dashboard](https://console.anthropic.com/).

#### Pricing table

Costs are calculated using Anthropic's published API pricing (per million tokens):

| Model | Input | Output | Cache Write | Cache Read |
|---|---|---|---|---|
| **Opus 4** | $15.00 | $75.00 | $18.75 | $1.50 |
| **Sonnet 4** | $3.00 | $15.00 | $3.75 | $0.30 |
| **Haiku 4** | $0.80 | $4.00 | $1.00 | $0.08 |

> Pricing as of early 2026. If Anthropic updates pricing, edit the rates in [`Models/UsageRecord.cs`](Models/UsageRecord.cs).

#### How cost is calculated per API call

Each API call in the JSONL file contains four token counts. The app multiplies each by the model's rate and sums them:

```
Cost = (input_tokens × input_rate + output_tokens × output_rate
      + cache_creation_tokens × cache_write_rate
      + cache_read_tokens × cache_read_rate) / 1,000,000
```

#### Worked example

A single Opus API call with:
- `input_tokens`: 50
- `output_tokens`: 350
- `cache_creation_input_tokens`: 5,000
- `cache_read_input_tokens`: 150,000

```
Input cost:          50 × $15.00   = $0.000750
Output cost:        350 × $75.00   = $0.026250
Cache write cost: 5,000 × $18.75   = $0.093750
Cache read cost: 150,000 × $1.50   = $0.225000
                                      ─────────
Total for this call:                  $0.345750
```

Notice that **cache read is 65% of the cost** here, even though it's the cheapest rate — because the volume is so high.

#### How totals are aggregated

| Dashboard metric | How it's calculated |
|---|---|
| **Est. Cost** (top card) | Sum of per-call costs across all API calls in the selected date range |
| **Daily Cost** (bar chart) | Per-call costs grouped by date |
| **Cost by Model** (pie chart) | Per-call costs grouped by model name |
| **Session Est. Cost** (sessions tab) | Per-call costs grouped by session ID |

#### Cost breakdown by what drives spending

In a typical heavy-usage scenario, here's where the money goes:

| Cost Driver | % of Total Cost | Why |
|---|---|---|
| **Output tokens** | ~40-50% | Claude's responses — most expensive rate ($75/M for Opus) |
| **Cache read tokens** | ~30-40% | Cheap per-token ($1.50/M) but enormous volume |
| **Cache creation tokens** | ~15-20% | Writing new context into cache |
| **Input tokens** | <1% | Your actual prompts are tiny compared to context |

The takeaway: **output tokens are the biggest cost driver** despite being a small fraction of total token count, because they're priced 50x higher than cache reads.

### Real-Time Updates

The app uses a `FileSystemWatcher` on `~/.claude/projects/` to detect new session data as Claude Code writes it. Changes trigger a dashboard refresh with a 5-second debounce. There's also a configurable auto-refresh timer (default: 60 seconds).

---

## Features

### Dashboard Tab

Three charts on one screen:
- **Daily Cost** — bar chart of USD spent per day
- **Daily Token Usage** — stacked bars showing input vs. output tokens
- **Cost by Model** — pie chart breakdown

### Sessions Tab

A sortable grid listing every Claude Code session:
- Project path
- Session ID
- Start time and last activity
- Primary model used
- API call count
- Input/output tokens
- Estimated cost

### Detailed Log Tab

Raw API call records (most recent 500) with full token breakdown per call.

### System Tray

- Closing the window minimizes to the system tray (does **not** exit)
- Double-click the tray icon to reopen the dashboard
- Right-click for quick actions: Open, Refresh, Exit
- Tray tooltip shows current token/cost totals

### Start with Windows

Check the **"Start with Windows"** checkbox in the toolbar. This adds a registry entry at:

```
HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run\ClaudeUsageMonitor
```

The app launches minimized to the tray on startup.

### Date Range Filter

Filter all data by:
- Today
- Last 7 Days
- Last 30 Days
- All Time

---

## Project Structure

```
ClaudeUsageMonitor/
├── Program.cs                  # Entry point, single-instance mutex
├── Form1.cs                    # Main dashboard form & UI
├── Form1.Designer.cs           # WinForms designer file
├── Models/
│   └── UsageRecord.cs          # Data models (UsageRecord, SessionSummary, DailyUsage)
├── Services/
│   ├── SessionParser.cs        # JSONL file parser & aggregation
│   └── StartupManager.cs       # Windows startup registry manager
└── ClaudeUsageMonitor.csproj   # Project file
```

---

## Configuration

| Setting | Where | Default |
|---|---|---|
| Auto-refresh interval | Toolbar spinner | 60 seconds |
| Start with Windows | Toolbar checkbox | Off |
| Date range filter | Toolbar dropdown | All Time |

All settings are in-memory only (no config file). The "Start with Windows" toggle persists via the Windows Registry.

---

## Troubleshooting

### App launches but shows no data

- Make sure you've used Claude Code at least once. Check that `%USERPROFILE%\.claude\projects\` exists and contains `.jsonl` files.
- Click **Refresh Now** to force a reload.

### Build errors with .NET 10

If you don't have .NET 10 installed, edit `ClaudeUsageMonitor.csproj` and change the target framework:

```xml
<TargetFramework>net9.0-windows</TargetFramework>
```

### App won't start (single instance)

The app uses a Mutex to prevent multiple instances. If a previous instance crashed, wait a few seconds and try again, or check Task Manager for a lingering `ClaudeUsageMonitor` process.

### NuGet restore warnings

You may see `NU1701` warnings about package compatibility. These are harmless — the app runs correctly despite the warnings.

---

## Tech Stack

- **C# / .NET 10** — WinForms application
- **[LiveCharts2](https://livecharts.dev/)** — charts (SkiaSharp-based, WinForms integration)
- **SkiaSharp** — rendering backend for charts
- **System.Text.Json** — JSONL parsing

---

## Contributing

1. Fork the repo
2. Create a feature branch (`git checkout -b feature/my-feature`)
3. Commit your changes
4. Push and open a Pull Request

---

## License

MIT License. See [LICENSE](LICENSE) for details.
