using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using LiteMon.Core.Appearance;
using LiteMon.Core.Diagnostics;
using LiteMon.Core.Layout;
using LiteMon.Core.Sensors;
using LiteMon.Core.Snapshots;
using LiteMon.Sensors;
using LiteMon.Settings;

namespace LiteMon.App;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "WPF Application 的清理在 OnExit 中完成")]
public partial class App : Application
{
    private readonly SettingsStore _store = new();
    private AppSettings _settings = AppSettings.Default;
    private SamplingService? _sampling;
    private SnapshotBus? _bus;
    private DeviceInfo _devices = DeviceInfo.Empty;
    private PanelWindow? _window;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += OnUnhandledException;

        _settings = _store.Load();

        var bus = new SnapshotBus();
        _bus = bus;
        ISensorProvider[] providers =
        [
            new CpuProvider(),
            new MemoryProvider(),
            new NetworkProvider(),
            new DiskProvider(),
            new NvmlProvider(),
            new LhmProvider(),
            new UptimeProvider(),
        ];

        _sampling = new SamplingService(providers, bus)
        {
            Interval = _settings.Interval,
        };
        _sampling.Start();

        AppearanceSettings appearance = _settings.Appearance.Normalized();
        (double panelWidth, double panelHeight) = PanelMetrics.GetSize(appearance.Layout, appearance.VisibleGroups);
        panelWidth = Math.Min(panelWidth, SystemParameters.VirtualScreenWidth - 24);

        var (left, top) = WindowPlacement.Clamp(
            _settings.WindowLeft,
            _settings.WindowTop,
            panelWidth,
            panelHeight,
            SystemParameters.VirtualScreenLeft,
            SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth,
            SystemParameters.VirtualScreenHeight);

        _window = CreateWindow(appearance, left, top);
        _window.Show();

        // 型号查询走 WMI，可能 1–2 秒：先显示面板，后台补齐型号
        _ = Task.Run(DeviceInfoCollector.Collect)
            .ContinueWith(
                task => Dispatcher.BeginInvoke(new Action(() =>
                {
                    _devices = task.Result;
                    _window?.SetDevices(_devices);
                })),
                TaskScheduler.Default);
        StartDiagnostics();
    }

    /// <summary>
    /// 应用新的刷新间隔：采样器立即重排 + 持久化，返回实际生效值。
    /// 这是全项目唯一的"改间隔"入口，菜单与对话框都只能走这里。
    /// </summary>
    private double ApplyInterval(double seconds)
    {
        double normalized = RefreshInterval.Normalize(seconds);
        if (_sampling is not null)
        {
            _sampling.Interval = TimeSpan.FromSeconds(normalized);
        }

        _settings = _settings with { RefreshIntervalSeconds = normalized };
        if (!_store.TrySave(_settings))
        {
            Debug.WriteLine("LiteMon: 设置保存失败（只影响下次启动）");
        }

        return normalized;
    }

    private PanelWindow CreateWindow(AppearanceSettings appearance, double left, double top)
    {
        return new PanelWindow(
            _bus!,
            _devices,
            _settings.RefreshIntervalSeconds,
            ApplyInterval,
            appearance,
            left,
            top,
            _settings.IsPinned,
            OnPinnedChanged,
            OnMoved,
            ApplyAppearance,
            appearance.IsTransparent);
    }

    /// <summary>
    /// 应用外观：归一化 → 持久化 → 按需重建窗口（透明/不透明对应分层/非分层，创建后不可切换）。
    /// </summary>
    private AppearanceSettings ApplyAppearance(AppearanceSettings appearance)
    {
        AppearanceSettings normalized = appearance.Normalized();
        _settings = _settings with { Appearance = normalized };

        if (!_store.TrySave(_settings))
        {
            Debug.WriteLine("LiteMon: 外观设置保存失败");
        }

        if (_window is null)
        {
            return normalized;
        }

        if (_window.IsLayered == normalized.IsTransparent)
        {
            _window.ApplyAppearance(normalized);
            return normalized;
        }

        // 先建新窗口再关旧的，避免"最后一个窗口关闭"导致程序退出
        PanelWindow previous = _window;
        _window = CreateWindow(normalized, previous.Left, previous.Top);
        _window.Show();
        _window.SetDevices(_devices);
        previous.Close();
        return normalized;
    }

    /// <summary>图钉状态变化：立即持久化。</summary>
    private void OnPinnedChanged(bool pinned)
    {
        _settings = _settings with { IsPinned = pinned };
        if (!_store.TrySave(_settings))
        {
            Debug.WriteLine("LiteMon: 固定状态保存失败");
        }
    }

    /// <summary>拖动结束：记录新位置。</summary>
    private void OnMoved(double left, double top)
    {
        _settings = _settings with { WindowLeft = left, WindowTop = top };
        if (!_store.TrySave(_settings))
        {
            Debug.WriteLine("LiteMon: 窗口位置保存失败");
        }
    }

    private void StartDiagnostics()
    {
        if (Environment.GetEnvironmentVariable("LITEMON_DIAG") != "1")
        {
            return;
        }

        string path = Path.Combine(AppContext.BaseDirectory, "diag.log");
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        long previousPoll = 0;
        long previousRender = 0;
        long previousFrames = 0;
        long previousAnimFrames = 0;
        long previousSnapshots = 0;
        long started = Stopwatch.GetTimestamp();

        timer.Tick += (_, _) =>
        {
            var (poll, render, frames, animFrames, snapshots) = PerfCounters.Read();
            double seconds = (Stopwatch.GetTimestamp() - started) / (double)Stopwatch.Frequency;
            double pollMs = (poll - previousPoll) * 1000.0 / Stopwatch.Frequency;
            double renderMs = (render - previousRender) * 1000.0 / Stopwatch.Frequency;
            long frameDelta = frames - previousFrames;
            long animDelta = animFrames - previousAnimFrames;
            long snapshotDelta = snapshots - previousSnapshots;

            previousPoll = poll;
            previousRender = render;
            previousFrames = frames;
            previousAnimFrames = animFrames;
            previousSnapshots = snapshots;

            using var process = Process.GetCurrentProcess();
            string line = $"{DateTimeOffset.Now:HH:mm:ss} t={seconds:F0}s admin={ElevationHelper.IsElevated} interval={_settings.RefreshIntervalSeconds:0}s poll={pollMs:F2}ms render={renderMs:F2}ms frames={frameDelta} anim={animDelta} snaps={snapshotDelta} ws={process.WorkingSet64 / 1048576.0:F0}MB";
            File.AppendAllText(path, line + Environment.NewLine);
        };

        timer.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _sampling?.Dispose();
        base.OnExit(e);
    }

    private static void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log(e.Exception);
        e.Handled = true;
    }

    private static void Log(Exception? exception)
    {
        if (exception is null)
        {
            return;
        }

        try
        {
            File.AppendAllText("crash.log", $"{DateTimeOffset.Now:O} {exception}{Environment.NewLine}");
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
