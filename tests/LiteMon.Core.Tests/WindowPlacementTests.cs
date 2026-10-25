using LiteMon.Core.Layout;

namespace LiteMon.Core.Tests;

public class WindowPlacementTests
{
    private const double W = 340;
    private const double H = 560;

    [Fact] // RI-30
    public void Clamp_WithoutSavedPosition_UsesTopRightDefault()
    {
        var (left, top) = WindowPlacement.Clamp(null, null, W, H, 0, 0, 1920, 1080);

        Assert.Equal(1920 - W - 24, left);
        Assert.Equal(24, top);
    }

    [Fact] // RI-31
    public void Clamp_InsideBounds_KeepsPosition()
    {
        var (left, top) = WindowPlacement.Clamp(500, 300, W, H, 0, 0, 1920, 1080);

        Assert.Equal(500, left);
        Assert.Equal(300, top);
    }

    [Fact] // RI-32
    public void Clamp_OutsideBounds_PullsBackInside()
    {
        var (left, top) = WindowPlacement.Clamp(9000, -500, W, H, 0, 0, 1920, 1080);

        Assert.Equal(1920 - W, left);
        Assert.Equal(0, top);
    }

    [Fact] // RI-33
    public void Clamp_SecondaryScreenOnLeft_AcceptsNegativeCoordinates()
    {
        var (left, top) = WindowPlacement.Clamp(-1600, 100, W, H, -1920, 0, 3840, 1080);

        Assert.Equal(-1600, left);
        Assert.Equal(100, top);
    }

    [Fact] // RI-34
    public void Clamp_WindowLargerThanScreen_AlignsToTopLeft()
    {
        var (left, top) = WindowPlacement.Clamp(100, 100, 800, 900, 0, 0, 640, 480);

        Assert.Equal(0, left);
        Assert.Equal(0, top);
    }

    [Theory] // RI-35
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Clamp_NonFiniteSavedValue_FallsBackToDefault(double bad)
    {
        var (left, _) = WindowPlacement.Clamp(bad, bad, W, H, 0, 0, 1920, 1080);

        Assert.Equal(1920 - W - 24, left);
    }
}
