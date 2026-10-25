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
    public const double PanelWidth = 340;
    public const double PanelHeight = 560;

    private const double HeaderHeight = 40;
    private const double CloseZoneWidth = 44;
    private const double PinZoneWidth = 28;

    private readonly PanelView _view;
    private readonly Func<double, double> _applyInterval;
    private readonly Action<bool> _onPinnedChanged;
    private readonly Action<double, double> _onMoved;
    private readonly List<MenuItem> _presetItems = [];

    private MenuItem? _customItem;
    private double _intervalSeconds;
    private bool _isPinned;

    public PanelWindow(
        SnapshotBus bus,
        DeviceInfo devices,
        double intervalSeconds,
        Func<double, double> applyInterval,
        double left,
        double top,
        bool isPinned,
        Action<bool> onPinnedChanged,
        Action<double, double> onMoved)
    {
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(applyInterval);
        ArgumentNullException.ThrowIfNull(onPinnedChanged);
        ArgumentNullException.ThrowIfNull(onMoved);

        _applyInterval = applyInterval;
        _onPinnedChanged = onPinnedChanged;
        _onMoved = onMoved;
        _isPinned = isPinned;
        _intervalSeconds = RefreshInterval.Normalize(intervalSeconds);

        Title = "LiteMon";
        Width = PanelWidth;
        Height = PanelHeight;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        // 关闭 AllowsTransparency：分层窗口会让 WPF 退回软件渲染，CPU 会飙升到 10%+
        AllowsTransparency = false;
        Background = new SolidColorBrush(Color.FromRgb(0x0B, 0x0E, 0x14));
        Topmost = true;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.Manual;

        Left = left;
        Top = top;

        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Stretch;
        _view = new PanelView(bus, devices);
        _view.SetRefreshInterval(_intervalSeconds);
        _view.SetPinned(_isPinned);
        Content = _view;

        MouseLeftButtonDown += OnMouseLeftButtonDown;
        MouseMove += OnMouseMove;
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

        if (position.Y < HeaderHeight)
        {
            if (position.X > Width - CloseZoneWidth)
            {
                Close();
                return;
            }

            if (position.X > Width - CloseZoneWidth - PinZoneWidth)
            {
                TogglePin();
                return;
            }
        }

        if (_isPinned)
        {
            return;   // 固定：拖不动
        }

        double beforeLeft = Left;
        double beforeTop = Top;

        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
            // 鼠标已释放时 DragMove 会抛异常，忽略
        }

        if (Math.Abs(Left - beforeLeft) > 0.5 || Math.Abs(Top - beforeTop) > 0.5)
        {
            _onMoved(Left, Top);
        }
    }

    private void TogglePin()
    {
        _isPinned = !_isPinned;
        _view.SetPinned(_isPinned);
        _onPinnedChanged(_isPinned);

        if (_isPinned)
        {
            // "钉在当前停留的位置"：固定瞬间把实际坐标也记下来
            _onMoved(Left, Top);
        }
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        Point position = e.GetPosition(this);
        bool onButton = position.Y < HeaderHeight && position.X > Width - CloseZoneWidth - PinZoneWidth;

        Cursor = onButton ? Cursors.Hand : _isPinned ? Cursors.Arrow : Cursors.SizeAll;
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
