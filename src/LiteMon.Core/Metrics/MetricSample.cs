namespace LiteMon.Core.Metrics;

/// <summary>单个指标的采样值。不可变。</summary>
public readonly record struct MetricSample(MetricId Id, double Value, MetricStatus Status, long TimestampMs);
