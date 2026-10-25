using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using LiteMon.Core.Diagnostics;
using LiteMon.Core.Formatting;
using LiteMon.Core.Metrics;
using LiteMon.Core.Sensors;
using LiteMon.Core.Snapshots;
using LiteMon.Sensors;

namespace LiteMon.App;

internal enum RowKind
{
    Percent,
    Temp,
    Mhz,
    Memory,
    Rate,
    Uptime,
}

internal sealed record RowSpec(MetricId Id, string Label, RowKind Kind);

internal sealed record GroupSpec(string Title, string Model, RowSpec[] Rows);

/// <summary>自绘面板：按硬件分组，标题带型号，12 行指标。</summary>
internal sealed class PanelView : FrameworkElement
{
    private static readonly Brush Background = Frozen(Color.FromArgb(0xFF, 0x0B, 0x0E, 0x14));
    private static readonly Brush Border = Frozen(Color.FromArgb(0x26, 0xFF, 0xFF, 0xFF));
    private static readonly Brush LabelBrush = Frozen(Color.FromRgb(0xA9, 0xB4, 0xC0));
    private static readonly Brush ValueBrush = Frozen(Color.FromRgb(0xF5, 0xF7, 0xFA));
    private static readonly Brush MutedBrush = Frozen(Color.FromRgb(0x6B, 0x76, 0x84));
    private static readonly Brush GroupBrush = Frozen(Color.FromRgb(0x7F, 0xB2, 0xE5));
    private static readonly Brush ModelBrush = Frozen(Color.FromRgb(0x5F, 0x6C, 0x7A));
    private static readonly Brush TrackBrush = Frozen(Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF));
    private static readonly Brush LowBrush = Frozen(Color.FromRgb(0x4A, 0xDE, 0x80));
    private static readonly Brush MidBrush = Frozen(Color.FromRgb(0xFB, 0xBF, 0x24));
    private static readonly Brush HighBrush = Frozen(Color.FromRgb(0xF8, 0x71, 0x71));
    private static readonly Pen BorderPen = CreatePen(Border);
    private static readonly Pen ClosePen = CreatePen(new SolidColorBrush(Color.FromRgb(0xA9, 0xB4, 0xC0)), 1.2);
    private static readonly bool NoAnimation = Environment.GetEnvironmentVariable("LITEMON_NO_ANIMATION") == "1";

    private const double AnimationMs = 300;

    private readonly SnapshotBus _bus;
    private GroupSpec[] _groups;
    private string _headerText = RefreshInterval.Describe(RefreshInterval.DefaultSeconds);
    private readonly Dictionary<(string Text, double Size, bool Bold), FormattedText> _textCache = new();
    private readonly Dictionary<MetricId, double> _display = new();
    private readonly Dictionary<MetricId, double> _animFrom = new();
    private readonly DispatcherTimer _animTimer;

    private Snapshot _snapshot = Snapshot.Empty;
    private long _animStartTicks;

    public PanelView(SnapshotBus bus, DeviceInfo devices)
    {
        _bus = bus;
        _groups = BuildGroups(devices);
        _bus.Updated += OnSnapshotUpdated;
        Unloaded += (_, _) => _bus.Updated -= OnSnapshotUpdated;

        // 30 fps 定时器驱动动画；CompositionTarget.Rendering 在部分机器上退订后仍触发
        _animTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(33),
        };
        _animTimer.Tick += OnFrame;
    }

    public void SetDevices(DeviceInfo devices)
    {
        _groups = BuildGroups(devices);
        InvalidateVisual();
    }

    /// <summary>更新头部"每 N 秒刷新"文案；值有变化才重绘。</summary>
    public void SetRefreshInterval(double seconds)
    {
        string text = RefreshInterval.Describe(seconds);
        if (string.Equals(text, _headerText, StringComparison.Ordinal))
        {
            return;
        }

        _headerText = text;
        InvalidateVisual();
    }

    private static GroupSpec[] BuildGroups(DeviceInfo devices) =>
    [
        new("CPU", devices.CpuName,
        [
            new(MetricId.CpuLoad, "使用率", RowKind.Percent),
            new(MetricId.CpuTemp, "温度", RowKind.Temp),
            new(MetricId.CpuClockMhz, "频率", RowKind.Mhz),
        ]),
        new("GPU", devices.GpuName,
        [
            new(MetricId.GpuLoad, "使用率", RowKind.Percent),
            new(MetricId.GpuTemp, "温度", RowKind.Temp),
            new(MetricId.GpuMemUsedMb, "显存", RowKind.Memory),
        ]),
        new("内存", devices.MemoryText,
        [
            new(MetricId.MemUsedPercent, "使用率", RowKind.Percent),
        ]),
        new("磁盘", devices.DiskName,
        [
            new(MetricId.DiskReadBps, "读取", RowKind.Rate),
            new(MetricId.DiskWriteBps, "写入", RowKind.Rate),
        ]),
        new("网络", devices.NicName,
        [
            new(MetricId.NetDownBps, "下行", RowKind.Rate),
            new(MetricId.NetUpBps, "上行", RowKind.Rate),
        ]),
        new("系统", string.Empty,
        [
            new(MetricId.UptimeSeconds, "运行时长", RowKind.Uptime),
        ]),
    ];

    private IEnumerable<RowSpec> AllRows => _groups.SelectMany(g => g.Rows);

    private static SolidColorBrush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static Pen CreatePen(Brush brush, double thickness = 1)
    {
        var pen = new Pen(brush, thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        pen.Freeze();
        return pen;
    }

    private void OnSnapshotUpdated(object? sender, Snapshot snapshot)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() => OnSnapshotUpdated(sender, snapshot)));
            return;
        }

        PerfCounters.AddSnapshot();
        _snapshot = snapshot;

        if (NoAnimation)
        {
            foreach (var row in AllRows)
            {
                var sample = snapshot.Get(row.Id);
                _display[row.Id] = sample.Status == MetricStatus.Ok ? sample.Value : 0;
            }

            InvalidateVisual();
            return;
        }

        foreach (var row in AllRows)
        {
            _animFrom[row.Id] = _display.GetValueOrDefault(row.Id);
        }

        _animStartTicks = Stopwatch.GetTimestamp();

        if (!_animTimer.IsEnabled)
        {
            _animTimer.Start();
        }
    }

    private void OnFrame(object? sender, EventArgs e)
    {
        PerfCounters.AddAnimationFrame();

        double elapsedMs = (Stopwatch.GetTimestamp() - _animStartTicks) * 1000.0 / Stopwatch.Frequency;
        double progress = Math.Clamp(elapsedMs / AnimationMs, 0, 1);
        double eased = 1 - Math.Pow(1 - progress, 3);

        foreach (var row in AllRows)
        {
            var sample = _snapshot.Get(row.Id);
            double target = sample.Status == MetricStatus.Ok ? sample.Value : 0;
            double from = _animFrom.GetValueOrDefault(row.Id);
            _display[row.Id] = progress >= 1 ? target : from + (target - from) * eased;
        }

        if (progress >= 1)
        {
            _animTimer.Stop();
        }

        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize)
        => new(
            double.IsInfinity(availableSize.Width) ? 340 : availableSize.Width,
            double.IsInfinity(availableSize.Height) ? 560 : availableSize.Height);

    protected override void OnRender(DrawingContext dc)
    {
        long start = Stopwatch.GetTimestamp();
        double width = ActualWidth;
        double height = ActualHeight;

        dc.DrawRectangle(Background, BorderPen, new Rect(0.5, 0.5, width - 1, height - 1));

        dc.DrawText(GetText("LiteMon", 15, true, ValueBrush), new Point(16, 11));
        dc.DrawText(GetText(_headerText, 10, false, MutedBrush, width - 88 - 40), new Point(88, 16));
        dc.DrawLine(ClosePen, new Point(width - 28, 15), new Point(width - 18, 25));
        dc.DrawLine(ClosePen, new Point(width - 18, 15), new Point(width - 28, 25));

        const double header = 42;
        const double footer = 24;
        const double groupHeader = 17;

        int rowCount = _groups.Sum(g => g.Rows.Length);
        double rowHeight = (height - header - footer - (_groups.Length * groupHeader)) / rowCount;

        double y = header;

        foreach (var group in _groups)
        {
            var title = GetText(group.Title, 9, true, GroupBrush);
            dc.DrawText(title, new Point(16, y));

            if (group.Model.Length > 0)
            {
                double maxWidth = width - 32 - title.Width - 8;
                var model = GetText(group.Model, 9, false, ModelBrush, maxWidth);
                dc.DrawText(model, new Point(16 + title.Width + 8, y));
            }

            y += groupHeader;

            foreach (var row in group.Rows)
            {
                DrawRow(dc, row, y, rowHeight, width);
                y += rowHeight;
            }
        }

        dc.DrawText(GetText(FooterText(), 10, false, MutedBrush), new Point(16, height - 19));
        PerfCounters.AddRender(Stopwatch.GetTimestamp() - start);
    }

    private string FooterText()
    {
        var source = _snapshot.Get(MetricId.CpuTempSource);
        if (source.Status != MetricStatus.Ok)
        {
            return "温度不可用 · 右键菜单";
        }

        return source.Value >= 1.5
            ? "温度：ACPI 热区 · 右键启用内核温度"
            : "温度：LHM 内核 · 右键菜单";
    }

    private void DrawRow(DrawingContext dc, RowSpec row, double y, double rowHeight, double width)
    {
        var sample = _snapshot.Get(row.Id);
        bool valid = sample.Status is MetricStatus.Ok or MetricStatus.Stale;
        string text = valid ? Format(row, sample.Value) : "--";

        var label = GetText(row.Label, 12, false, valid ? LabelBrush : MutedBrush);
        dc.DrawText(label, new Point(16, y + 1));

        var value = GetText(text, 14, true, valid ? ValueBrush : MutedBrush);
        dc.DrawText(value, new Point(width - 16 - value.Width, y));

        double ratio = valid ? Ratio(row) : 0;
        var track = new Rect(16, y + rowHeight - 8, width - 32, 3);
        dc.DrawRectangle(TrackBrush, null, track);

        if (ratio > 0.01)
        {
            var fill = ratio > 0.8 ? HighBrush : ratio > 0.6 ? MidBrush : LowBrush;
            dc.DrawRectangle(fill, null, new Rect(track.X, track.Y, track.Width * Math.Clamp(ratio, 0, 1), track.Height));
        }
    }

    private double Ratio(RowSpec row)
    {
        double value = _display.GetValueOrDefault(row.Id);

        return row.Kind switch
        {
            RowKind.Percent => value / 100,
            RowKind.Temp => value / 100,
            RowKind.Mhz => value / 6000,
            RowKind.Memory => _snapshot.Get(MetricId.GpuMemTotalMb).Value > 0
                ? value / _snapshot.Get(MetricId.GpuMemTotalMb).Value
                : 0,
            RowKind.Rate => Math.Log10(1 + Math.Max(0, value)) / Math.Log10(1 + (100.0 * 1024 * 1024)),
            _ => 0,
        };
    }

    private static string Format(RowSpec row, double value) => row.Kind switch
    {
        RowKind.Percent => MetricFormatter.Percent(value),
        RowKind.Temp => MetricFormatter.Celsius(value),
        RowKind.Mhz => MetricFormatter.Megahertz(value),
        RowKind.Memory => MetricFormatter.Megabytes(value),
        RowKind.Rate => MetricFormatter.Rate(value),
        RowKind.Uptime => MetricFormatter.Uptime(value),
        _ => MetricFormatter.Format(row.Id, value),
    };

    private FormattedText GetText(string text, double size, bool bold, Brush brush, double maxWidth = 0)
    {
        if (_textCache.Count > 256)
        {
            _textCache.Clear();
        }

        var key = (text, size, bold);
        if (_textCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var formatted = new FormattedText(
            text,
            System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Microsoft YaHei UI"), FontStyles.Normal,
                bold ? FontWeights.SemiBold : FontWeights.Normal, FontStretches.Normal),
            size,
            brush,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);

        if (maxWidth > 0)
        {
            formatted.MaxTextWidth = maxWidth;
            formatted.Trimming = TextTrimming.CharacterEllipsis;
        }

        _textCache[key] = formatted;
        return formatted;
    }
}
