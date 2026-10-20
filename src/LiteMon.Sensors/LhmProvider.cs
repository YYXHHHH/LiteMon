using LiteMon.Core.Metrics;
using LiteMon.Core.Sensors;
using LiteMon.Sensors.Interop;
using LibreHardwareMonitor.Hardware;

namespace LiteMon.Sensors;

/// <summary>
/// CPU 温度/功耗/风扇：LibreHardwareMonitorLib + PawnIO。
/// 非管理员时驱动不加载，LHM 返回 0.0，这里按 M0 结论把 &lt;= 0 视为不可用。
/// </summary>
public sealed class LhmProvider : ISensorProvider
{
    private Computer? _computer;
    private double? _acpiCelsius;
    private long _acpiNextAt;

    public string Name => "lhm";

    public ProviderCadence Cadence => ProviderCadence.Medium;

    public ProviderCapabilities Capabilities { get; } = new(
        new HashSet<MetricId> { MetricId.CpuTemp, MetricId.CpuTempSource, MetricId.CpuPowerW, MetricId.FanRpm },
        RequiresAdmin: true,
        RequiresDriver: true);

    public bool IsEnabled { get; private set; }

    public void Initialize()
    {
        try
        {
            _computer = new Computer
            {
                IsCpuEnabled = true,
                IsMotherboardEnabled = true,
                IsMemoryEnabled = false,
                IsStorageEnabled = false,
                IsGpuEnabled = false,
                IsNetworkEnabled = false,
            };

            _computer.Open();
            IsEnabled = _computer.Hardware.Count > 0;
        }
        catch (Exception)
        {
            _computer = null;
            IsEnabled = false;
        }
    }

    public void Poll(IMetricWriter writer, long nowMs)
    {
        if (_computer is null)
        {
            return;
        }

        double? temperature = null;
        double? power = null;
        double? fan = null;

        try
        {
            foreach (var hardware in _computer.Hardware)
            {
                ReadHardware(hardware, ref temperature, ref power, ref fan);
            }
        }
        catch (Exception)
        {
            return;
        }

        // 内核温度读不到时，回退到 ACPI 热区（非管理员可用），每 10 秒刷新一次
        bool fromLhm = temperature is > 0;
        if (!fromLhm)
        {
            temperature = TryAcpi(nowMs);
        }

        Write(writer, MetricId.CpuTemp, temperature, 1, 120);

        if (temperature is > 0)
        {
            writer.Write(MetricId.CpuTempSource, fromLhm ? 1 : 2);
        }
        else
        {
            writer.Write(MetricId.CpuTempSource, 0, MetricStatus.Unavailable);
        }
        Write(writer, MetricId.CpuPowerW, power, 1, 500);
        Write(writer, MetricId.FanRpm, fan, 1, 20_000);
    }

    public void Dispose()
    {
        _computer?.Close();
        _computer = null;
    }

    private static void ReadHardware(IHardware hardware, ref double? temperature, ref double? power, ref double? fan)
    {
        hardware.Update();

        foreach (var sensor in hardware.Sensors)
        {
            if (sensor.Value is not { } value)
            {
                continue;
            }

            switch (sensor.SensorType)
            {
                case SensorType.Temperature when IsPackage(sensor.Name) && value > 0:
                    temperature ??= value;
                    break;
                case SensorType.Power when sensor.Name.Contains("Package", StringComparison.OrdinalIgnoreCase) && value > 0:
                    power ??= value;
                    break;
                case SensorType.Fan when value > 0:
                    fan ??= value;
                    break;
            }
        }

        foreach (var sub in hardware.SubHardware)
        {
            ReadHardware(sub, ref temperature, ref power, ref fan);
        }
    }

    private double? TryAcpi(long nowMs)
    {
        if (nowMs < _acpiNextAt)
        {
            return _acpiCelsius;
        }

        _acpiNextAt = nowMs + 10_000;

        if (AcpiThermal.TryRead(out double celsius))
        {
            _acpiCelsius = celsius;
        }

        return _acpiCelsius;
    }

    private static bool IsPackage(string name)
        => name.Contains("Package", StringComparison.OrdinalIgnoreCase)
           || name.Contains("Tctl", StringComparison.OrdinalIgnoreCase);

    private static void Write(IMetricWriter writer, MetricId id, double? value, double min, double max)
    {
        if (value is { } v && v >= min && v <= max)
        {
            writer.Write(id, v);
        }
        else
        {
            writer.Write(id, 0, MetricStatus.Unavailable);
        }
    }
}
