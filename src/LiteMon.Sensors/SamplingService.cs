using LiteMon.Core.Metrics;
using LiteMon.Core.Sensors;
using LiteMon.Core.Snapshots;

namespace LiteMon.Sensors;

/// <summary>
/// 单采样线程 + 内部节拍：快 1 s / 中 2 s / 慢 10 s。
/// 单个 Provider 抛异常不影响其他 Provider。
/// </summary>
public sealed class SamplingService : IDisposable
{
    private readonly IReadOnlyList<ISensorProvider> _providers;
    private readonly SnapshotBus _bus;
    private readonly SnapshotBuilder _builder = new();
    private readonly ManualResetEventSlim _stop = new(false);
    private Thread? _thread;
    private bool _disposed;

    public SamplingService(IReadOnlyList<ISensorProvider> providers, SnapshotBus bus)
    {
        _providers = providers;
        _bus = bus;
    }

    public void Start()
    {
        if (_thread is not null)
        {
            return;
        }

        _thread = new Thread(Run)
        {
            IsBackground = true,
            Name = "sampler",
            Priority = ThreadPriority.BelowNormal,
        };
        _thread.Start();
    }

    private void Run()
    {
        foreach (var provider in _providers)
        {
            try
            {
                provider.Initialize();
            }
            catch (Exception)
            {
                // 初始化失败 = 该 Provider 不可用，不影响其他
            }
        }

        long tick = 0;

        while (!_stop.IsSet)
        {
            long now = Environment.TickCount64;
            _builder.Begin(now);

            PollByCadence(ProviderCadence.Fast, now);
            if (tick % 2 == 0)
            {
                PollByCadence(ProviderCadence.Medium, now);
            }

            if (tick % 10 == 0)
            {
                PollByCadence(ProviderCadence.Slow, now);
            }

            _bus.Publish(_builder.Build());
            tick++;

            if (_stop.Wait(1000))
            {
                break;
            }
        }

        foreach (var provider in _providers)
        {
            try
            {
                provider.Dispose();
            }
            catch (Exception)
            {
                // 退出阶段忽略清理异常
            }
        }
    }

    private void PollByCadence(ProviderCadence cadence, long now)
    {
        foreach (var provider in _providers)
        {
            if (!provider.IsEnabled || provider.Cadence != cadence)
            {
                continue;
            }

            try
            {
                provider.Poll(_builder, now);
            }
            catch (Exception)
            {
                // 单轮失败：保持上一轮状态，下一轮继续
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _stop.Set();
        _thread?.Join(3000);
        _stop.Dispose();
    }
}
