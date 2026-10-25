using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LiteMon.Core.Sensors;

namespace LiteMon.App;

/// <summary>自定义刷新间隔输入框：只接受 1–60 的整数秒。</summary>
internal sealed class RefreshIntervalDialog : Window
{
    private static readonly Brush PanelBrush = CreateBrush(0x14, 0x19, 0x22);
    private static readonly Brush InputBrush = CreateBrush(0x0B, 0x0E, 0x14);
    private static readonly Brush InputBorderBrush = CreateBrush(0x2A, 0x33, 0x40);
    private static readonly Brush TextBrush = CreateBrush(0xF5, 0xF7, 0xFA);
    private static readonly Brush MutedBrush = CreateBrush(0xA9, 0xB4, 0xC0);
    private static readonly Brush ErrorBrush = CreateBrush(0xF8, 0x71, 0x71);

    private readonly TextBox _input;
    private readonly TextBlock _error;

    /// <summary>用户确认后的秒数（仅当 DialogResult == true 时有意义）。</summary>
    public double Seconds { get; private set; }

    public RefreshIntervalDialog(double currentSeconds)
    {
        Seconds = RefreshInterval.Normalize(currentSeconds);

        Title = "自定义刷新间隔";
        Width = 320;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        Background = PanelBrush;
        Foreground = TextBrush;
        FontFamily = new FontFamily("Microsoft YaHei UI");
        FontSize = 12;

        var stack = new StackPanel { Margin = new Thickness(16) };

        stack.Children.Add(new TextBlock
        {
            Text = RefreshInterval.PromptText,
            Foreground = MutedBrush,
            Margin = new Thickness(0, 0, 0, 8),
        });

        _input = new TextBox
        {
            Text = Seconds.ToString("0", CultureInfo.InvariantCulture),
            Padding = new Thickness(6, 4, 6, 4),
            Background = InputBrush,
            Foreground = TextBrush,
            BorderBrush = InputBorderBrush,
            CaretBrush = Brushes.White,
        };
        stack.Children.Add(_input);

        _error = new TextBlock
        {
            Foreground = ErrorBrush,
            Margin = new Thickness(0, 6, 0, 0),
            TextWrapping = TextWrapping.Wrap,
            Visibility = Visibility.Collapsed,
        };
        stack.Children.Add(_error);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 14, 0, 0),
        };

        var cancel = new Button { Content = "取消", Width = 72, Margin = new Thickness(0, 0, 8, 0), IsCancel = true };
        var confirm = new Button { Content = "确定", Width = 72, IsDefault = true };
        confirm.Click += OnConfirm;

        buttons.Children.Add(cancel);
        buttons.Children.Add(confirm);
        stack.Children.Add(buttons);

        Content = stack;
        Loaded += (_, _) =>
        {
            _input.Focus();
            _input.SelectAll();
        };
    }

    private void OnConfirm(object sender, RoutedEventArgs e)
    {
        if (!RefreshInterval.TryParse(_input.Text, out double seconds))
        {
            _error.Text = RefreshInterval.ValidationMessage;
            _error.Visibility = Visibility.Visible;
            _input.Focus();
            _input.SelectAll();
            return;
        }

        Seconds = seconds;
        DialogResult = true;
    }

    private static SolidColorBrush CreateBrush(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}
