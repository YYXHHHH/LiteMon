namespace LiteMon.Core.Metrics;

/// <summary>
/// 一次采样得到的完整快照。构造后不可变，可安全跨线程读取。
/// 不变量：Metrics.Length == (int)MetricId.Count，且索引即 ID。
/// </summary>
public sealed class Snapshot
{
    public static readonly Snapshot Empty = CreateEmpty();

    private static Snapshot CreateEmpty()
    {
        var slots = new MetricSample[(int)MetricId.Count];
        for (int i = 0; i < slots.Length; i++)
        {
            slots[i] = new MetricSample((MetricId)i, 0, MetricStatus.Unavailable, 0);
        }

        return new Snapshot(0, 0, slots);
    }

    private readonly MetricSample[] _metrics;

    public Snapshot(long sequence, long timestampMs, MetricSample[] metrics)
    {
        ArgumentNullException.ThrowIfNull(metrics);
        if (metrics.Length != (int)MetricId.Count)
        {
            throw new ArgumentException(
                $"快照必须包含 {(int)MetricId.Count} 个槽位，实际为 {metrics.Length}。",
                nameof(metrics));
        }

        _metrics = metrics;
        Sequence = sequence;
        TimestampMs = timestampMs;
    }

    /// <summary>单调递增的采样序号，用于判断"是否是新一帧"。</summary>
    public long Sequence { get; }

    /// <summary>采样时间（单调时钟毫秒）。</summary>
    public long TimestampMs { get; }

    public IReadOnlyList<MetricSample> All => _metrics;

    public MetricSample Get(MetricId id)
    {
        int index = (int)id;
        return index >= 0 && index < _metrics.Length ? _metrics[index] : default;
    }
}
