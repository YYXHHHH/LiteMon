using LiteMon.Core.Metrics;

namespace LiteMon.Core.Tests;

public class SnapshotBuilderTests
{
    [Fact] // SNAP-01
    public void NewBuilder_ProducesAllUnavailable()
    {
        var builder = new SnapshotBuilder();
        builder.Begin(1_000);

        var snapshot = builder.Build();

        Assert.Equal((int)MetricId.Count, snapshot.All.Count);
        Assert.All(snapshot.All, sample => Assert.Equal(MetricStatus.Unavailable, sample.Status));
    }

    [Fact] // SNAP-02
    public void SlotIndex_MatchesMetricId()
    {
        var builder = new SnapshotBuilder();
        builder.Begin(1_000);
        builder.Write(MetricId.CpuLoad, 42);

        var snapshot = builder.Build();

        foreach (var id in Enum.GetValues<MetricId>())
        {
            if (id == MetricId.Count) continue;

            var sample = snapshot.Get(id);
            Assert.Equal(id, sample.Id);
            Assert.Equal(id, snapshot.All[(int)id].Id);
        }

        Assert.Equal(42, snapshot.Get(MetricId.CpuLoad).Value);
    }

    [Fact] // SNAP-03
    public void ValuesBecomeStale_AfterTwelveSeconds()
    {
        var builder = new SnapshotBuilder();
        builder.Begin(0);
        builder.Write(MetricId.CpuLoad, 42);

        Assert.Equal(MetricStatus.Ok, builder.Build().Get(MetricId.CpuLoad).Status);

        builder.Begin(SnapshotBuilder.StaleAfterMs);
        Assert.Equal(MetricStatus.Ok, builder.Build().Get(MetricId.CpuLoad).Status);

        builder.Begin(SnapshotBuilder.StaleAfterMs + 1);
        Assert.Equal(MetricStatus.Stale, builder.Build().Get(MetricId.CpuLoad).Status);
    }

    [Fact] // SNAP-04
    public void Snapshot_IsImmutable_AfterBuilderChanges()
    {
        var builder = new SnapshotBuilder();
        builder.Begin(0);
        builder.Write(MetricId.CpuLoad, 42);

        var snapshot = builder.Build();
        builder.Write(MetricId.CpuLoad, 99);

        Assert.Equal(42, snapshot.Get(MetricId.CpuLoad).Value);
    }

    [Fact] // SNAP-05
    public void Snapshot_RejectsWrongSlotCount()
    {
        Assert.Throws<ArgumentException>(() => new Snapshot(0, 0, new MetricSample[8]));
    }

    [Fact] // SNAP-06
    public void Unavailable_IsNotZero()
    {
        var builder = new SnapshotBuilder();
        builder.Begin(0);
        builder.Write(MetricId.CpuTemp, 0, MetricStatus.Unavailable);

        var sample = builder.Build().Get(MetricId.CpuTemp);

        Assert.Equal(MetricStatus.Unavailable, sample.Status);
    }
}
