using System.IO;
using System.Windows;
using System.Windows.Threading;
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

        new PanelWindow(bus).Show();
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
