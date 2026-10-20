using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using LiteMon.Core.Snapshots;
using LiteMon.Sensors;

namespace LiteMon.App;

/// <summary>无边框置顶面板：拖动移动、右上角 × 或 Esc 退出、右键菜单退出。</summary>
public sealed class PanelWindow : Window
{
    private readonly PanelView _view;

    public void SetDevices(DeviceInfo devices) => _view.SetDevices(devices);

    public PanelWindow(SnapshotBus bus, DeviceInfo devices)
    {
        Title = "LiteMon";
        Width = 340;
        Height = 560;
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
        _view = new PanelView(bus, devices);
        Content = _view;

        MouseLeftButtonDown += OnMouseLeftButtonDown;
        KeyDown += OnKeyDown;

        var menu = new ContextMenu();

        if (!ElevationHelper.IsElevated)
        {
            var elevate = new MenuItem { Header = "以管理员身份重启（读取内核温度）" };
            elevate.Click += OnRestartElevated;
            menu.Items.Add(elevate);
            menu.Items.Add(new Separator());
        }

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

    private void OnRestartElevated(object sender, RoutedEventArgs e)
    {
        MessageBoxResult result = MessageBox.Show(
            "读取 CPU 内核温度需要管理员权限（加载签名驱动 PawnIO）。\n\n现在以管理员身份重启 LiteMon？",
            "LiteMon",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            ElevationHelper.RestartElevated();
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
