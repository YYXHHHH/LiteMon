using LiteMon.Core.Formatting;
using LiteMon.Core.Metrics;

namespace LiteMon.Core.Tests;

public class MetricFormatterTests
{
    [Theory] // FMT-01 / FMT-02
    [InlineData(0, "0 B")]
    [InlineData(1023, "1023 B")]
    [InlineData(1024, "1.0 KB")]
    [InlineData(1048576, "1.0 MB")]
    [InlineData(1099511627776, "1.0 TB")]
    public void Bytes_Uses1024Base(double value, string expected)
    {
        Assert.Equal(expected, MetricFormatter.Bytes(value));
    }

    [Fact] // FMT-03
    public void Percent_ClampsToRange()
    {
        Assert.Equal("0%", MetricFormatter.Percent(-5));
        Assert.Equal("100%", MetricFormatter.Percent(105));
        Assert.Equal("47%", MetricFormatter.Percent(47.3));
    }

    [Fact] // FMT-04
    public void Rate_ZeroOrNegative_IsZero()
    {
        Assert.Equal("0 B/s", MetricFormatter.Rate(0));
        Assert.Equal("0 B/s", MetricFormatter.Rate(-1));
        Assert.Equal("1.0 KB/s", MetricFormatter.Rate(1024));
    }

    [Fact] // FMT-05
    public void Celsius_RoundsToInteger()
    {
        Assert.Equal("73 °C", MetricFormatter.Celsius(72.6));
        Assert.Equal("0 °C", MetricFormatter.Celsius(0));
    }

    [Fact] // FMT-06
    public void NonFiniteValues_ArePlaceholders()
    {
        Assert.Equal("--", MetricFormatter.Bytes(double.NaN));
        Assert.Equal("--", MetricFormatter.Celsius(double.PositiveInfinity));
        Assert.Equal("--", MetricFormatter.Percent(double.NaN));
        Assert.Equal("--", MetricFormatter.Format(MetricId.CpuTemp, double.NaN));
    }

    [Fact] // FMT-07
    public void UnavailableAndError_ShowPlaceholder()
    {
        Assert.Equal("--", MetricFormatter.Format(MetricId.CpuTemp, 72, MetricStatus.Unavailable));
        Assert.Equal("--", MetricFormatter.Format(MetricId.CpuLoad, 50, MetricStatus.Error));
    }

    [Fact] // FMT-08
    public void Format_DispatchesByMetricId()
    {
        Assert.Equal("73 °C", MetricFormatter.Format(MetricId.CpuTemp, 72.6));
        Assert.Equal("42%", MetricFormatter.Format(MetricId.CpuLoad, 42));
        Assert.Equal("3600 MHz", MetricFormatter.Format(MetricId.CpuClockMhz, 3600));
        Assert.Equal("65 W", MetricFormatter.Format(MetricId.GpuPowerW, 65.4));
        Assert.Equal("1.0 KB/s", MetricFormatter.Format(MetricId.NetDownBps, 1024));
        Assert.Equal("1653 MB", MetricFormatter.Format(MetricId.GpuMemUsedMb, 1653));
    }
}
