using System.Globalization;
using LiteMon.Core.Metrics;

namespace LiteMon.Core.Formatting;

/// <summary>
/// 指标格式化。统一单位与小数位，保证 UI 与测试一致。
/// 非有限值（NaN/∞）一律输出 "--"，绝不输出 "NaN%"。
/// </summary>
public static class MetricFormatter
{
    private static readonly string[] ByteUnits = ["B", "KB", "MB", "GB", "TB"];

    public static string Format(MetricId id, double value, MetricStatus status = MetricStatus.Ok)
        => status is MetricStatus.Unavailable or MetricStatus.Error ? "--" : FormatValue(id, value);

    public static string FormatValue(MetricId id, double value) => id switch
    {
        MetricId.CpuLoad or MetricId.CpuCoreMax or MetricId.CpuCoreAvg
            or MetricId.GpuLoad or MetricId.GpuFanPercent
            or MetricId.MemUsedPercent or MetricId.DiskUsedPercent
            or MetricId.BatteryPercent => Percent(value),

        MetricId.CpuTemp or MetricId.GpuTemp or MetricId.GpuHotspotTemp or MetricId.DiskTemp => Celsius(value),

        MetricId.CpuClockMhz or MetricId.GpuClockMhz => Megahertz(value),
        MetricId.CpuPowerW or MetricId.GpuPowerW => Watts(value),

        MetricId.GpuMemUsedMb or MetricId.GpuMemTotalMb => Megabytes(value),

        MetricId.MemUsedBytes or MetricId.MemTotalBytes or MetricId.MemAvailableBytes
            or MetricId.DiskFreeBytes or MetricId.NetTotalIn or MetricId.NetTotalOut => Bytes(value),

        MetricId.DiskReadBps or MetricId.DiskWriteBps
            or MetricId.NetUpBps or MetricId.NetDownBps
            or MetricId.NetLinkDownBps or MetricId.NetLinkUpBps => Rate(value),

        MetricId.FanRpm => Rpm(value),
        MetricId.Voltage => Volts(value),
        MetricId.AcOnline => value > 0 ? "AC" : "Battery",
        MetricId.UptimeSeconds => Uptime(value),
        MetricId.ProcessCount => F0(value),
        _ => F1(value),
    };

    /// <summary>1024 进制，0–1023 用整数 B，其余保留一位小数。</summary>
    public static string Bytes(double value)
    {
        if (!double.IsFinite(value)) return "--";
        if (value < 0) value = 0;

        int unit = 0;
        while (value >= 1024 && unit < ByteUnits.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0 ? $"{F0(value)} B" : $"{F1(value)} {ByteUnits[unit]}";
    }

    public static string Rate(double value)
    {
        if (!double.IsFinite(value) || value <= 0) return "0 B/s";
        return Bytes(value) + "/s";
    }

    public static string Percent(double value)
    {
        if (!double.IsFinite(value)) return "--";
        return $"{F0(Math.Clamp(value, 0, 100))}%";
    }

    public static string Celsius(double value) => double.IsFinite(value) ? $"{F0(value)} °C" : "--";

    public static string Megahertz(double value) => double.IsFinite(value) ? $"{F0(value)} MHz" : "--";

    public static string Watts(double value) => double.IsFinite(value) ? $"{F0(value)} W" : "--";

    public static string Megabytes(double value) => double.IsFinite(value) ? $"{F0(value)} MB" : "--";

    public static string Rpm(double value) => double.IsFinite(value) ? $"{F0(value)} RPM" : "--";

    public static string Volts(double value) => double.IsFinite(value) ? $"{F2(value)} V" : "--";

    public static string Uptime(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds < 0) return "--";
        var span = TimeSpan.FromSeconds(seconds);
        return span.TotalDays >= 1
            ? $"{(int)span.TotalDays}d {span.Hours:00}:{span.Minutes:00}"
            : $"{span.Hours:00}:{span.Minutes:00}";
    }

    private static string F0(double value) => value.ToString("F0", CultureInfo.InvariantCulture);

    private static string F1(double value) => value.ToString("F1", CultureInfo.InvariantCulture);

    private static string F2(double value) => value.ToString("F2", CultureInfo.InvariantCulture);
}
