using LiteMon.Core.Metrics;
using LiteMon.Core.Sensors;

namespace LiteMon.Sensors;

/// <summary>系统运行时长（秒）。</summary>
public sealed class UptimeProvider : ISensorProvider
{
    public string Name => "uptime";

    public ProviderCadence Cadence => ProviderCadence.Slow;

    public ProviderCapabilities Capabilities { get; } = new(
        new HashSet<MetricId> { MetricId.UptimeSeconds },
        RequiresAdmin: false,
        RequiresDriver: false);

    public bool IsEnabled => true;

    public void Initialize()
    {
    }

    public void Poll(IMetricWriter writer, long nowMs)
    {
        writer.Write(MetricId.UptimeSeconds, Environment.TickCount64 / 1000.0);
    }

    public void Dispose()
    {
    }
}
