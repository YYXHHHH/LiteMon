using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using LiteMon.Core.Sensors;
using LiteMon.Core.Snapshots;
using LiteMon.Sensors;

namespace LiteMon.App;

/// <summary>
/// 无边框置顶面板：拖动移动、右上角 × 或 Esc 退出、右键菜单切换刷新间隔/提权/退出。
/// </summary>
public sealed class PanelWindow : Window
{
    private readonly PanelView _view;
    private readonly Func<double, double> _applyInterval;
    private readonly List<MenuItem> _presetItems = [];

    private MenuItem? _customItem;
    private double _intervalSeconds;

    public PanelWindow(SnapshotBus bus, DeviceInfo devices, double intervalSeconds, Func<double, double> applyInterval)
    {
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(applyInterval);

        _applyInterval = applyInterval;
        _intervalSeconds = RefreshInterval.Normalize(intervalSeconds);

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
        _view.SetRefreshInterval(_intervalSeconds);
        Content = _view;

        MouseLeftButtonDown += OnMouseLeftButtonDown;
        KeyDown += OnKeyDown;

        var menu = new ContextMenu();
        menu.Items.Add(BuildIntervalMenu());

        if (!ElevationHelper.IsElevated)
        {
            menu.Items.Add(new Separator());
            var elevate = new MenuItem { Header = "以管理员身份重启（读取内核温度）" };
            elevate.Click += OnRestartElevated;
            menu.Items.Add(elevate);
        }

        menu.Items.Add(new Separator());
        var exit = new MenuItem { Header = "退出 LiteMon" };
        exit.Click += (_, _) => Close();
        menu.Items.Add(exit);

        ContextMenu = menu;

        Loaded += (_, _) => Focus();
    }

    public void SetDevices(DeviceInfo devices) => _view.SetDevices(devices);

    private MenuItem BuildIntervalMenu()
    {
        var root = new MenuItem { Header = "刷新间隔" };

        foreach (double preset in RefreshInterval.Presets)
        {
            var item = new MenuItem
            {
                Header = RefreshInterval.DescribeShort(preset),
                IsCheckable = true,
                Tag = preset,
            };
            item.Click += OnPresetClicked;
            _presetItems.Add(item);
            root.Items.Add(item);
        }

        root.Items.Add(new Separator());

        _customItem = new MenuItem { Header = "自定义…", IsCheckable = true };
        _customItem.Click += OnCustomClicked;
        root.Items.Add(_customItem);

        UpdateIntervalChecks();
        return root;
    }

    private void OnPresetClicked(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: double seconds })
        {
            ApplyInterval(seconds);
        }
    }

    private void OnCustomClicked(object sender, RoutedEventArgs e)
    {
        // 菜单关闭会抢焦点，延后到下一轮消息泵再弹模态框
        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(ShowCustomDialog));
    }

    private void ShowCustomDialog()
    {
        var dialog = new RefreshIntervalDialog(_intervalSeconds) { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            ApplyInterval(dialog.Seconds);
        }
        else
        {
            UpdateIntervalChecks();   // 取消后把勾选态恢复成真实值
        }
    }

    private void ApplyInterval(double seconds)
    {
        double applied = _applyInterval(seconds);

        if (Math.Abs(applied - _intervalSeconds) > 0.001)
        {
            _intervalSeconds = applied;
            _view.SetRefreshInterval(applied);
        }

        UpdateIntervalChecks();
    }

    private void UpdateIntervalChecks()
    {
        foreach (var item in _presetItems)
        {
            item.IsChecked = item.Tag is double value && Math.Abs(value - _intervalSeconds) < 0.001;
        }

        if (_customItem is not null)
        {
            _customItem.IsChecked = !RefreshInterval.IsPreset(_intervalSeconds);
        }
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
