using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using LiteMon.Core.Snapshots;

namespace LiteMon.App;

/// <summary>无边框置顶面板：拖动移动、右上角 × 或 Esc 退出、右键菜单退出。</summary>
public sealed class PanelWindow : Window
{
    public PanelWindow(SnapshotBus bus)
    {
        Title = "LiteMon";
        Width = 320;
        Height = 500;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        // 关闭 AllowsTransparency：分层窗口会让 WPF 退回软件渲染，CPU 会飙升到 10%+
        AllowsTransparency = false;
        Background = new SolidColorBrush(Color.FromRgb(0x0B, 0x0E, 0x14));
        Topmost = true;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.Manual;

        Left = Math.Max(0, SystemParameters.WorkArea.Right - Width - 24);
        Top = SystemParameters.WorkArea.Top + 24;

        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Stretch;
        Content = new PanelView(bus);

        MouseLeftButtonDown += OnMouseLeftButtonDown;
        KeyDown += OnKeyDown;

        var menu = new ContextMenu();
        var exit = new MenuItem { Header = "退出 LiteMon" };
        exit.Click += (_, _) => Close();
        menu.Items.Add(exit);
        ContextMenu = menu;

        Loaded += (_, _) => Focus();
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        Point position = e.GetPosition(this);
        if (position.X > Width - 44 && position.Y < 40)
        {
            Close();
            return;
        }

        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
            // 鼠标已释放时 DragMove 会抛异常，忽略
        }
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
        }
    }
}
