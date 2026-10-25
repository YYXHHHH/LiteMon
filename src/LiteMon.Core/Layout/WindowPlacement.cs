namespace LiteMon.Core.Layout;

/// <summary>
/// 窗口位置计算（纯函数，不依赖 WPF）。
/// 负责把"上次保存的位置"收敛到当前虚拟屏幕内，避免换分辨率/拔副屏后窗口丢失。
/// </summary>
public static class WindowPlacement
{
    /// <summary>默认位置与屏幕边缘的间距（DIP）。</summary>
    public const double DefaultMargin = 24;

    /// <summary>
    /// 计算启动时应使用的左上角坐标。
    /// </summary>
    /// <param name="left">保存的 X（null 表示没保存过）。</param>
    /// <param name="top">保存的 Y（null 表示没保存过）。</param>
    /// <param name="width">窗口宽度（DIP）。</param>
    /// <param name="height">窗口高度（DIP）。</param>
    /// <param name="screenLeft">虚拟屏幕左边界（多显示器时可能为负）。</param>
    /// <param name="screenTop">虚拟屏幕上边界。</param>
    /// <param name="screenWidth">虚拟屏幕宽度。</param>
    /// <param name="screenHeight">虚拟屏幕高度。</param>
    /// <returns>保证落在屏幕内的左上角坐标。</returns>
    public static (double Left, double Top) Clamp(
        double? left,
        double? top,
        double width,
        double height,
        double screenLeft,
        double screenTop,
        double screenWidth,
        double screenHeight)
    {
        if (!double.IsFinite(width) || width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "窗口宽度必须为正数。");
        }

        if (!double.IsFinite(height) || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "窗口高度必须为正数。");
        }

        if (!double.IsFinite(screenWidth) || screenWidth <= 0)
        {
            screenWidth = width + (2 * DefaultMargin);
        }

        if (!double.IsFinite(screenHeight) || screenHeight <= 0)
        {
            screenHeight = height + (2 * DefaultMargin);
        }

        double fallbackLeft = screenLeft + screenWidth - width - DefaultMargin;
        double fallbackTop = screenTop + DefaultMargin;

        double x = left is { } savedLeft && double.IsFinite(savedLeft) ? savedLeft : fallbackLeft;
        double y = top is { } savedTop && double.IsFinite(savedTop) ? savedTop : fallbackTop;

        double maxLeft = screenLeft + Math.Max(0, screenWidth - width);
        double maxTop = screenTop + Math.Max(0, screenHeight - height);

        return (Math.Clamp(x, screenLeft, maxLeft), Math.Clamp(y, screenTop, maxTop));
    }
}
