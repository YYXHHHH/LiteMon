using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace LiteMon.App;

/// <summary>
/// 让面板"透出桌面"而不使用分层窗口（分层会让 WPF 退回软件渲染）。
/// 做法：HwndTarget 背景设为透明 + DWM 亚克力（SetWindowCompositionAttribute）。
/// 不透明模式不调用这里任何代码，零额外开销。
/// </summary>
internal static class WindowBackdrop
{
    private const int WcaAccentPolicy = 19;
    private const int AccentEnableAcrylicBlurBehind = 4;

    private static bool _supported = true;

    /// <summary>当前系统是否支持透明（首次调用失败后置 false）。</summary>
    public static bool IsSupported => _supported;

    /// <summary>启用透明：先让 HwndTarget 透明，再叠加亚克力。</summary>
    public static bool TryEnable(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        if (!_supported)
        {
            return false;
        }

        IntPtr hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return false;
        }

        if (PresentationSource.FromVisual(window) is HwndSource source && source.CompositionTarget is not null)
        {
            source.CompositionTarget.BackgroundColor = Colors.Transparent;
        }

        if (SetAccent(hwnd) != 0)
        {
            return true;
        }

        _supported = false;
        return false;
    }

    /// <summary>关闭透明：清掉亚克力并恢复不透明合成背景。</summary>
    public static void Disable(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        IntPtr hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd != IntPtr.Zero)
        {
            ClearAccent(hwnd);
        }

        if (PresentationSource.FromVisual(window) is HwndSource source && source.CompositionTarget is not null)
        {
            source.CompositionTarget.BackgroundColor = Colors.Black;
        }
    }

    private static int SetAccent(IntPtr hwnd)
    {
        int size = Marshal.SizeOf<AccentPolicy>();
        IntPtr buffer = Marshal.AllocHGlobal(size);
        try
        {
            // 渐变全透明：由 WPF 的背景画刷决定深浅，亚克力只负责把背后内容虚化
            var policy = new AccentPolicy
            {
                AccentState = AccentEnableAcrylicBlurBehind,
                AccentFlags = 2,
                GradientColor = 0,
            };
            Marshal.StructureToPtr(policy, buffer, false);
            var data = new CompositionAttributeData { Attribute = WcaAccentPolicy, Data = buffer, SizeOfData = size };
            return SetWindowCompositionAttribute(hwnd, ref data);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static void ClearAccent(IntPtr hwnd)
    {
        int size = Marshal.SizeOf<AccentPolicy>();
        IntPtr buffer = Marshal.AllocHGlobal(size);
        try
        {
            var policy = new AccentPolicy { AccentState = 0, AccentFlags = 0, GradientColor = 0 };
            Marshal.StructureToPtr(policy, buffer, false);
            var data = new CompositionAttributeData { Attribute = WcaAccentPolicy, Data = buffer, SizeOfData = size };
            _ = SetWindowCompositionAttribute(hwnd, ref data);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AccentPolicy
    {
        public int AccentState;
        public int AccentFlags;
        public int GradientColor;
        public int AnimationId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CompositionAttributeData
    {
        public int Attribute;
        public IntPtr Data;
        public int SizeOfData;
    }

    [DllImport("user32.dll")]
    private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref CompositionAttributeData data);
}
