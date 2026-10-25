using LiteMon.Core.Metrics;
using LiteMon.Core.Sensors;
using LiteMon.Core.Snapshots;
using LiteMon.Sensors;

namespace LiteMon.Sensors.Tests;

public class SamplingServiceTests
{
    [Fact] // RI-20
    public void IntervalChange_TakesEffectImmediately()
    {
        var bus = new SnapshotBus();
        var fast = new CountingProvider(ProviderCadence.Fast);

        using var service = new SamplingService([fast], bus) { Interval = TimeSpan.FromSeconds(1) };
        service.Start();
        Thread.Sleep(1_600);

        int before = fast.Polls;
        Assert.True(before >= 2, "1 秒档运行 1.6 秒应至少采 2 次，实际 " + before);

        service.Interval = TimeSpan.FromSeconds(60);
        Thread.Sleep(1_200);

        int delta = fast.Polls - before;
        Assert.True(delta >= 1, "改间隔后应立即补采一轮");
        Assert.True(delta <= 2, "改间隔后不应继续按 1 秒节奏采样，实际 " + delta);
    }

    [Fact] // RI-21
    public void MediumCadence_NeverSlowerThanInterval()
    {
        var bus = new SnapshotBus();
        var medium = new CountingProvider(ProviderCadence.Medium);

        using var service = new SamplingService([medium], bus) { Interval = TimeSpan.FromSeconds(2) };
        service.Start();
        Thread.Sleep(4_500);

        // 2 秒间隔下：0s / 2s / 4s，容差 2–4 次
        Assert.InRange(medium.Polls, 2, 4);
    }

    [Fact] // RI-22
    public void Interval_IsClampedAndNormalized()
    {
        var bus = new SnapshotBus();
        using var service = new SamplingService([], bus);

        service.Interval = TimeSpan.FromSeconds(0.2);
        Assert.Equal(TimeSpan.FromSeconds(1), service.Interval);

        service.Interval = TimeSpan.FromSeconds(9999);
        Assert.Equal(TimeSpan.FromSeconds(60), service.Interval);

        service.Interval = TimeSpan.FromSeconds(2.6);
        Assert.Equal(TimeSpan.FromSeconds(3), service.Interval);
    }

    [Fact] // RI-23
    public void Publish_UsesStaleThresholdScaledByInterval()
    {
        var bus = new SnapshotBus();
        var fast = new CountingProvider(ProviderCadence.Fast);

        using var service = new SamplingService([fast], bus) { Interval = TimeSpan.FromSeconds(30) };
        service.Start();
        Thread.Sleep(600);
        service.Interval = TimeSpan.FromSeconds(60);
        Thread.Sleep(600);

        Snapshot snapshot = bus.Current;
        Assert.Equal(MetricStatus.Ok, snapshot.Get(MetricId.CpuLoad).Status);
    }

    private sealed class CountingProvider : ISensorProvider
    {
        private int _polls;

        public CountingProvider(ProviderCadence cadence) => Cadence = cadence;

        public string Name => "counting";

        public ProviderCadence Cadence { get; }

        public ProviderCapabilities Capabilities { get; } = new(new HashSet<MetricId>(), false, false);

        public bool IsEnabled => true;

        public int Polls => Volatile.Read(ref _polls);

        public void Initialize()
        {
        }

        public void Poll(IMetricWriter writer, long nowMs)
        {
            Interlocked.Increment(ref _polls);
            writer.Write(MetricId.CpuLoad, 1);
        }

        public void Dispose()
        {
        }
    }
}
