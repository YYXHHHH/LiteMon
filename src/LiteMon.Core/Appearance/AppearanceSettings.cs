namespace LiteMon.Core.Appearance;

/// <summary>面板布局。</summary>
public enum PanelLayout
{
    /// <summary>竖版详细（分组 + 多行 + 进度条）。</summary>
    Detailed = 0,

    /// <summary>横版紧凑（单行条）。</summary>
    Compact = 1,
}

/// <summary>面板显示的硬件部件（位掩码，可组合）。</summary>
[Flags]
public enum PanelGroup
{
    None = 0,
    Cpu = 1,
    Gpu = 2,
    Memory = 4,
    Disk = 8,
    Network = 16,
    System = 32,
    All = Cpu | Gpu | Memory | Disk | Network | System,
}

/// <summary>
/// 外观设置：布局 / 透明度 / 进度条 / 动画 / 可见部件。
/// 默认值刻意选择"零额外开销"：不透明、全选、无动画。
/// </summary>
public sealed record AppearanceSettings
{
    public const int MinOpacity = 45;
    public const int MaxOpacity = 100;
    public const int OpacityStep = 5;

    /// <summary>透明度档位（菜单顺序）。100 = 不透明。</summary>
    public static IReadOnlyList<int> OpacityPresets { get; } = [100, 90, 75, 60, 45];

    public PanelLayout Layout { get; init; } = PanelLayout.Detailed;

    public int OpacityPercent { get; init; } = MaxOpacity;

    public bool ShowBars { get; init; } = true;

    /// <summary>数值平滑过渡。默认 false：省 CPU（每秒少约 9 帧重绘），数字更锐。</summary>
    public bool SmoothAnimation { get; init; }

    /// <summary>显示哪些部件（默认全选）。</summary>
    public PanelGroup VisibleGroups { get; init; } = PanelGroup.All;

    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsTransparent => OpacityPercent < MaxOpacity;

    public bool IsVisible(PanelGroup group) => (VisibleGroups & group) == group;

    public AppearanceSettings Normalized()
    {
        int opacity = NormalizeOpacity(OpacityPercent);
        bool bars = Layout == PanelLayout.Compact ? false : ShowBars;
        // 透明窗口是分层窗口（软件渲染），动画会成倍放大开销：强制关闭
        bool animation = opacity < MaxOpacity ? false : SmoothAnimation;
        PanelGroup groups = VisibleGroups & PanelGroup.All;
        if (groups == PanelGroup.None)
        {
            groups = PanelGroup.All;   // 防呆：不允许一个部件都不显示
        }

        return this with { OpacityPercent = opacity, ShowBars = bars, SmoothAnimation = animation, VisibleGroups = groups };
    }

    /// <summary>把透明度吸附到 5 的倍数并夹在 [45, 100]。</summary>
    public static int NormalizeOpacity(int value)
    {
        int clamped = Math.Clamp(value, MinOpacity, MaxOpacity);
        double steps = (clamped - MinOpacity) / (double)OpacityStep;
        int snapped = (int)(Math.Round(steps, MidpointRounding.AwayFromZero) * OpacityStep) + MinOpacity;
        return Math.Clamp(snapped, MinOpacity, MaxOpacity);
    }
}
