using System.Diagnostics;

namespace WinFormsProbe;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        var form = new Form
        {
            Width = 320,
            Height = 460,
            FormBorderStyle = FormBorderStyle.None,
            ShowInTaskbar = false,
            BackColor = System.Drawing.Color.FromArgb(0x0B, 0x0E, 0x14),
        };

        var timer = new System.Windows.Forms.Timer { Interval = 5000 };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var p = Process.GetCurrentProcess();
            Console.WriteLine("=== WinForms baseline window ===");
            Console.WriteLine($"working set   : {p.WorkingSet64 / 1048576.0:F1} MB (private {p.PrivateMemorySize64 / 1048576.0:F1} MB)");
            Application.Exit();
        };
        timer.Start();
        Application.Run(form);
    }
}
