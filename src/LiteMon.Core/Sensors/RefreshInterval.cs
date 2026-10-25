using System.Globalization;

namespace LiteMon.Core.Sensors;

/// <summary>
/// 刷新间隔的取值域、归一化与展示文案。
/// UI、设置持久化、采样器共用这一份规则，避免三处各写一套边界。
/// </summary>
public static class RefreshInterval
{
    /// <summary>默认间隔（秒）。</summary>
    public const double DefaultSeconds = 1;

    /// <summary>最小间隔（秒）。低于 1 秒时 PDH/LHM 数值失真、CPU 占用翻倍。</summary>
    public const double MinSeconds = 1;

    /// <summary>最大间隔（秒）。再大就没有"实时监控"的意义了。</summary>
    public const double MaxSeconds = 60;

    /// <summary>右键菜单里的预设档位（秒），顺序即菜单顺序。</summary>
    public static readonly IReadOnlyList<double> Presets = [1, 2, 5, 10];

    /// <summary>把任意来源的秒数收敛到 [1, 60] 的整数。</summary>
    public static double Normalize(double seconds)
    {
        if (double.IsNaN(seconds))
        {
            return DefaultSeconds;
        }

        double rounded = Math.Round(seconds, MidpointRounding.AwayFromZero);
        return Math.Clamp(rounded, MinSeconds, MaxSeconds);
    }

    public static bool IsPreset(double seconds) => Presets.Contains(Normalize(seconds));

    /// <summary>解析用户输入：只接受 [1, 60] 的整数（允许 "5" "5.0"，拒绝 "5.5" "abc" "0" "61"）。</summary>
    public static bool TryParse(string? text, out double seconds)
    {
        seconds = 0;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        if (!double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed))
        {
            return false;
        }

        if (!double.IsFinite(parsed) || parsed < MinSeconds || parsed > MaxSeconds)
        {
            return false;
        }

        if (parsed != Math.Round(parsed))
        {
            return false;
        }

        seconds = parsed;
        return true;
    }

    /// <summary>面板头部文案，例如 "每 5 秒刷新"、"每 3 秒刷新 · 自定义"。</summary>
    public static string Describe(double seconds)
    {
        double value = Normalize(seconds);
        string text = "每 " + Number(value) + " 秒刷新";
        return IsPreset(value) ? text : text + " · 自定义";
    }

    /// <summary>菜单文案，例如 "每 5 秒"。</summary>
    public static string DescribeShort(double seconds) => "每 " + Number(Normalize(seconds)) + " 秒";

    /// <summary>对话框提示文案。</summary>
    public static string PromptText => "刷新间隔（秒），范围 " + RangeText;

    /// <summary>对话框校验失败文案。</summary>
    public static string ValidationMessage => "请输入 " + RangeText + " 之间的整数秒";

    private static string RangeText
        => string.Create(CultureInfo.InvariantCulture, $"{MinSeconds:0}–{MaxSeconds:0}");

    private static string Number(double seconds)
        => seconds.ToString("0", CultureInfo.InvariantCulture);
}
