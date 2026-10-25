namespace LiteMon.Core.Appearance;

/// <summary>面板几何：两种布局的尺寸、单元宽度、按可见部件计算窗口大小。</summary>
public static class PanelMetrics
{
    public const double DetailWidth = 340;
    public const double DetailHeader = 42;
    public const double DetailFooter = 24;
    public const double DetailGroupHeader = 17;
    public const double DetailRowHeight = 33;

    public const double CompactHeight = 44;
    public const double CompactPadding = 12;
    public const double CompactButtonZone = 72;

    public const double CompactCellCpu = 112;
    public const double CompactCellGpu = 112;
    public const double CompactCellMemory = 88;
    public const double CompactCellDisk = 112;
    public const double CompactCellNetwork = 112;
    public const double CompactCellSystem = 96;

    /// <summary>绘制顺序（也是菜单顺序）。</summary>
    public static IReadOnlyList<PanelGroup> GroupOrder { get; } =
        [PanelGroup.Cpu, PanelGroup.Gpu, PanelGroup.Memory, PanelGroup.Disk, PanelGroup.Network, PanelGroup.System];

    public static int RowCount(PanelGroup group) => group switch
    {
        PanelGroup.Cpu => 3,
        PanelGroup.Gpu => 3,
        PanelGroup.Memory => 1,
        PanelGroup.Disk => 2,
        PanelGroup.Network => 2,
        PanelGroup.System => 1,
        _ => 0,
    };

    public static double CellWidth(PanelGroup group) => group switch
    {
        PanelGroup.Cpu => CompactCellCpu,
        PanelGroup.Gpu => CompactCellGpu,
        PanelGroup.Memory => CompactCellMemory,
        PanelGroup.Disk => CompactCellDisk,
        PanelGroup.Network => CompactCellNetwork,
        PanelGroup.System => CompactCellSystem,
        _ => 0,
    };

    public static string Label(PanelGroup group) => group switch
    {
        PanelGroup.Cpu => "CPU",
        PanelGroup.Gpu => "GPU",
        PanelGroup.Memory => "内存",
        PanelGroup.Disk => "磁盘",
        PanelGroup.Network => "网络",
        PanelGroup.System => "系统",
        _ => string.Empty,
    };

    /// <summary>按布局与可见部件计算窗口尺寸。</summary>
    public static (double Width, double Height) GetSize(PanelLayout layout, PanelGroup visible)
    {
        PanelGroup groups = visible & PanelGroup.All;

        if (layout == PanelLayout.Compact)
        {
            double width = (CompactPadding * 2) + CompactButtonZone;
            foreach (var group in GroupOrder)
            {
                if ((groups & group) == group)
                {
                    width += CellWidth(group);
                }
            }

            return (width, CompactHeight);
        }

        int visibleGroups = 0;
        int rows = 0;
        foreach (var group in GroupOrder)
        {
            if ((groups & group) == group)
            {
                visibleGroups++;
                rows += RowCount(group);
            }
        }

        double height = DetailHeader + DetailFooter + (visibleGroups * DetailGroupHeader) + (rows * DetailRowHeight);
        return (DetailWidth, height);
    }
}
