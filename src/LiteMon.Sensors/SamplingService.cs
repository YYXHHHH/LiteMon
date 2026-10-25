using System.Diagnostics;
using LiteMon.Core.Diagnostics;
using LiteMon.Core.Metrics;
using LiteMon.Core.Sensors;
using LiteMon.Core.Snapshots;

namespace LiteMon.Sensors;

/// <summary>
/// 单采样线程 + 墙钟节拍。刷新间隔可在运行时修改，改动后立即重排并补采一轮。
/// 档位规则：Fast = I；Medium = max(2 s, I)；Slow = max(10 s, I)。
/// 陈旧阈值：max(SnapshotBuilder.StaleAfterMs, 3I)。
/// 单个 Provider 抛异常不影响其他 Provider。
/// </summary>
public sealed class SamplingService : IDisposable
{
    /// <summary>中档周期的下限：LHM/NVML 低于 2 s 会明显吃 CPU。</summary>
    private const int MediumFloorMs = 2_000;

    /// <summary>慢档周期的下限。</summary>
    private const int SlowFloorMs = 10_000;

    /// <summary>单次等待上限，防止时间跳变导致的超长休眠。</summary>
    private const int MaxWaitMs = 60_000;

    private readonly IReadOnlyList<ISensorProvider> _providers;
    private readonly SnapshotBus _bus;
    private readonly SnapshotBuilder _builder = new();
    private readonly ManualResetEventSlim _stop = new(false);
    private readonly ManualResetEventSlim _wake = new(false);
    private readonly WaitHandle[] _waits;

    private Thread? _thread;
    private long _intervalMs = 1_000;
    private long _scheduleVersion;
    private bool _disposed;

    public SamplingService(IReadOnlyList<ISensorProvider> providers, SnapshotBus bus)
    {
        ArgumentNullException.ThrowIfNull(providers);
        ArgumentNullException.ThrowIfNull(bus);

        _providers = providers;
        _bus = bus;
        _waits = [_stop.WaitHandle, _wake.WaitHandle];
    }

    /// <summary>当前刷新间隔（1–60 秒）。赋值后采样线程立即按新节拍重排并补采一轮。</summary>
    public TimeSpan Interval
    {
        get => TimeSpan.FromMilliseconds(Volatile.Read(ref _intervalMs));
        set
        {
            long milliseconds = (long)Math.Round(
                RefreshInterval.Normalize(value.TotalSeconds) * 1000,
                MidpointRounding.AwayFromZero);

            if (Interlocked.Exchange(ref _intervalMs, milliseconds) == milliseconds)
            {
                return;
            }

            // 版本号 + 唤醒：采样线程看到版本变化后会把三个档位的到期时间清零
            Interlocked.Increment(ref _scheduleVersion);
            _wake.Set();
        }
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

        long nextFast = 0;
        long nextMedium = 0;
        long nextSlow = 0;
        long seenVersion = -1;

        while (!_stop.IsSet)
        {
            long now = Environment.TickCount64;
            long intervalMs = Volatile.Read(ref _intervalMs);
            long version = Volatile.Read(ref _scheduleVersion);

            if (version != seenVersion)
            {
                // 刚改过间隔：三档立即到期，用户不用等满旧周期
                seenVersion = version;
                nextFast = 0;
                nextMedium = 0;
                nextSlow = 0;
            }

            _builder.Begin(now);

            if (now >= nextFast)
            {
                PollByCadence(ProviderCadence.Fast, now);
                nextFast = now + intervalMs;
            }

            if (now >= nextMedium)
            {
                PollByCadence(ProviderCadence.Medium, now);
                nextMedium = now + Math.Max(MediumFloorMs, intervalMs);
            }

            if (now >= nextSlow)
            {
                PollByCadence(ProviderCadence.Slow, now);
                nextSlow = now + Math.Max(SlowFloorMs, intervalMs);
            }

            _bus.Publish(_builder.Build(Math.Max(SnapshotBuilder.StaleAfterMs, intervalMs * 3)));

            int waitMs = (int)Math.Clamp(nextFast - Environment.TickCount64, 1, MaxWaitMs);
            int index = WaitHandle.WaitAny(_waits, waitMs, false);

            if (index == 0)
            {
                break;   // _stop 已置位
            }

            if (index == 1)
            {
                _wake.Reset();   // 被间隔变更唤醒，回到循环顶部重排
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
                long start = Stopwatch.GetTimestamp();
                provider.Poll(_builder, now);
                PerfCounters.AddPoll(Stopwatch.GetTimestamp() - start);
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
        _wake.Dispose();
    }
}
