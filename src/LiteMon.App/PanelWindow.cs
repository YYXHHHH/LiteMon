using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using LiteMon.Core.Appearance;
using LiteMon.Core.Layout;
using LiteMon.Core.Sensors;
using LiteMon.Core.Snapshots;
using LiteMon.Sensors;

namespace LiteMon.App;

/// <summary>
/// 无边框置顶面板：拖动、图钉固定、右键菜单（刷新间隔 / 外观 / 提权 / 退出）。
/// 尺寸由布局与可见部件决定，切换后自动收敛到屏幕内。
/// </summary>
public sealed class PanelWindow : Window
{
    private const double HeaderHeight = 40;
    private const double CloseZoneWidth = 44;
    private const double PinZoneWidth = 28;

    private readonly PanelView _view;
    private readonly Func<double, double> _applyInterval;
    private readonly Func<AppearanceSettings, AppearanceSettings> _applyAppearance;
    private readonly Action<bool> _onPinnedChanged;
    private readonly Action<double, double> _onMoved;
    private readonly List<MenuItem> _presetItems = [];
    private readonly List<MenuItem> _opacityItems = [];
    private readonly Dictionary<PanelGroup, MenuItem> _groupItems = [];

    private MenuItem? _customItem;
    private MenuItem? _layoutDetailed;
    private MenuItem? _layoutCompact;
    private MenuItem? _barsShow;
    private MenuItem? _barsHide;
    private MenuItem? _animSmooth;
    private MenuItem? _animSnap;
    private MenuItem? _opacityRoot;
    private double _intervalSeconds;
    private bool _isPinned;
    private AppearanceSettings _appearance;

    public PanelWindow(
        SnapshotBus bus,
        DeviceInfo devices,
        double intervalSeconds,
        Func<double, double> applyInterval,
        AppearanceSettings appearance,
        double left,
        double top,
        bool isPinned,
        Action<bool> onPinnedChanged,
        Action<double, double> onMoved,
        Func<AppearanceSettings, AppearanceSettings> applyAppearance,
        bool layered)
    {
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(applyInterval);
        ArgumentNullException.ThrowIfNull(onPinnedChanged);
        ArgumentNullException.ThrowIfNull(onMoved);
        ArgumentNullException.ThrowIfNull(applyAppearance);

        _applyInterval = applyInterval;
        _applyAppearance = applyAppearance;
        _onPinnedChanged = onPinnedChanged;
        _onMoved = onMoved;
        _isPinned = isPinned;
        _appearance = appearance.Normalized();
        _intervalSeconds = RefreshInterval.Normalize(intervalSeconds);

        Title = "LiteMon";
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        // 只有需要"真的透出桌面"时才启用分层窗口（代价是软件渲染，所以默认关闭、且透明时强制无动画）
        IsLayered = layered;
        AllowsTransparency = layered;
        Topmost = true;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.Manual;

        (double width, double height) = PanelMetrics.GetSize(_appearance.Layout, _appearance.VisibleGroups);
        Width = width;
        Height = height;
        Left = left;
        Top = top;

        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Stretch;
        _view = new PanelView(bus, devices);
        _view.SetRefreshInterval(_intervalSeconds);
        _view.SetPinned(_isPinned);
        _view.SetAppearance(_appearance);
        Content = _view;

        ApplyBackground();

        MouseLeftButtonDown += OnMouseLeftButtonDown;
        MouseMove += OnMouseMove;
        KeyDown += OnKeyDown;

        var menu = new ContextMenu();
        menu.Items.Add(BuildIntervalMenu());
        menu.Items.Add(BuildAppearanceMenu());

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

    /// <summary>由 App 调用：应用外观（尺寸、背景、透明、视图、菜单勾选）。</summary>
    public void ApplyAppearance(AppearanceSettings appearance)
    {
        AppearanceSettings normalized = appearance.Normalized();
        _appearance = normalized;

        (double width, double height) = PanelMetrics.GetSize(normalized.Layout, normalized.VisibleGroups);
        width = Math.Min(width, SystemParameters.VirtualScreenWidth - 24);

        Width = width;
        Height = height;

        (double left, double top) = WindowPlacement.Clamp(
            Left,
            Top,
            width,
            height,
            SystemParameters.VirtualScreenLeft,
            SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth,
            SystemParameters.VirtualScreenHeight);
        Left = left;
        Top = top;

        _view.SetAppearance(normalized);
        ApplyBackground();
        UpdateAppearanceChecks();
    }

    /// <summary>当前窗口是否为分层窗口（透明模式）。切换透明度需要由 App 重建窗口。</summary>
    public bool IsLayered { get; }

    private void ApplyBackground()
    {
        // 分层窗口必须让窗口自身完全透明，由 PanelView 画带 alpha 的背景；
        // 非分层窗口则用不透明底色兜底。
        Background = IsLayered ? Brushes.Transparent : new SolidColorBrush(Color.FromRgb(0x0B, 0x0E, 0x14));
    }

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

    private MenuItem BuildAppearanceMenu()
    {
        var root = new MenuItem { Header = "外观" };

        var layout = new MenuItem { Header = "布局" };
        _layoutDetailed = new MenuItem { Header = "竖版详细", IsCheckable = true };
        _layoutCompact = new MenuItem { Header = "横版紧凑", IsCheckable = true };
        _layoutDetailed.Click += (_, _) => OnAppearancePicked(_appearance with { Layout = PanelLayout.Detailed });
        _layoutCompact.Click += (_, _) => OnAppearancePicked(_appearance with { Layout = PanelLayout.Compact });
        layout.Items.Add(_layoutDetailed);
        layout.Items.Add(_layoutCompact);

        _opacityRoot = new MenuItem { Header = "透明度" };
        foreach (int percent in AppearanceSettings.OpacityPresets)
        {
            var item = new MenuItem
            {
                Header = percent == AppearanceSettings.MaxOpacity ? "不透明" : percent + "%",
                IsCheckable = true,
                Tag = percent,
            };
            item.Click += (_, _) => OnAppearancePicked(_appearance with { OpacityPercent = (int)item.Tag });
            _opacityItems.Add(item);
            _opacityRoot.Items.Add(item);
        }

        var bars = new MenuItem { Header = "进度条" };
        _barsShow = new MenuItem { Header = "显示", IsCheckable = true };
        _barsHide = new MenuItem { Header = "隐藏", IsCheckable = true };
        _barsShow.Click += (_, _) => OnAppearancePicked(_appearance with { ShowBars = true });
        _barsHide.Click += (_, _) => OnAppearancePicked(_appearance with { ShowBars = false });
        bars.Items.Add(_barsShow);
        bars.Items.Add(_barsHide);

        var animation = new MenuItem { Header = "动画" };
        _animSmooth = new MenuItem { Header = "平滑过渡（较费 CPU）", IsCheckable = true };
        _animSnap = new MenuItem { Header = "立即跳变（默认，最省）", IsCheckable = true };
        _animSmooth.Click += (_, _) => OnAppearancePicked(_appearance with { SmoothAnimation = true });
        _animSnap.Click += (_, _) => OnAppearancePicked(_appearance with { SmoothAnimation = false });
        animation.Items.Add(_animSnap);
        animation.Items.Add(_animSmooth);

        var groups = new MenuItem { Header = "显示部件" };
        foreach (var group in PanelMetrics.GroupOrder)
        {
            PanelGroup captured = group;
            var item = new MenuItem { Header = PanelMetrics.Label(group), IsCheckable = true };
            item.Click += (_, _) => OnAppearancePicked(ToggleGroup(captured));
            _groupItems[group] = item;
            groups.Items.Add(item);
        }

        root.Items.Add(layout);
        root.Items.Add(_opacityRoot);
        root.Items.Add(bars);
        root.Items.Add(animation);
        root.Items.Add(groups);

        root.SubmenuOpened += (_, _) => UpdateAppearanceChecks();
        UpdateAppearanceChecks();
        return root;
    }

    private AppearanceSettings ToggleGroup(PanelGroup group)
    {
        PanelGroup visible = _appearance.VisibleGroups ^ group;
        return _appearance with { VisibleGroups = visible };
    }

    private void OnAppearancePicked(AppearanceSettings appearance)
    {
        // App 负责归一化、持久化以及必要时重建窗口（透明/不透明切换）
        _ = _applyAppearance(appearance);
    }

    private void UpdateAppearanceChecks()
    {
        if (_layoutDetailed is not null && _layoutCompact is not null)
        {
            _layoutDetailed.IsChecked = _appearance.Layout == PanelLayout.Detailed;
            _layoutCompact.IsChecked = _appearance.Layout == PanelLayout.Compact;
        }

        foreach (var item in _opacityItems)
        {
            item.IsChecked = (int)item.Tag == _appearance.OpacityPercent;
        }

        if (_barsShow is not null && _barsHide is not null)
        {
            _barsShow.IsChecked = _appearance.ShowBars;
            _barsHide.IsChecked = !_appearance.ShowBars;
            _barsShow.IsEnabled = _appearance.Layout == PanelLayout.Detailed;
            _barsHide.IsEnabled = _appearance.Layout == PanelLayout.Detailed;
        }

        if (_animSmooth is not null && _animSnap is not null)
        {
            _animSmooth.IsChecked = _appearance.SmoothAnimation;
            _animSnap.IsChecked = !_appearance.SmoothAnimation;
            bool animationAllowed = _appearance.Layout == PanelLayout.Detailed && !_appearance.IsTransparent;
            _animSmooth.IsEnabled = animationAllowed;
            _animSnap.IsEnabled = animationAllowed;
            _animSmooth.Header = _appearance.IsTransparent
                ? "平滑过渡（透明模式不可用）"
                : "平滑过渡（较费 CPU）";
        }

        foreach (var (group, item) in _groupItems)
        {
            bool visible = _appearance.IsVisible(group);
            item.IsChecked = visible;

            bool lastOne = visible && _appearance.VisibleGroups == group;
            item.IsEnabled = !lastOne;
            item.Header = lastOne ? PanelMetrics.Label(group) + "（至少保留一项）" : PanelMetrics.Label(group);
        }
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
            UpdateIntervalChecks();
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

    private double HitHeaderHeight => _appearance.Layout == PanelLayout.Compact ? Height : HeaderHeight;

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        Point position = e.GetPosition(this);

        if (position.Y < HitHeaderHeight)
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
            return;
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
            _onMoved(Left, Top);
        }
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        Point position = e.GetPosition(this);
        bool onButton = position.Y < HitHeaderHeight && position.X > Width - CloseZoneWidth - PinZoneWidth;

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
