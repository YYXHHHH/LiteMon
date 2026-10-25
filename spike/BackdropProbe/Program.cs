using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace BackdropProbe;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        string mode = Environment.GetEnvironmentVariable("PROBE_MODE") ?? "dwm";
        var app = new Application();
        var window = new ProbeWindow(mode);
        window.Show();

        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        int ticks = 0;
        var process = Process.GetCurrentProcess();
        TimeSpan startCpu = TimeSpan.Zero;
        var started = Stopwatch.StartNew();

        timer.Tick += (_, _) =>
        {
            ticks++;
            window.SetTick(ticks);
            if (ticks == 5)
            {
                process.Refresh();
                startCpu = process.TotalProcessorTime;
            }
            if (started.Elapsed < TimeSpan.FromSeconds(20))
            {
                return;
            }

            timer.Stop();
            process.Refresh();
            double cpu = (process.TotalProcessorTime - startCpu).TotalMilliseconds / ((started.Elapsed.TotalSeconds - 5) * 1000) * 100;
            Console.WriteLine($"mode={mode} cpu={cpu:F2}% ws={process.WorkingSet64 / 1048576.0:F0}MB {window.Diagnostic}");
            app.Shutdown();
        };
        timer.Start();
        app.Run();
    }
}

internal sealed class ProbeWindow : Window
{
    private readonly string _mode;
    private readonly TextBlock _text;
    private readonly TextBlock _status;

    public ProbeWindow(string mode)
    {
        _mode = mode;
        Title = "BackdropProbe";

        Width = 340;
        Height = 560;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = mode == "layered";
        int alpha = mode == "clear" ? 0x00 : 0xBF;
        Background = new SolidColorBrush(Color.FromArgb((byte)alpha, 0x0B, 0x0E, 0x14));

        Topmost = true;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = 200;
        Top = 120;

        var panel = new StackPanel { Margin = new Thickness(16) };
        _text = new TextBlock { Text = "probe", FontSize = 20, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White };
        _status = new TextBlock { Text = "init", FontSize = 12, Margin = new Thickness(0, 12, 0, 0), Foreground = Brushes.Gray };
        panel.Children.Add(_text);
        panel.Children.Add(_status);
        Content = panel;

        SourceInitialized += (_, _) => Apply();
    }

    public string Diagnostic { get; private set; } = "";

    public void SetTick(int tick) => _text.Text = "probe " + tick.ToString(CultureInfo.InvariantCulture);

    private void Apply()
    {
        var helper = new WindowInteropHelper(this);
        IntPtr hwnd = helper.Handle;

        if (_mode == "dwm")
        {
            int value = 3;
            int hr = DwmSetWindowAttribute(hwnd, 38, ref value, sizeof(int));
            MakeTransparent();
            Diagnostic = "dwm hr=0x" + hr.ToString("X8", CultureInfo.InvariantCulture);
        }
        else if (_mode == "accent" || _mode == "clear")
        {
            int ok = SetAccent(hwnd, unchecked((int)0xBF0B0E14));
            MakeTransparent();
            Diagnostic = "accent ok=" + ok.ToString(CultureInfo.InvariantCulture);
        }
        else if (_mode == "extend")
        {
            var margins = new Margins { Left = -1, Right = -1, Top = -1, Bottom = -1 };
            int hr = DwmExtendFrameIntoClientArea(hwnd, ref margins);
            MakeTransparent();
            Diagnostic = "extend hr=0x" + hr.ToString("X8", CultureInfo.InvariantCulture);
        }
        else
        {
            Diagnostic = "mode=" + _mode;
        }

        _status.Text = Diagnostic;
    }

    private void MakeTransparent()
    {
        if (PresentationSource.FromVisual(this) is HwndSource source && source.CompositionTarget is not null)
        {
            source.CompositionTarget.BackgroundColor = Colors.Transparent;
        }
    }

    private static int SetAccent(IntPtr hwnd, int gradient)
    {
        int size = Marshal.SizeOf<AccentPolicy>();
        IntPtr buffer = Marshal.AllocHGlobal(size);
        try
        {
            var policy = new AccentPolicy { AccentState = 4, AccentFlags = 2, GradientColor = gradient };
            Marshal.StructureToPtr(policy, buffer, false);
            var data = new CompositionAttributeData { Attribute = 19, Data = buffer, SizeOfData = size };
            return SetWindowCompositionAttribute(hwnd, ref data);
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

    [StructLayout(LayoutKind.Sequential)]
    private struct Margins
    {
        public int Left;
        public int Right;
        public int Top;
        public int Bottom;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    [DllImport("dwmapi.dll")]
    private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref Margins margins);

    [DllImport("user32.dll")]
    private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref CompositionAttributeData data);
}
