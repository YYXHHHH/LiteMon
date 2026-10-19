using LiteMon.Core.Metrics;
using LiteMon.Core.Sensors;
using LiteMon.Sensors.Interop;

namespace LiteMon.Sensors;

/// <summary>磁盘吞吐：PDH PhysicalDisk(_Total) 读写字节/秒。</summary>
public sealed class DiskProvider : ISensorProvider
{
    private readonly PdhQuery _pdh = new();

    public string Name => "disk";

    public ProviderCadence Cadence => ProviderCadence.Fast;

    public ProviderCapabilities Capabilities { get; } = new(
        new HashSet<MetricId> { MetricId.DiskReadBps, MetricId.DiskWriteBps },
        RequiresAdmin: false,
        RequiresDriver: false);

    public bool IsEnabled { get; private set; }

    public void Initialize()
    {
        IsEnabled = _pdh.Open(
            ("read", @"\PhysicalDisk(_Total)\Disk Read Bytes/sec"),
            ("write", @"\PhysicalDisk(_Total)\Disk Write Bytes/sec"));
    }

    public void Poll(IMetricWriter writer, long nowMs)
    {
        _pdh.Collect();

        writer.Write(MetricId.DiskReadBps, Math.Max(0, _pdh.Get("read") ?? 0));
        writer.Write(MetricId.DiskWriteBps, Math.Max(0, _pdh.Get("write") ?? 0));
    }

    public void Dispose() => _pdh.Dispose();
}
