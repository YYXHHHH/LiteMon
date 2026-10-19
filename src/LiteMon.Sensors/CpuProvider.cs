using LiteMon.Core.Metrics;
using LiteMon.Core.Sensors;
using LiteMon.Sensors.Interop;

namespace LiteMon.Sensors;

/// <summary>CPU 总利用率（PDH % Processor Utility）+ 当前频率（CallNtPowerInformation）。</summary>
public sealed class CpuProvider : ISensorProvider
{
    private readonly PdhQuery _pdh = new();
    private bool _primed;

    public string Name => "cpu";

    public ProviderCadence Cadence => ProviderCadence.Fast;

    public ProviderCapabilities Capabilities { get; } = new(
        new HashSet<MetricId> { MetricId.CpuLoad, MetricId.CpuClockMhz },
        RequiresAdmin: false,
        RequiresDriver: false);

    public bool IsEnabled { get; private set; }

    public void Initialize()
    {
        IsEnabled = _pdh.Open(("cpu", @"\Processor Information(_Total)\% Processor Utility"));
    }

    public void Poll(IMetricWriter writer, long nowMs)
    {
        _pdh.Collect();

        // PDH 首轮只建立基线，没有有效速率
        if (!_primed)
        {
            _primed = true;
            return;
        }

        if (_pdh.Get("cpu") is { } usage)
        {
            writer.Write(MetricId.CpuLoad, Math.Clamp(usage, 0, 100));
        }

        if (NativeMethods.TryGetAverageMhz(out var mhz))
        {
            writer.Write(MetricId.CpuClockMhz, mhz);
        }
    }

    public void Dispose() => _pdh.Dispose();
}
