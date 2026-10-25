using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using LiteMon.Core.Appearance;
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

internal sealed record GroupSpec(PanelGroup Group, string Title, string Model, RowSpec[] Rows);

/// <summary>
/// 自绘面板：支持竖版详细 / 横版紧凑、透明模式描边文字、进度条开关、部件勾选。
/// 默认关闭数值动画：数据到达时只画一帧，画面不变则完全不重绘。
/// </summary>
internal sealed class PanelView : FrameworkElement
{
    private const double AnimationMs = 300;

    private static readonly Brush LabelBrush = Frozen(Color.FromRgb(0xA9, 0xB4, 0xC0));
    private static readonly Brush ValueBrush = Frozen(Color.FromRgb(0xF5, 0xF7, 0xFA));
    private static readonly Brush MutedBrush = Frozen(Color.FromRgb(0x6B, 0x76, 0x84));
    private static readonly Brush GroupBrush = Frozen(Color.FromRgb(0x7F, 0xB2, 0xE5));
    private static readonly Brush ModelBrush = Frozen(Color.FromRgb(0x5F, 0x6C, 0x7A));
    private static readonly Brush TrackBrush = Frozen(Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF));
    private static readonly Brush LowBrush = Frozen(Color.FromRgb(0x4A, 0xDE, 0x80));
    private static readonly Brush MidBrush = Frozen(Color.FromRgb(0xFB, 0xBF, 0x24));
    private static readonly Brush HighBrush = Frozen(Color.FromRgb(0xF8, 0x71, 0x71));
    private static readonly Brush PinBrush = Frozen(Color.FromRgb(0xA9, 0xB4, 0xC0));
    private static readonly Brush PinActiveBrush = Frozen(Color.FromRgb(0x7F, 0xB2, 0xE5));
    private static readonly Brush CloseBrush = Frozen(Color.FromRgb(0xA9, 0xB4, 0xC0));
    private static readonly Pen BorderPen = CreatePen(Frozen(Color.FromArgb(0x26, 0xFF, 0xFF, 0xFF)));
    private static readonly Pen PinnedBorderPen = CreatePen(Frozen(Color.FromArgb(0x66, 0x7F, 0xB2, 0xE5)));
    private static readonly Pen TransparentBorderPen = CreatePen(Frozen(Color.FromArgb(0x80, 0x00, 0x00, 0x00)));
    private static readonly Pen ClosePen = CreatePen(CloseBrush, 1.2);
    private static readonly Pen PinPen = CreatePen(PinBrush, 1.2);
    private static readonly Pen PinActivePen = CreatePen(PinActiveBrush, 1.2);
    private static readonly Brush ShadowBrush = Frozen(Color.FromArgb(0xF2, 0x00, 0x00, 0x00));
    private static readonly Brush ShadowBodyBrush = Brushes.White;
    private static readonly bool NoAnimation = Environment.GetEnvironmentVariable("LITEMON_NO_ANIMATION") == "1";

    private readonly SnapshotBus _bus;
    private readonly Dictionary<(string Text, double Size, bool Bold, Brush Brush), FormattedText> _textCache = new();
    private readonly Dictionary<MetricId, double> _display = new();
    private readonly Dictionary<MetricId, double> _animFrom = new();
    private readonly DispatcherTimer _animTimer;

    private GroupSpec[] _groups;
    private Snapshot _snapshot = Snapshot.Empty;
    private AppearanceSettings _appearance = new();
    private Brush _background = Frozen(Color.FromRgb(0x0B, 0x0E, 0x14));
    private string _headerText = RefreshInterval.Describe(RefreshInterval.DefaultSeconds);
    private string _lastFingerprint = string.Empty;
    private bool _pinned;
    private long _animStartTicks;

    public PanelView(SnapshotBus bus, DeviceInfo devices)
    {
        _bus = bus;
        _groups = BuildGroups(devices);
        _bus.Updated += OnSnapshotUpdated;
        Unloaded += (_, _) => _bus.Updated -= OnSnapshotUpdated;

        _animTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(33),
        };
        _animTimer.Tick += OnFrame;
    }

    public void SetDevices(DeviceInfo devices)
    {
        _groups = BuildGroups(devices);
        InvalidateContent();
    }

    public void SetRefreshInterval(double seconds)
    {
        string text = RefreshInterval.Describe(seconds);
        if (string.Equals(text, _headerText, StringComparison.Ordinal))
        {
            return;
        }

        _headerText = text;
        InvalidateContent();
    }

    public void SetPinned(bool pinned)
    {
        if (_pinned == pinned)
        {
            return;
        }

        _pinned = pinned;
        InvalidateContent();
    }

    public void SetAppearance(AppearanceSettings appearance)
    {
        AppearanceSettings normalized = appearance.Normalized();
        if (_appearance == normalized)
        {
            return;
        }

        bool transparencyChanged = _appearance.IsTransparent != normalized.IsTransparent;
        _appearance = normalized;

        if (transparencyChanged)
        {
            _textCache.Clear();   // 描边/非描边使用不同画刷，清缓存避免混用
        }

        _background = CreateBackground(normalized.OpacityPercent);

        if (!normalized.SmoothAnimation)
        {
            _animTimer.Stop();
            SnapToTargets();
        }

        InvalidateContent();
    }

    private static SolidColorBrush CreateBackground(int opacityPercent)
    {
        byte alpha = (byte)Math.Round(opacityPercent * 255 / 100.0, MidpointRounding.AwayFromZero);
        return Frozen(Color.FromArgb(alpha, 0x0B, 0x0E, 0x14));
    }

    private void InvalidateContent()
    {
        _lastFingerprint = string.Empty;
        InvalidateVisual();
    }

    private static GroupSpec[] BuildGroups(DeviceInfo devices) =>
    [
        new(PanelGroup.Cpu, "CPU", devices.CpuName,
        [
            new(MetricId.CpuLoad, "使用率", RowKind.Percent),
            new(MetricId.CpuTemp, "温度", RowKind.Temp),
            new(MetricId.CpuClockMhz, "频率", RowKind.Mhz),
        ]),
        new(PanelGroup.Gpu, "GPU", devices.GpuName,
        [
            new(MetricId.GpuLoad, "使用率", RowKind.Percent),
            new(MetricId.GpuTemp, "温度", RowKind.Temp),
            new(MetricId.GpuMemUsedMb, "显存", RowKind.Memory),
        ]),
        new(PanelGroup.Memory, "内存", devices.MemoryText,
        [
            new(MetricId.MemUsedPercent, "使用率", RowKind.Percent),
        ]),
        new(PanelGroup.Disk, "磁盘", devices.DiskName,
        [
            new(MetricId.DiskReadBps, "读取", RowKind.Rate),
            new(MetricId.DiskWriteBps, "写入", RowKind.Rate),
        ]),
        new(PanelGroup.Network, "网络", devices.NicName,
        [
            new(MetricId.NetDownBps, "下行", RowKind.Rate),
            new(MetricId.NetUpBps, "上行", RowKind.Rate),
        ]),
        new(PanelGroup.System, "系统", string.Empty,
        [
            new(MetricId.UptimeSeconds, "运行时长", RowKind.Uptime),
        ]),
    ];

    private IEnumerable<RowSpec> VisibleRows =>
        _groups.Where(group => _appearance.IsVisible(group.Group)).SelectMany(group => group.Rows);

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

        bool animate = _appearance.SmoothAnimation
            && _appearance.Layout == PanelLayout.Detailed
            && !NoAnimation;

        if (!animate)
        {
            SnapToTargets();
            if (ContentUnchanged())
            {
                return;
            }

            InvalidateVisual();
            return;
        }

        foreach (var row in VisibleRows)
        {
            _animFrom[row.Id] = _display.GetValueOrDefault(row.Id);
        }

        _animStartTicks = Stopwatch.GetTimestamp();

        if (!_animTimer.IsEnabled)
        {
            _animTimer.Start();
        }
    }

    private void SnapToTargets()
    {
        foreach (var row in VisibleRows)
        {
            var sample = _snapshot.Get(row.Id);
            _display[row.Id] = sample.Status == MetricStatus.Ok ? sample.Value : 0;
        }
    }

    /// <summary>待绘制文本与上一帧完全一致时返回 true（省掉一次重绘）。</summary>
    private bool ContentUnchanged()
    {
        var builder = new StringBuilder(256);
        builder.Append(_headerText);

        foreach (var row in VisibleRows)
        {
            builder.Append(row.Label).Append('=').Append(RowText(row)).Append(';');
        }

        builder.Append(FooterText());
        string fingerprint = builder.ToString();

        if (string.Equals(fingerprint, _lastFingerprint, StringComparison.Ordinal))
        {
            return true;
        }

        _lastFingerprint = fingerprint;
        return false;
    }

    private void OnFrame(object? sender, EventArgs e)
    {
        PerfCounters.AddAnimationFrame();

        double elapsedMs = (Stopwatch.GetTimestamp() - _animStartTicks) * 1000.0 / Stopwatch.Frequency;
        double progress = Math.Clamp(elapsedMs / AnimationMs, 0, 1);
        double eased = 1 - Math.Pow(1 - progress, 3);

        foreach (var row in VisibleRows)
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

        _lastFingerprint = string.Empty;
        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        (double width, double height) = PanelMetrics.GetSize(_appearance.Layout, _appearance.VisibleGroups);
        return new(
            double.IsInfinity(availableSize.Width) ? width : availableSize.Width,
            double.IsInfinity(availableSize.Height) ? height : availableSize.Height);
    }

    protected override void OnRender(DrawingContext dc)
    {
        long start = Stopwatch.GetTimestamp();
        double width = ActualWidth;
        double height = ActualHeight;

        Pen border = _pinned ? PinnedBorderPen : _appearance.IsTransparent ? TransparentBorderPen : BorderPen;
        dc.DrawRectangle(_background, border, new Rect(0.5, 0.5, width - 1, height - 1));

        if (_appearance.Layout == PanelLayout.Compact)
        {
            DrawCompact(dc, width, height);
        }
        else
        {
            DrawDetailed(dc, width, height);
        }

        PerfCounters.AddRender(Stopwatch.GetTimestamp() - start);
    }

    private void DrawDetailed(DrawingContext dc, double width, double height)
    {
        DrawHeader(dc, width);

        double y = PanelMetrics.DetailHeader;
        bool first = true;

        foreach (var group in _groups)
        {
            if (!_appearance.IsVisible(group.Group))
            {
                continue;
            }

            if (!first)
            {
                y += 2;
            }

            first = false;
            DrawText(dc, group.Title, 9, true, GroupBrush, new Point(16, y));

            if (group.Model.Length > 0)
            {
                double maxWidth = width - 32 - MeasureText(group.Title, 9, true) - 8;
                DrawText(dc, group.Model, 9, false, ModelBrush, new Point(16 + MeasureText(group.Title, 9, true) + 8, y), maxWidth);
            }

            y += PanelMetrics.DetailGroupHeader;

            foreach (var row in group.Rows)
            {
                DrawRow(dc, row, y, width);
                y += PanelMetrics.DetailRowHeight;
            }
        }

        DrawText(dc, FooterText(), 10, false, MutedBrush, new Point(16, height - 19));
    }

    private void DrawHeader(DrawingContext dc, double width)
    {
        DrawText(dc, "LiteMon", 15, true, ValueBrush, new Point(16, 11));
        DrawText(dc, _headerText, 10, false, MutedBrush, new Point(88, 16), width - 88 - 40);
        DrawPinAndClose(dc, width, 20);
    }

    private void DrawPinAndClose(DrawingContext dc, double width, double centerY)
    {
        double pinX = width - 58;
        dc.DrawEllipse(_pinned ? PinActiveBrush : null, _pinned ? PinActivePen : PinPen, new Point(pinX, centerY - 3.5), 3, 3);
        dc.DrawLine(_pinned ? PinActivePen : PinPen, new Point(pinX, centerY - 0.5), new Point(pinX, centerY + 6));

        dc.DrawLine(ClosePen, new Point(width - 28, centerY - 5), new Point(width - 18, centerY + 5));
        dc.DrawLine(ClosePen, new Point(width - 18, centerY - 5), new Point(width - 28, centerY + 5));
    }

    private void DrawCompact(DrawingContext dc, double width, double height)
    {
        double x = PanelMetrics.CompactPadding;
        double centerY = height / 2;

        foreach (var group in PanelMetrics.GroupOrder)
        {
            if (!_appearance.IsVisible(group))
            {
                continue;
            }

            DrawText(dc, PanelMetrics.Label(group), 10, false, MutedBrush, new Point(x, centerY - 14));
            DrawText(dc, CompactValue(group), 13, true, ValueBrush, new Point(x, centerY - 5));
            x += PanelMetrics.CellWidth(group);
        }

        DrawPinAndClose(dc, width, centerY);
    }

    private void DrawRow(DrawingContext dc, RowSpec row, double y, double width)
    {
        var sample = _snapshot.Get(row.Id);
        bool valid = sample.Status is MetricStatus.Ok or MetricStatus.Stale;
        string text = valid ? Format(row, sample.Value) : "--";

        DrawText(dc, row.Label, 12, false, valid ? LabelBrush : MutedBrush, new Point(16, y + 5));

        double valueWidth = MeasureText(text, 14, true);
        DrawText(dc, text, 14, true, valid ? ValueBrush : MutedBrush, new Point(width - 16 - valueWidth, y + 3));

        if (!_appearance.ShowBars)
        {
            return;
        }

        double ratio = valid ? Ratio(row) : 0;
        var track = new Rect(16, y + PanelMetrics.DetailRowHeight - 8, width - 32, 3);
        dc.DrawRectangle(TrackBrush, null, track);

        if (ratio > 0.01)
        {
            var fill = ratio > 0.8 ? HighBrush : ratio > 0.6 ? MidBrush : LowBrush;
            dc.DrawRectangle(fill, null, new Rect(track.X, track.Y, track.Width * Math.Clamp(ratio, 0, 1), track.Height));
        }
    }

    private string CompactValue(PanelGroup group) => group switch
    {
        PanelGroup.Cpu => Pair(MetricId.CpuLoad, RowKind.Percent, MetricId.CpuTemp, RowKind.Temp),
        PanelGroup.Gpu => Pair(MetricId.GpuLoad, RowKind.Percent, MetricId.GpuTemp, RowKind.Temp),
        PanelGroup.Memory => Single(MetricId.MemUsedPercent, RowKind.Percent),
        PanelGroup.Disk => Single(MetricId.DiskReadBps, RowKind.Rate),
        PanelGroup.Network => Single(MetricId.NetDownBps, RowKind.Rate),
        PanelGroup.System => Single(MetricId.UptimeSeconds, RowKind.Uptime),
        _ => "--",
    };

    private string Pair(MetricId first, RowKind firstKind, MetricId second, RowKind secondKind)
    {
        string left = Single(first, firstKind);
        if (left == "--")
        {
            return left;
        }

        string right = Single(second, secondKind);
        return right == "--" ? left : left + " " + right;
    }

    private string Single(MetricId id, RowKind kind)
    {
        var sample = _snapshot.Get(id);
        if (sample.Status is not (MetricStatus.Ok or MetricStatus.Stale))
        {
            return "--";
        }

        return kind switch
        {
            RowKind.Percent => MetricFormatter.Percent(sample.Value),
            RowKind.Temp => MetricFormatter.Celsius(sample.Value),
            RowKind.Mhz => MetricFormatter.Megahertz(sample.Value),
            RowKind.Memory => MetricFormatter.Megabytes(sample.Value),
            RowKind.Rate => MetricFormatter.Rate(sample.Value),
            RowKind.Uptime => MetricFormatter.Uptime(sample.Value),
            _ => "--",
        };
    }

    private string RowText(RowSpec row)
    {
        var sample = _snapshot.Get(row.Id);
        bool valid = sample.Status is MetricStatus.Ok or MetricStatus.Stale;
        return valid ? Format(row, sample.Value) : "--";
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

    private void DrawText(DrawingContext dc, string text, double size, bool bold, Brush brush, Point origin, double maxWidth = 0)
    {
        if (!_appearance.IsTransparent)
        {
            dc.DrawText(GetText(text, size, bold, brush, maxWidth), origin);
            return;
        }

        // 用 4 次偏移绘制模拟 1px 黑边：比"几何描边"便宜得多（不触发轮廓三角化）
        FormattedText shadow = GetText(text, size, bold, ShadowBrush, maxWidth);
        dc.DrawText(shadow, new Point(origin.X - 1, origin.Y));
        dc.DrawText(shadow, new Point(origin.X + 1, origin.Y));
        dc.DrawText(shadow, new Point(origin.X, origin.Y - 1));
        dc.DrawText(shadow, new Point(origin.X, origin.Y + 1));
        dc.DrawText(GetText(text, size, bold, ShadowBodyBrush, maxWidth), origin);
    }

    private double MeasureText(string text, double size, bool bold)
        => GetText(text, size, bold, Brushes.White).Width;

    private FormattedText GetText(string text, double size, bool bold, Brush brush, double maxWidth = 0)
    {
        if (_textCache.Count > 256)
        {
            _textCache.Clear();
        }

        var key = (text, size, bold, brush);
        if (_textCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var formatted = new FormattedText(
            text,
            CultureInfo.CurrentUICulture,
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
