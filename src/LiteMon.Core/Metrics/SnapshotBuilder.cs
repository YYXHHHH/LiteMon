using LiteMon.Core.Sensors;

namespace LiteMon.Core.Metrics;

/// <summary>
/// 采样构建器。只在采样线程使用，不做线程同步。
/// 每次 <see cref="Build"/> 产生一个不可变快照；未写入的槽位保持 Unavailable。
/// </summary>
public sealed class SnapshotBuilder : IMetricWriter
{
    /// <summary>默认陈旧阈值，必须大于最慢采样档 10 s；采样器会按刷新间隔放大后传入。</summary>
    public const long StaleAfterMs = 12_000;

    private readonly MetricSample[] _slots = new MetricSample[(int)MetricId.Count];
    private long _nowMs;
    private long _sequence;

    public SnapshotBuilder()
    {
        for (int i = 0; i < _slots.Length; i++)
        {
            _slots[i] = new MetricSample((MetricId)i, 0, MetricStatus.Unavailable, 0);
        }
    }

    /// <summary>开始新一轮采样（单调时钟毫秒）。</summary>
    public void Begin(long nowMs)
    {
        if (nowMs < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(nowMs), "时间戳不能为负数。");
        }

        _nowMs = nowMs;
    }

    public void Write(MetricId id, double value, MetricStatus status = MetricStatus.Ok)
    {
        _slots[(int)id] = new MetricSample(id, value, status, _nowMs);
    }

    public void MarkUnavailable(MetricId id)
    {
        _slots[(int)id] = new MetricSample(id, 0, MetricStatus.Unavailable, _nowMs);
    }

    public void MarkUnavailable(IEnumerable<MetricId> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);
        foreach (var id in ids)
        {
            MarkUnavailable(id);
        }
    }

    /// <summary>构建不可变快照：使用默认陈旧阈值 <see cref="StaleAfterMs"/>。</summary>
    public Snapshot Build() => Build(StaleAfterMs);

    /// <summary>
    /// 构建不可变快照：把超过 <paramref name="staleAfterMs"/> 未更新的 Ok 值标为 Stale。
    /// 采样器会传入 max(默认值, 3 × 刷新间隔)，避免用户把间隔调大后误判过期。
    /// </summary>
    public Snapshot Build(long staleAfterMs)
    {
        if (staleAfterMs <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(staleAfterMs), "陈旧阈值必须为正数。");
        }

        var copy = (MetricSample[])_slots.Clone();

        for (int i = 0; i < copy.Length; i++)
        {
            var sample = copy[i];
            if (sample.Status == MetricStatus.Ok && _nowMs - sample.TimestampMs > staleAfterMs)
            {
                copy[i] = sample with { Status = MetricStatus.Stale };
            }
        }

        return new Snapshot(_sequence++, _nowMs, copy);
    }
}
