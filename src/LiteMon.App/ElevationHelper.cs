using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;

namespace LiteMon.App;

internal static class ElevationHelper
{
    [DllImport("shell32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsUserAnAdmin();

    public static bool IsElevated => IsUserAnAdmin();

    /// <summary>以管理员身份重启自身（触发 UAC）。成功后当前实例退出。</summary>
    public static bool RestartElevated()
    {
        string? exe = Environment.ProcessPath;
        if (exe is null)
        {
            return false;
        }

        var startInfo = new ProcessStartInfo(exe)
        {
            UseShellExecute = true,
            Verb = "runas",
            WorkingDirectory = AppContext.BaseDirectory,
        };

        try
        {
            Process.Start(startInfo);
            Application.Current.Shutdown();
            return true;
        }
        catch (Win32Exception)
        {
            // 用户取消 UAC
            return false;
        }
    }
}
