using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using LiteMon.Core.Diagnostics;
using LiteMon.Core.Sensors;
using LiteMon.Core.Snapshots;
using LiteMon.Sensors;

namespace LiteMon.App;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "WPF Application 的清理在 OnExit 中完成")]
public partial class App : Application
{
    private SamplingService? _sampling;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += OnUnhandledException;

        var bus = new SnapshotBus();
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

        _sampling = new SamplingService(providers, bus);
        _sampling.Start();

        DeviceInfo devices = DeviceInfoCollector.Collect();
        new PanelWindow(bus, devices).Show();
        StartDiagnostics();
    }

    private static void StartDiagnostics()
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
            string line = $"{DateTimeOffset.Now:HH:mm:ss} t={seconds:F0}s admin={ElevationHelper.IsElevated} poll={pollMs:F2}ms render={renderMs:F2}ms frames={frameDelta} anim={animDelta} snaps={snapshotDelta} ws={process.WorkingSet64 / 1048576.0:F0}MB";
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
