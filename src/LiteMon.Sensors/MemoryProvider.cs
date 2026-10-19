using System.Runtime.InteropServices;
using LiteMon.Core.Metrics;
using LiteMon.Core.Sensors;
using LiteMon.Sensors.Interop;

namespace LiteMon.Sensors;

public sealed class MemoryProvider : ISensorProvider
{
    public string Name => "memory";

    public ProviderCadence Cadence => ProviderCadence.Fast;

    public ProviderCapabilities Capabilities { get; } = new(
        new HashSet<MetricId>
        {
            MetricId.MemUsedPercent, MetricId.MemUsedBytes,
            MetricId.MemTotalBytes, MetricId.MemAvailableBytes,
        },
        RequiresAdmin: false,
        RequiresDriver: false);

    public bool IsEnabled => true;

    public void Initialize()
    {
    }

    public void Poll(IMetricWriter writer, long nowMs)
    {
        var status = new NativeMethods.MEMORYSTATUSEX
        {
            Length = (uint)Marshal.SizeOf<NativeMethods.MEMORYSTATUSEX>(),
        };

        if (!NativeMethods.GlobalMemoryStatusEx(ref status) || status.TotalPhys == 0)
        {
            writer.Write(MetricId.MemUsedPercent, 0, MetricStatus.Unavailable);
            return;
        }

        writer.Write(MetricId.MemUsedPercent, 100.0 * (status.TotalPhys - status.AvailPhys) / status.TotalPhys);
        writer.Write(MetricId.MemUsedBytes, status.TotalPhys - status.AvailPhys);
        writer.Write(MetricId.MemTotalBytes, status.TotalPhys);
        writer.Write(MetricId.MemAvailableBytes, status.AvailPhys);
    }

    public void Dispose()
    {
    }
}
