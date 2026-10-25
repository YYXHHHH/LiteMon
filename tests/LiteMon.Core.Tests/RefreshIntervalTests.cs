using LiteMon.Core.Sensors;

namespace LiteMon.Core.Tests;

public class RefreshIntervalTests
{
    [Theory] // RI-01
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(0.4, 1)]
    [InlineData(1, 1)]
    [InlineData(2.5, 3)]
    [InlineData(59.6, 60)]
    [InlineData(60, 60)]
    [InlineData(999, 60)]
    [InlineData(double.NaN, 1)]
    [InlineData(double.PositiveInfinity, 60)]
    public void Normalize_ClampsToRange(double input, double expected)
        => Assert.Equal(expected, RefreshInterval.Normalize(input));

    [Theory] // RI-02
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(5, true)]
    [InlineData(10, true)]
    [InlineData(3, false)]
    [InlineData(60, false)]
    public void IsPreset_MatchesMenu(double seconds, bool expected)
        => Assert.Equal(expected, RefreshInterval.IsPreset(seconds));

    [Theory] // RI-03
    [InlineData("5", true, 5)]
    [InlineData(" 5 ", true, 5)]
    [InlineData("05", true, 5)]
    [InlineData("5.0", true, 5)]
    [InlineData("5.5", false, 0)]
    [InlineData("0", false, 0)]
    [InlineData("61", false, 0)]
    [InlineData("abc", false, 0)]
    [InlineData("", false, 0)]
    [InlineData(null, false, 0)]
    public void TryParse_ValidatesRangeAndIntegers(string? text, bool expected, double expectedSeconds)
    {
        Assert.Equal(expected, RefreshInterval.TryParse(text, out double seconds));
        if (expected)
        {
            Assert.Equal(expectedSeconds, seconds);
        }
    }

    [Fact] // RI-04
    public void Describe_AddsCustomSuffixOnlyForNonPresets()
    {
        Assert.Equal("每 1 秒刷新", RefreshInterval.Describe(1));
        Assert.Equal("每 10 秒刷新", RefreshInterval.Describe(10));
        Assert.Equal("每 3 秒刷新 · 自定义", RefreshInterval.Describe(3));
        Assert.Equal("每 60 秒刷新 · 自定义", RefreshInterval.Describe(60));
    }

    [Fact] // RI-05
    public void DescribeShort_MatchesMenuItems()
    {
        Assert.Equal("每 1 秒", RefreshInterval.DescribeShort(1));
        Assert.Equal("每 10 秒", RefreshInterval.DescribeShort(10));
    }

    [Fact] // RI-06
    public void ValidationMessage_ContainsRange()
    {
        Assert.Contains("1–60", RefreshInterval.ValidationMessage, StringComparison.Ordinal);
    }
}
