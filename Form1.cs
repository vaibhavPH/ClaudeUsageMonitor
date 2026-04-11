using ClaudeUsageMonitor.Models;
using ClaudeUsageMonitor.Services;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Drawing.Geometries;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.VisualElements;
using LiveChartsCore.SkiaSharpView.WinForms;
using SkiaSharp;
using Align = LiveChartsCore.Drawing.Align;

namespace ClaudeUsageMonitor;

public partial class Form1 : Form
{
    private readonly SessionParser _parser = new();
    private readonly System.Windows.Forms.Timer _refreshTimer = new();
    private FileSystemWatcher? _watcher;

    // UI Controls
    private TabControl _tabControl = null!;
    private Label _lblTotalTokens = null!;
    private Label _lblTotalCost = null!;
    private Label _lblTotalCalls = null!;
    private Label _lblActiveSessions = null!;
    private Label _lblLastRefresh = null!;
    private CartesianChart _dailyCostChart = null!;
    private PieChart _modelPieChart = null!;
    private CartesianChart _tokenChart = null!;
    private DataGridView _sessionGrid = null!;
    private DataGridView _detailGrid = null!;
    private ComboBox _cmbDateRange = null!;
    private CheckBox _chkAutoStart = null!;
    private NumericUpDown _nudRefreshInterval = null!;
    private NotifyIcon _trayIcon = null!;
    private Button _btnRefresh = null!;

    // Data
    private List<UsageRecord> _allRecords = new();

    public Form1()
    {
        InitializeComponent();
        BuildUI();
        SetupTrayIcon();
        SetupFileWatcher();
        SetupRefreshTimer();
        LoadData();
    }

    private void BuildUI()
    {
        Text = "Claude Usage Monitor";
        Size = new Size(1200, 800);
        MinimumSize = new Size(900, 600);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(30, 30, 30);
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 9.5f);

        // Top bar with summary cards
        var topPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 100,
            BackColor = Color.FromArgb(40, 40, 40),
            Padding = new Padding(15, 10, 15, 10)
        };

        _lblTotalTokens = CreateSummaryCard("Total Tokens", "0", 0);
        _lblTotalCost = CreateSummaryCard("Est. Cost (USD)", "$0.00", 1);
        _lblTotalCalls = CreateSummaryCard("API Calls", "0", 2);
        _lblActiveSessions = CreateSummaryCard("Sessions", "0", 3);

        topPanel.Controls.AddRange(new Control[] { _lblTotalTokens.Parent!, _lblTotalCost.Parent!, _lblTotalCalls.Parent!, _lblActiveSessions.Parent! });

        // Toolbar
        var toolbar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 45,
            BackColor = Color.FromArgb(35, 35, 35),
            Padding = new Padding(10, 5, 10, 5)
        };

        _cmbDateRange = new ComboBox
        {
            Items = { "Today", "Last 7 Days", "Last 30 Days", "All Time" },
            SelectedIndex = 3,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 140,
            Location = new Point(10, 10),
            BackColor = Color.FromArgb(50, 50, 50),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        _cmbDateRange.SelectedIndexChanged += (_, _) => RefreshView();

        _btnRefresh = new Button
        {
            Text = "Refresh Now",
            Location = new Point(160, 8),
            Size = new Size(100, 28),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(88, 101, 242),
            ForeColor = Color.White
        };
        _btnRefresh.FlatAppearance.BorderSize = 0;
        _btnRefresh.Click += (_, _) => LoadData();

        _chkAutoStart = new CheckBox
        {
            Text = "Start with Windows",
            Location = new Point(280, 12),
            AutoSize = true,
            ForeColor = Color.White,
            Checked = StartupManager.IsStartupEnabled()
        };
        _chkAutoStart.CheckedChanged += (_, _) =>
        {
            if (_chkAutoStart.Checked)
                StartupManager.EnableStartup();
            else
                StartupManager.DisableStartup();
        };

        var lblInterval = new Label
        {
            Text = "Auto-refresh (sec):",
            Location = new Point(460, 12),
            AutoSize = true,
            ForeColor = Color.LightGray
        };

        _nudRefreshInterval = new NumericUpDown
        {
            Minimum = 5,
            Maximum = 3600,
            Value = 60,
            Location = new Point(590, 9),
            Width = 70,
            BackColor = Color.FromArgb(50, 50, 50),
            ForeColor = Color.White
        };
        _nudRefreshInterval.ValueChanged += (_, _) =>
        {
            _refreshTimer.Interval = (int)_nudRefreshInterval.Value * 1000;
        };

        _lblLastRefresh = new Label
        {
            Text = "Last refresh: --",
            Location = new Point(680, 12),
            AutoSize = true,
            ForeColor = Color.Gray
        };

        toolbar.Controls.AddRange(new Control[] { _cmbDateRange, _btnRefresh, _chkAutoStart, lblInterval, _nudRefreshInterval, _lblLastRefresh });

        // Tab control
        _tabControl = new TabControl
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(30, 30, 30),
            Font = new Font("Segoe UI", 10f)
        };

        // Dashboard tab
        var dashTab = new TabPage("Dashboard") { BackColor = Color.FromArgb(30, 30, 30) };
        BuildDashboardTab(dashTab);
        _tabControl.TabPages.Add(dashTab);

        // Sessions tab
        var sessionsTab = new TabPage("Sessions") { BackColor = Color.FromArgb(30, 30, 30) };
        BuildSessionsTab(sessionsTab);
        _tabControl.TabPages.Add(sessionsTab);

        // Detail tab
        var detailTab = new TabPage("Detailed Log") { BackColor = Color.FromArgb(30, 30, 30) };
        BuildDetailTab(detailTab);
        _tabControl.TabPages.Add(detailTab);

        Controls.Add(_tabControl);
        Controls.Add(toolbar);
        Controls.Add(topPanel);
    }

    private Label CreateSummaryCard(string title, string value, int index)
    {
        var card = new Panel
        {
            Size = new Size(220, 75),
            Location = new Point(15 + index * 235, 10),
            BackColor = Color.FromArgb(55, 55, 55),
            Padding = new Padding(12)
        };

        var lblTitle = new Label
        {
            Text = title,
            Dock = DockStyle.Top,
            Height = 22,
            ForeColor = Color.FromArgb(160, 160, 160),
            Font = new Font("Segoe UI", 9f)
        };

        var lblValue = new Label
        {
            Text = value,
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(88, 101, 242),
            Font = new Font("Segoe UI Semibold", 18f),
            TextAlign = ContentAlignment.MiddleLeft
        };

        card.Controls.Add(lblValue);
        card.Controls.Add(lblTitle);
        return lblValue;
    }

    private void BuildDashboardTab(TabPage tab)
    {
        // Top row: daily cost chart (50% height)
        _dailyCostChart = new CartesianChart
        {
            Dock = DockStyle.Top,
            Height = 300,
            BackColor = Color.FromArgb(30, 30, 30)
        };

        // Bottom panel for token chart + model pie side by side
        var bottomPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(30, 30, 30)
        };

        _tokenChart = new CartesianChart
        {
            Dock = DockStyle.Left,
            Width = 600,
            BackColor = Color.FromArgb(30, 30, 30)
        };

        _modelPieChart = new PieChart
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(30, 30, 30)
        };

        bottomPanel.Controls.Add(_modelPieChart);
        bottomPanel.Controls.Add(_tokenChart);

        tab.Controls.Add(bottomPanel);
        tab.Controls.Add(_dailyCostChart);

        // Resize token chart width proportionally
        tab.Resize += (_, _) =>
        {
            _dailyCostChart.Height = tab.Height / 2;
            _tokenChart.Width = (int)(tab.Width * 0.6);
        };
    }

    private void BuildSessionsTab(TabPage tab)
    {
        _sessionGrid = CreateStyledGrid();
        _sessionGrid.Dock = DockStyle.Fill;
        _sessionGrid.Columns.AddRange(
            new DataGridViewTextBoxColumn { HeaderText = "Project", Width = 200 },
            new DataGridViewTextBoxColumn { HeaderText = "Session ID", Width = 120 },
            new DataGridViewTextBoxColumn { HeaderText = "Started", Width = 150 },
            new DataGridViewTextBoxColumn { HeaderText = "Last Activity", Width = 150 },
            new DataGridViewTextBoxColumn { HeaderText = "Model", Width = 140 },
            new DataGridViewTextBoxColumn { HeaderText = "API Calls", Width = 80 },
            new DataGridViewTextBoxColumn { HeaderText = "Input Tokens", Width = 100, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight } },
            new DataGridViewTextBoxColumn { HeaderText = "Output Tokens", Width = 100, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight } },
            new DataGridViewTextBoxColumn { HeaderText = "Est. Cost", Width = 90, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight } }
        );
        tab.Controls.Add(_sessionGrid);
    }

    private void BuildDetailTab(TabPage tab)
    {
        _detailGrid = CreateStyledGrid();
        _detailGrid.Dock = DockStyle.Fill;
        _detailGrid.Columns.AddRange(
            new DataGridViewTextBoxColumn { HeaderText = "Timestamp", Width = 170 },
            new DataGridViewTextBoxColumn { HeaderText = "Model", Width = 160 },
            new DataGridViewTextBoxColumn { HeaderText = "Project", Width = 180 },
            new DataGridViewTextBoxColumn { HeaderText = "Input", Width = 90, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight } },
            new DataGridViewTextBoxColumn { HeaderText = "Output", Width = 90, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight } },
            new DataGridViewTextBoxColumn { HeaderText = "Cache Create", Width = 100, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight } },
            new DataGridViewTextBoxColumn { HeaderText = "Cache Read", Width = 100, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight } },
            new DataGridViewTextBoxColumn { HeaderText = "Total", Width = 100, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight } },
            new DataGridViewTextBoxColumn { HeaderText = "Est. Cost", Width = 90, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight } }
        );
        tab.Controls.Add(_detailGrid);
    }

    private static DataGridView CreateStyledGrid()
    {
        return new DataGridView
        {
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            RowHeadersVisible = false,
            BackgroundColor = Color.FromArgb(30, 30, 30),
            GridColor = Color.FromArgb(60, 60, 60),
            DefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.FromArgb(35, 35, 35),
                ForeColor = Color.White,
                SelectionBackColor = Color.FromArgb(88, 101, 242),
                SelectionForeColor = Color.White,
                Font = new Font("Segoe UI", 9f)
            },
            ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 9f)
            },
            EnableHeadersVisualStyles = false,
            AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None,
            RowTemplate = { Height = 28 }
        };
    }

    private static Icon CreateAppIcon()
    {
        using var bmp = new Bitmap(32, 32);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

        // Accent-colored rounded background
        using var bgBrush = new SolidBrush(Color.FromArgb(88, 101, 242));
        g.FillEllipse(bgBrush, 1, 1, 30, 30);

        // "C" letter in white
        using var font = new Font("Segoe UI", 18f, FontStyle.Bold, GraphicsUnit.Pixel);
        using var textBrush = new SolidBrush(Color.White);
        var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString("C", font, textBrush, new RectangleF(0, 0, 32, 32), sf);

        return Icon.FromHandle(bmp.GetHicon());
    }

    private void SetupTrayIcon()
    {
        var appIcon = CreateAppIcon();
        Icon = appIcon; // Also set the form title bar icon

        _trayIcon = new NotifyIcon
        {
            Text = "Claude Usage Monitor",
            Icon = appIcon,
            Visible = true
        };

        var trayMenu = new ContextMenuStrip();
        trayMenu.Items.Add("Open Dashboard", null, (_, _) => { Show(); WindowState = FormWindowState.Normal; Activate(); });
        trayMenu.Items.Add("Refresh Now", null, (_, _) => LoadData());
        trayMenu.Items.Add("-");
        trayMenu.Items.Add("Exit", null, (_, _) => { _trayIcon.Visible = false; Application.Exit(); });
        _trayIcon.ContextMenuStrip = trayMenu;
        _trayIcon.DoubleClick += (_, _) => { Show(); WindowState = FormWindowState.Normal; Activate(); };
    }

    private void SetupFileWatcher()
    {
        var projectsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "projects");

        if (!Directory.Exists(projectsDir)) return;

        _watcher = new FileSystemWatcher(projectsDir)
        {
            Filter = "*.jsonl",
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
            EnableRaisingEvents = true
        };

        _watcher.Changed += (_, _) =>
        {
            // Debounce: only refresh if last refresh was > 5 seconds ago
            if (DateTime.Now - _lastRefresh > TimeSpan.FromSeconds(5))
            {
                BeginInvoke(LoadData);
            }
        };
    }

    private DateTime _lastRefresh = DateTime.MinValue;

    private void SetupRefreshTimer()
    {
        _refreshTimer.Interval = 60000;
        _refreshTimer.Tick += (_, _) => LoadData();
        _refreshTimer.Start();
    }

    private void LoadData()
    {
        try
        {
            _btnRefresh.Enabled = false;
            _btnRefresh.Text = "Loading...";
            Application.DoEvents();

            _allRecords = _parser.ParseAllSessions();
            _lastRefresh = DateTime.Now;
            RefreshView();

            _lblLastRefresh.Text = $"Last refresh: {DateTime.Now:HH:mm:ss}";
        }
        catch (Exception ex)
        {
            _lblLastRefresh.Text = $"Error: {ex.Message}";
        }
        finally
        {
            _btnRefresh.Enabled = true;
            _btnRefresh.Text = "Refresh Now";
        }
    }

    private void RefreshView()
    {
        var filtered = FilterByDateRange(_allRecords);
        UpdateSummaryCards(filtered);
        UpdateDailyCostChart(filtered);
        UpdateTokenChart(filtered);
        UpdateModelPieChart(filtered);
        UpdateSessionGrid(filtered);
        UpdateDetailGrid(filtered);
    }

    private List<UsageRecord> FilterByDateRange(List<UsageRecord> records)
    {
        var cutoff = _cmbDateRange.SelectedIndex switch
        {
            0 => DateTime.Today,
            1 => DateTime.Today.AddDays(-7),
            2 => DateTime.Today.AddDays(-30),
            _ => DateTime.MinValue
        };
        return records.Where(r => r.Timestamp >= cutoff).ToList();
    }

    private void UpdateSummaryCards(List<UsageRecord> records)
    {
        var totalTokens = records.Sum(r => (long)r.TotalTokens);
        var totalCost = records.Sum(r => r.EstimatedCostUsd);
        var totalCalls = records.Count;
        var sessions = records.Select(r => r.SessionId).Distinct().Count();

        _lblTotalTokens.Text = FormatNumber(totalTokens);
        _lblTotalCost.Text = $"${totalCost:F2}";
        _lblTotalCalls.Text = FormatNumber(totalCalls);
        _lblActiveSessions.Text = sessions.ToString();

        // Update tray icon tooltip
        _trayIcon.Text = $"Claude: {FormatNumber(totalTokens)} tokens | ${totalCost:F2}";
    }

    private void UpdateDailyCostChart(List<UsageRecord> records)
    {
        var daily = _parser.GetDailyUsage(records);

        _dailyCostChart.Series = new ISeries[]
        {
            new ColumnSeries<decimal>
            {
                Values = daily.Select(d => d.TotalCostUsd).ToArray(),
                Name = "Daily Cost (USD)",
                Fill = new SolidColorPaint(new SKColor(88, 101, 242)),
                MaxBarWidth = 30
            }
        };

        _dailyCostChart.XAxes = new Axis[]
        {
            new Axis
            {
                Labels = daily.Select(d => d.Date.ToString("MM/dd")).ToArray(),
                LabelsRotation = 45,
                LabelsPaint = new SolidColorPaint(new SKColor(180, 180, 180)),
                Name = "Date",
                NamePaint = new SolidColorPaint(new SKColor(180, 180, 180))
            }
        };

        _dailyCostChart.YAxes = new Axis[]
        {
            new Axis
            {
                Name = "Cost (USD)",
                NamePaint = new SolidColorPaint(new SKColor(180, 180, 180)),
                LabelsPaint = new SolidColorPaint(new SKColor(180, 180, 180)),
                Labeler = v => $"${v:F2}"
            }
        };

        _dailyCostChart.Title = new DrawnLabelVisual(new LabelGeometry
        {
            Text = "Daily Cost",
            TextSize = 16,
            Paint = new SolidColorPaint(new SKColor(220, 220, 220)),
            HorizontalAlign = Align.Start,
            VerticalAlign = Align.Start
        });
    }

    private void UpdateTokenChart(List<UsageRecord> records)
    {
        var daily = _parser.GetDailyUsage(records);

        var dailyInput = records
            .GroupBy(r => DateOnly.FromDateTime(r.Timestamp))
            .OrderBy(g => g.Key)
            .Select(g => (double)g.Sum(r => r.InputTokens + r.CacheReadTokens))
            .ToArray();

        var dailyOutput = records
            .GroupBy(r => DateOnly.FromDateTime(r.Timestamp))
            .OrderBy(g => g.Key)
            .Select(g => (double)g.Sum(r => r.OutputTokens))
            .ToArray();

        _tokenChart.Series = new ISeries[]
        {
            new StackedColumnSeries<double>
            {
                Values = dailyInput,
                Name = "Input + Cache Read",
                Fill = new SolidColorPaint(new SKColor(59, 130, 246)),
                MaxBarWidth = 25
            },
            new StackedColumnSeries<double>
            {
                Values = dailyOutput,
                Name = "Output",
                Fill = new SolidColorPaint(new SKColor(168, 85, 247)),
                MaxBarWidth = 25
            }
        };

        _tokenChart.XAxes = new Axis[]
        {
            new Axis
            {
                Labels = daily.Select(d => d.Date.ToString("MM/dd")).ToArray(),
                LabelsRotation = 45,
                LabelsPaint = new SolidColorPaint(new SKColor(180, 180, 180))
            }
        };

        _tokenChart.YAxes = new Axis[]
        {
            new Axis
            {
                Name = "Tokens",
                NamePaint = new SolidColorPaint(new SKColor(180, 180, 180)),
                LabelsPaint = new SolidColorPaint(new SKColor(180, 180, 180)),
                Labeler = v => FormatNumber((long)v)
            }
        };

        _tokenChart.Title = new DrawnLabelVisual(new LabelGeometry
        {
            Text = "Daily Token Usage",
            TextSize = 16,
            Paint = new SolidColorPaint(new SKColor(220, 220, 220)),
            HorizontalAlign = Align.Start,
            VerticalAlign = Align.Start
        });
    }

    private void UpdateModelPieChart(List<UsageRecord> records)
    {
        var modelGroups = records
            .GroupBy(r => r.Model)
            .Select(g => new { Model = FormatModelName(g.Key), Cost = g.Sum(r => r.EstimatedCostUsd) })
            .OrderByDescending(g => g.Cost)
            .ToList();

        var colors = new SKColor[]
        {
            new(88, 101, 242),   // Purple
            new(59, 130, 246),   // Blue
            new(168, 85, 247),   // Violet
            new(236, 72, 153),   // Pink
            new(34, 197, 94),    // Green
        };

        _modelPieChart.Series = modelGroups.Select((g, i) =>
            new PieSeries<decimal>
            {
                Values = new[] { g.Cost },
                Name = $"{g.Model} (${g.Cost:F2})",
                Fill = new SolidColorPaint(colors[i % colors.Length]),
                DataLabelsSize = 12,
                DataLabelsPaint = new SolidColorPaint(new SKColor(220, 220, 220))
            } as ISeries).ToArray();

        _modelPieChart.Title = new DrawnLabelVisual(new LabelGeometry
        {
            Text = "Cost by Model",
            TextSize = 16,
            Paint = new SolidColorPaint(new SKColor(220, 220, 220)),
            HorizontalAlign = Align.Start,
            VerticalAlign = Align.Start
        });
    }

    private void UpdateSessionGrid(List<UsageRecord> records)
    {
        var summaries = _parser.GetSessionSummaries(records);
        _sessionGrid.Rows.Clear();
        foreach (var s in summaries)
        {
            _sessionGrid.Rows.Add(
                s.Project,
                s.SessionId[..Math.Min(8, s.SessionId.Length)] + "...",
                s.StartTime.ToString("yyyy-MM-dd HH:mm"),
                s.LastActivity.ToString("yyyy-MM-dd HH:mm"),
                FormatModelName(s.PrimaryModel),
                s.ApiCalls,
                FormatNumber(s.TotalInputTokens),
                FormatNumber(s.TotalOutputTokens),
                $"${s.TotalCostUsd:F4}"
            );
        }
    }

    private void UpdateDetailGrid(List<UsageRecord> records)
    {
        _detailGrid.Rows.Clear();
        foreach (var r in records.OrderByDescending(r => r.Timestamp).Take(500))
        {
            _detailGrid.Rows.Add(
                r.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
                FormatModelName(r.Model),
                r.Project,
                FormatNumber(r.InputTokens),
                FormatNumber(r.OutputTokens),
                FormatNumber(r.CacheCreationTokens),
                FormatNumber(r.CacheReadTokens),
                FormatNumber(r.TotalTokens),
                $"${r.EstimatedCostUsd:F4}"
            );
        }
    }

    private static string FormatNumber(long n) => n switch
    {
        >= 1_000_000 => $"{n / 1_000_000.0:F1}M",
        >= 1_000 => $"{n / 1_000.0:F1}K",
        _ => n.ToString("N0")
    };

    private static string FormatModelName(string model) => model switch
    {
        var m when m.Contains("opus") => "Opus 4.6",
        var m when m.Contains("sonnet") => "Sonnet 4.6",
        var m when m.Contains("haiku") => "Haiku 4.5",
        _ => model
    };

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
            _trayIcon.ShowBalloonTip(2000, "Claude Usage Monitor",
                "Running in background. Double-click tray icon to open.", ToolTipIcon.Info);
        }
        else
        {
            _trayIcon.Visible = false;
            _watcher?.Dispose();
            _refreshTimer.Stop();
        }
        base.OnFormClosing(e);
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        // If started with --minimized flag, hide to tray
        if (Environment.GetCommandLineArgs().Contains("--minimized"))
        {
            WindowState = FormWindowState.Minimized;
            Hide();
        }
    }
}
