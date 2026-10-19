using LiteMon.Core.Metrics;

namespace LiteMon.Core.Sensors;

public enum ProviderCadence
{
    Fast = 0,
    Medium = 1,
    Slow = 2,
}

public sealed record ProviderCapabilities(
    IReadOnlySet<MetricId> Metrics,
    bool RequiresAdmin,
    bool RequiresDriver);

public interface IMetricWriter
{
    void Write(MetricId id, double value, MetricStatus status = MetricStatus.Ok);
}

public interface ISensorProvider : IDisposable
{
    string Name { get; }

    ProviderCadence Cadence { get; }

    ProviderCapabilities Capabilities { get; }

    bool IsEnabled { get; }

    void Initialize();

    void Poll(IMetricWriter writer, long nowMs);
}
