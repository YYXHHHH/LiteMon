using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace RenderProbe;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        try
        {
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };

            if (Environment.GetCommandLineArgs().Contains("--baseline"))
            {
                var plain = new Window
                {
                    Width = 320,
                    Height = 460,
                    WindowStyle = WindowStyle.None,
                    ShowInTaskbar = false,
                    Background = Brushes.Black,
                };
                plain.Show();
                var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
                timer.Tick += (_, _) =>
                {
                    timer.Stop();
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                    var p = Process.GetCurrentProcess();
                    Console.WriteLine($"=== WPF baseline window (no custom render) ===");
                    Console.WriteLine($"working set   : {p.WorkingSet64 / 1048576.0:F1} MB (private {p.PrivateMemorySize64 / 1048576.0:F1} MB)");
                    app.Shutdown();
                };
                timer.Start();
                app.Run();
                return;
            }
            var window = new ProbeWindow();
            window.Show();
            window.StartBenchmark(() => app.Shutdown());
            app.Run();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"WPF host failed: {ex.GetType().Name}: {ex.Message}");
        }
    }
}

internal sealed class ProbeWindow : Window
{
    private const int RowCount = 12;
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    private readonly string[] _labels =
    {
        "CPU 使用率", "CPU 温度", "CPU 频率", "GPU 使用率",
        "GPU 温度", "显存", "内存", "磁盘读",
        "磁盘写", "网络下行", "网络上行", "风扇",
    };

    private readonly double[] _target = new double[RowCount];
    private readonly double[] _display = new double[RowCount];
    private readonly Dictionary<(string Text, double Size), FormattedText> _textCache = new();

    private static readonly Brush LabelBrush = Frozen(Color.FromRgb(0xA9, 0xB4, 0xC0));
    private static readonly Brush ValueBrush = Frozen(Colors.White);
    private static readonly Brush TrackBrush = Frozen(Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF));
    private static readonly Brush LowBrush = Frozen(Color.FromRgb(0x4A, 0xDE, 0x80));
    private static readonly Brush MidBrush = Frozen(Color.FromRgb(0xFB, 0xBF, 0x24));
    private static readonly Brush HighBrush = Frozen(Color.FromRgb(0xF8, 0x71, 0x71));

    private static Brush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly List<double> _renderMs = new();
    private readonly List<double> _frameMs = new();
    private readonly Random _random = new(20261019);

    private long _lastFrameTicks;
    private long _startAlloc;
    private int _frames;
    private int _gc0, _gc1, _gc2;

    public ProbeWindow()
    {
        Title = "LiteMon RenderProbe";
        Width = 320;
        Height = 460;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Topmost = true;
        Background = new SolidColorBrush(Color.FromRgb(0x0B, 0x0E, 0x14));
        Left = 80;
        Top = 80;
    }

    public void StartBenchmark(Action onDone)
    {
        for (int i = 0; i < RowCount; i++) _target[i] = 20 + i * 5;
        Array.Copy(_target, _display, RowCount);

        var update = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1),
        };
        update.Tick += (_, _) =>
        {
            for (int i = 0; i < RowCount; i++)
                _target[i] = 10 + _random.NextDouble() * 85;
        };
        update.Start();

        _startAlloc = GC.GetTotalAllocatedBytes(false);
        _gc0 = GC.CollectionCount(0);
        _gc1 = GC.CollectionCount(1);
        _gc2 = GC.CollectionCount(2);

        var sw = Stopwatch.StartNew();
        CompositionTarget.Rendering += OnRendering;

        // 10 秒后收尾
        var finish = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        finish.Tick += (_, _) =>
        {
            finish.Stop();
            CompositionTarget.Rendering -= OnRendering;
            Report(sw.Elapsed);
            onDone();
        };
        finish.Start();
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        long now = _clock.ElapsedTicks;
        if (_lastFrameTicks != 0)
            _frameMs.Add((now - _lastFrameTicks) * 1000.0 / Stopwatch.Frequency);
        _lastFrameTicks = now;

        bool settled = true;
        for (int i = 0; i < RowCount; i++)
        {
            double delta = _target[i] - _display[i];
            if (Math.Abs(delta) > 0.05)
            {
                _display[i] += delta * 0.35;
                settled = false;
            }
        }

        if (!settled)
        {
            // 只有动画未收敛时才重绘；收敛后不产生任何渲染负载
            InvalidateVisual();
        }
    }

    protected override void OnRender(DrawingContext dc)
    {
        long t0 = Stopwatch.GetTimestamp();

        dc.DrawRectangle(Background, null, new Rect(0, 0, ActualWidth, ActualHeight));

        double y = 12;
        double rowHeight = (ActualHeight - 24) / RowCount;

        for (int i = 0; i < RowCount; i++)
        {
            var label = GetText(_labels[i], 12, LabelBrush);
            dc.DrawText(label, new Point(14, y + 2));

            var value = GetText(_display[i].ToString("F0") + "%", 15, ValueBrush);
            dc.DrawText(value, new Point(ActualWidth - 14 - value.Width, y));

            var track = new Rect(14, y + rowHeight - 6, ActualWidth - 28, 3);
            dc.DrawRectangle(TrackBrush, null, track);

            double w = Math.Clamp(_display[i] / 100.0, 0, 1) * track.Width;
            var fill = _display[i] > 80 ? HighBrush : _display[i] > 60 ? MidBrush : LowBrush;
            dc.DrawRectangle(fill, null, new Rect(track.X, track.Y, w, track.Height));

            y += rowHeight;
        }

        _frames++;
        _renderMs.Add((Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency);
    }

    private FormattedText GetText(string text, double size, Brush brush)
    {
        if (_textCache.Count > 256)
        {
            _textCache.Clear();          // 防止数字变化导致缓存无限增长
        }

        var key = (text, size);
        if (_textCache.TryGetValue(key, out var cached))
        {
            return cached;
        }
        var ft = new FormattedText(text, Culture, FlowDirection.LeftToRight,
            new Typeface("Microsoft YaHei UI"), size, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
        _textCache[key] = ft;
        return ft;
    }

    private void Report(TimeSpan elapsed)
    {
        long alloc = GC.GetTotalAllocatedBytes(false) - _startAlloc;
        double avgRender = _renderMs.Count > 0 ? _renderMs.Average() : 0;
        double p95Render = Percentile(_renderMs, 0.95);
        double avgFrame = _frameMs.Count > 0 ? _frameMs.Average() : 0;
        double p95Frame = Percentile(_frameMs, 0.95);
        double fps = elapsed.TotalSeconds > 0 ? _frames / elapsed.TotalSeconds : 0;
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var proc = Process.GetCurrentProcess();
        double wsMb = proc.WorkingSet64 / 1048576.0;
        double privateMb = proc.PrivateMemorySize64 / 1048576.0;
        double allocPerSec = alloc / elapsed.TotalSeconds / 1024.0;

        Console.WriteLine("=== RenderProbe (WPF self-render, 12 rows) ===");
        Console.WriteLine($"duration      : {elapsed.TotalSeconds:F1} s");
        Console.WriteLine($"frames        : {_frames}  (fps {fps:F1})");
        Console.WriteLine($"frame avg/p95 : {avgFrame:F2} / {p95Frame:F2} ms");
        Console.WriteLine($"render avg/p95: {avgRender:F3} / {p95Render:F3} ms  (samples {_renderMs.Count})");
        Console.WriteLine($"alloc         : {alloc / 1024.0:F0} KB total, {allocPerSec:F1} KB/s");
        Console.WriteLine($"GC 0/1/2      : {GC.CollectionCount(0) - _gc0}/{GC.CollectionCount(1) - _gc1}/{GC.CollectionCount(2) - _gc2}");
        Console.WriteLine($"working set   : {wsMb:F1} MB (private {privateMb:F1} MB)");
    }

    private static double Percentile(List<double> values, double p)
    {
        if (values.Count == 0) return 0;
        var sorted = values.OrderBy(v => v).ToArray();
        int index = (int)Math.Ceiling(p * sorted.Length) - 1;
        return sorted[Math.Clamp(index, 0, sorted.Length - 1)];
    }
}
