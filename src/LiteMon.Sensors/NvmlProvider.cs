using LiteMon.Core.Metrics;
using LiteMon.Core.Sensors;
using LiteMon.Sensors.Interop;

namespace LiteMon.Sensors;

/// <summary>NVIDIA GPU：NVML。驱动已安装即可读，无需管理员。</summary>
public sealed class NvmlProvider : ISensorProvider
{
    private IntPtr _device;
    private bool _initialized;
    private uint _memTotalMb;

    public string Name => "nvml";

    public ProviderCadence Cadence => ProviderCadence.Medium;

    public ProviderCapabilities Capabilities { get; } = new(
        new HashSet<MetricId>
        {
            MetricId.GpuLoad, MetricId.GpuTemp, MetricId.GpuMemUsedMb,
            MetricId.GpuMemTotalMb, MetricId.GpuPowerW,
        },
        RequiresAdmin: false,
        RequiresDriver: false);

    public bool IsEnabled { get; private set; }

    public void Initialize()
    {
        try
        {
            if (NativeMethods.nvmlInit_v2() != 0)
            {
                return;
            }

            _initialized = true;

            if (NativeMethods.nvmlDeviceGetCount_v2(out var count) != 0 || count == 0)
            {
                return;
            }

            if (NativeMethods.nvmlDeviceGetHandleByIndex_v2(0, out _device) != 0)
            {
                return;
            }

            if (NativeMethods.nvmlDeviceGetMemoryInfo(_device, out var memory) == 0 && memory.Total > 0)
            {
                _memTotalMb = (uint)(memory.Total / 1024 / 1024);
            }

            IsEnabled = true;
        }
        catch (DllNotFoundException)
        {
            IsEnabled = false;
        }
        catch (EntryPointNotFoundException)
        {
            IsEnabled = false;
        }
    }

    public void Poll(IMetricWriter writer, long nowMs)
    {
        if (NativeMethods.nvmlDeviceGetUtilizationRates(_device, out var utilization) == 0)
        {
            writer.Write(MetricId.GpuLoad, utilization.Gpu);
        }

        if (NativeMethods.nvmlDeviceGetTemperature(_device, 0, out var temperature) == 0)
        {
            writer.Write(MetricId.GpuTemp, temperature);
        }

        if (NativeMethods.nvmlDeviceGetMemoryInfo(_device, out var memory) == 0)
        {
            writer.Write(MetricId.GpuMemUsedMb, memory.Used / 1024.0 / 1024.0);
            writer.Write(MetricId.GpuMemTotalMb, memory.Total / 1024.0 / 1024.0);
        }
        else if (_memTotalMb > 0)
        {
            writer.Write(MetricId.GpuMemTotalMb, _memTotalMb);
        }

        if (NativeMethods.nvmlDeviceGetPowerUsage(_device, out var milliwatts) == 0)
        {
            writer.Write(MetricId.GpuPowerW, milliwatts / 1000.0);
        }
    }

    public void Dispose()
    {
        if (_initialized)
        {
            _ = NativeMethods.nvmlShutdown();
            _initialized = false;
        }
    }
}
