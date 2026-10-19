namespace LiteMon.Core.Metrics;

/// <summary>指标状态。禁止用 0 或异常表示"读不到"。</summary>
public enum MetricStatus : byte
{
    /// <summary>本轮正常读数。</summary>
    Ok = 0,

    /// <summary>传感器不存在、无权限或驱动未加载。</summary>
    Unavailable = 1,

    /// <summary>上一轮有值，但本轮失败且尚未超过过期阈值。</summary>
    Stale = 2,

    /// <summary>连续失败，已标记为错误。</summary>
    Error = 3,
}
