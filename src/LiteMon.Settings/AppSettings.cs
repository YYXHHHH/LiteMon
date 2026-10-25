using System.Globalization;
using System.Text.Json.Serialization;
using LiteMon.Core.Appearance;
using LiteMon.Core.Sensors;

namespace LiteMon.Settings;

/// <summary>
/// 持久化设置。字段只增不改：新增设置给默认值即可向后兼容旧配置文件。
/// </summary>
public sealed record AppSettings
{
    public const int CurrentVersion = 2;

    public int Version { get; init; } = CurrentVersion;

    public double RefreshIntervalSeconds { get; init; } = RefreshInterval.DefaultSeconds;

    /// <summary>窗口是否固定在当前位置（固定后不可拖动）。</summary>
    public bool IsPinned { get; init; }

    /// <summary>窗口左上角 X（DIP）；null 表示沿用默认位置。</summary>
    public double? WindowLeft { get; init; }

    /// <summary>窗口左上角 Y（DIP）；null 表示沿用默认位置。</summary>
    public double? WindowTop { get; init; }

    /// <summary>外观（布局 / 透明度 / 进度条 / 动画 / 可见部件）。</summary>
    public AppearanceSettings Appearance { get; init; } = new();

    /// <summary>默认设置（1 秒）。</summary>
    public static AppSettings Default { get; } = new();

    /// <summary>刷新间隔的强类型形式（派生量，不落盘）。</summary>
    [JsonIgnore]
    public TimeSpan Interval => TimeSpan.FromSeconds(RefreshIntervalSeconds);

    /// <summary>把任意来源的值收敛到合法区间（手改配置、旧版本字段都可能越界）。</summary>
    public AppSettings Normalized()
        => this with
        {
            Version = CurrentVersion,
            RefreshIntervalSeconds = RefreshInterval.Normalize(RefreshIntervalSeconds),
            Appearance = Appearance.Normalized(),
        };

    public override string ToString()
        => string.Create(CultureInfo.InvariantCulture, $"v{Version} {RefreshIntervalSeconds}s");
}
