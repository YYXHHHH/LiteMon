using LiteMon.Core.Appearance;

namespace LiteMon.Core.Tests;

public class AppearanceTests
{
    [Theory] // RI-40
    [InlineData(100, 100)]
    [InlineData(95, 95)]
    [InlineData(73, 75)]
    [InlineData(10, 45)]
    [InlineData(300, 100)]
    public void NormalizeOpacity_SnapsAndClamps(int input, int expected)
        => Assert.Equal(expected, AppearanceSettings.NormalizeOpacity(input));

    [Fact] // RI-41
    public void Compact_ForcesBarsOff()
    {
        var normalized = new AppearanceSettings { Layout = PanelLayout.Compact, ShowBars = true }.Normalized();
        Assert.False(normalized.ShowBars);
    }

    [Fact] // RI-42
    public void EmptyVisibleGroups_FallsBackToAll()
    {
        var normalized = new AppearanceSettings { VisibleGroups = PanelGroup.None }.Normalized();
        Assert.Equal(PanelGroup.All, normalized.VisibleGroups);
    }

    [Fact] // RI-43
    public void UnknownBits_AreMaskedOut()
    {
        var normalized = new AppearanceSettings { VisibleGroups = (PanelGroup)0x7FFF }.Normalized();
        Assert.Equal(PanelGroup.All, normalized.VisibleGroups);
    }

    [Fact] // RI-44
    public void Defaults_AreZeroCost()
    {
        var defaults = new AppearanceSettings();
        Assert.Equal(PanelLayout.Detailed, defaults.Layout);
        Assert.Equal(100, defaults.OpacityPercent);
        Assert.True(defaults.ShowBars);
        Assert.False(defaults.SmoothAnimation);
        Assert.Equal(PanelGroup.All, defaults.VisibleGroups);
        Assert.False(defaults.IsTransparent);
    }

    [Fact] // RI-49
    public void Transparent_ForcesAnimationOff()
    {
        var normalized = new AppearanceSettings { OpacityPercent = 45, SmoothAnimation = true }.Normalized();
        Assert.False(normalized.SmoothAnimation);
        Assert.True(normalized.IsTransparent);

        var opaque = new AppearanceSettings { OpacityPercent = 100, SmoothAnimation = true }.Normalized();
        Assert.True(opaque.SmoothAnimation);
    }

    [Fact] // RI-45
    public void IsVisible_UsesBitmask()
    {
        var appearance = new AppearanceSettings { VisibleGroups = PanelGroup.Cpu | PanelGroup.Gpu };
        Assert.True(appearance.IsVisible(PanelGroup.Cpu));
        Assert.True(appearance.IsVisible(PanelGroup.Gpu));
        Assert.False(appearance.IsVisible(PanelGroup.Memory));
    }

    [Fact] // RI-46
    public void CompactWidth_IsSumOfVisibleCells()
    {
        Assert.Equal(728, PanelMetrics.GetSize(PanelLayout.Compact, PanelGroup.All).Width);
        Assert.Equal(320, PanelMetrics.GetSize(PanelLayout.Compact, PanelGroup.Cpu | PanelGroup.Gpu).Width);
        Assert.Equal(208, PanelMetrics.GetSize(PanelLayout.Compact, PanelGroup.Cpu).Width);
        Assert.Equal(44, PanelMetrics.GetSize(PanelLayout.Compact, PanelGroup.All).Height);
    }

    [Fact] // RI-47
    public void DetailHeight_ShrinksWithVisibleGroups()
    {
        Assert.Equal(564, PanelMetrics.GetSize(PanelLayout.Detailed, PanelGroup.All).Height);
        Assert.Equal(298, PanelMetrics.GetSize(PanelLayout.Detailed, PanelGroup.Cpu | PanelGroup.Gpu).Height);
        Assert.Equal(182, PanelMetrics.GetSize(PanelLayout.Detailed, PanelGroup.Cpu).Height);
        Assert.Equal(340, PanelMetrics.GetSize(PanelLayout.Detailed, PanelGroup.All).Width);
    }

    [Fact] // RI-48
    public void RowCounts_MatchDetailedLayout()
    {
        Assert.Equal(12, PanelMetrics.GroupOrder.Sum(group => PanelMetrics.RowCount(group)));
        Assert.Equal(3, PanelMetrics.RowCount(PanelGroup.Cpu));
        Assert.Equal(1, PanelMetrics.RowCount(PanelGroup.System));
    }
}
