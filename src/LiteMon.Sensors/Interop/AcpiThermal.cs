using System.Globalization;
using System.Management;

namespace LiteMon.Sensors.Interop;

/// <summary>
/// 非管理员可读的 ACPI 热区温度：
/// Win32_PerfFormattedData_Counters_ThermalZoneInformation（root\cimv2）。
/// HighPrecisionTemperature 单位为 0.1 K，Temperature 单位为 K。
/// 取所有热区中的最高值，作为 CPU 附近温度的近似。
/// </summary>
internal static class AcpiThermal
{
    public static bool TryRead(out double celsius)
    {
        celsius = 0;

        try
        {
            using var searcher = new ManagementObjectSearcher(
                "root\\cimv2",
                "SELECT HighPrecisionTemperature, Temperature FROM Win32_PerfFormattedData_Counters_ThermalZoneInformation");

            double hottestKelvin = 0;

            foreach (ManagementObject row in searcher.Get())
            {
                using (row)
                {
                    double kelvin = ReadKelvin(row);
                    if (kelvin is > 250 and < 400)
                    {
                        hottestKelvin = Math.Max(hottestKelvin, kelvin);
                    }
                }
            }

            if (hottestKelvin <= 0)
            {
                return false;
            }

            celsius = hottestKelvin - 273.15;
            return true;
        }
        catch (ManagementException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static double ReadKelvin(ManagementBaseObject row)
    {
        if (row["HighPrecisionTemperature"] is { } high
            && double.TryParse(high.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double tenths)
            && tenths > 1000)
        {
            return tenths / 10.0;
        }

        if (row["Temperature"] is { } plain
            && double.TryParse(plain.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double kelvin))
        {
            return kelvin;
        }

        return 0;
    }
}
