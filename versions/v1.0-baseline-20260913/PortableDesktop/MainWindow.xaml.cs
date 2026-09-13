using PortableDesktop.Models;
using PortableDesktop.Services;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shell;

namespace PortableDesktop;

public partial class MainWindow : Window
{
    private readonly DesktopItemService _itemService;
    private readonly IconExtractorService _iconExtractor;
    private readonly AppSettings _settings;
    private readonly Action _onSettingsChanged;

    public MainWindow(DesktopItemService itemService, IconExtractorService iconExtractor,
                      AppSettings settings, Action onSettingsChanged)
    {
        InitializeComponent();
        _itemService = itemService;
        _iconExtractor = iconExtractor;
        _settings = settings;
        _onSettingsChanged = onSettingsChanged;

        // Window position & size
        Left = settings.MainWindowLeft;
        Top = settings.MainWindowTop;
        Width = settings.MainWindowWidth;
        Height = settings.MainWindowHeight;

        // WindowChrome: CaptionHeight=0 so title bar controls are clickable
        // ResizeBorderThickness still provides edge resize handles
        var chrome = new WindowChrome
        {
            CaptionHeight = 0,
            ResizeBorderThickness = new Thickness(8),
            CornerRadius = new CornerRadius(0),
            GlassFrameThickness = new Thickness(0),
            UseAeroCaptionButtons = false
        };
        WindowChrome.SetWindowChrome(this, chrome);

        // Track window state changes for maximize/restore icon
        StateChanged += (_, _) => UpdateMaxButtonIcon();

        // Theme dropdown
        ThemeComboBox.ItemsSource = new[] { "浅色", "粉色", "毛玻璃", "护眼绿" };
        ThemeComboBox.SelectionChanged += ThemeComboBox_SelectionChanged;
        var themeIndex = settings.Theme switch
        {
            "Light" => 0,
            "Pink" => 1,
            "Acrylic" => 2,
            "Green" => 3,
            _ => 0
        };
        ThemeComboBox.SelectedIndex = themeIndex;

        // 确保启动时主题被正确应用（如果 SelectedIndex 未改变则 SelectionChanged 不会触发）
        ApplyTheme(settings.Theme);

        // Drag & Drop
        DragEnter += MainWindow_DragEnter;
        DragLeave += MainWindow_DragLeave;
        Drop += MainWindow_Drop;

        // Content loading
        Loaded += (_, _) => RefreshItems();
        SizeChanged += MainWindow_SizeChanged;
        LocationChanged += MainWindow_LocationChanged;

        UpdateEmptyHint();
    }

    // ========== 标题栏拖动 ==========
    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleMaximize();
        }
        else
        {
            DragMove();
        }
    }

    // ========== 窗口控制按钮 ==========
    private void MinButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaxButton_Click(object sender, RoutedEventArgs e)
    {
        ToggleMaximize();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

    private void ToggleMaximize()
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void UpdateMaxButtonIcon()
    {
        if (WindowState == WindowState.Maximized)
        {
            MaxButton.Content = ""; // Restore icon
            WindowBorder.CornerRadius = new CornerRadius(0);
            WindowBorder.BorderThickness = new Thickness(0);
            TitleBarBorder.CornerRadius = new CornerRadius(0);
            ContentBorder.CornerRadius = new CornerRadius(0);
        }
        else
        {
            MaxButton.Content = ""; // Maximize icon
            WindowBorder.CornerRadius = new CornerRadius(16);
            WindowBorder.BorderThickness = new Thickness(1);
            TitleBarBorder.CornerRadius = new CornerRadius(16, 16, 0, 0);
            ContentBorder.CornerRadius = new CornerRadius(0, 0, 16, 16);
        }
    }

    // ========== 拖放事件 ==========
    private void MainWindow_DragEnter(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
            DropOverlay.Visibility = Visibility.Visible;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
    }

    private void MainWindow_DragLeave(object sender, DragEventArgs e)
    {
        DropOverlay.Visibility = Visibility.Collapsed;
    }

    private void MainWindow_Drop(object sender, DragEventArgs e)
    {
        DropOverlay.Visibility = Visibility.Collapsed;

        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            return;

        var files = (string[])e.Data.GetData(DataFormats.FileDrop)!;
        foreach (var file in files)
        {
            _itemService.AddFromPath(file);
        }
        RefreshItems();
    }

    // ========== 刷新图标网格 ==========
    private void RefreshItems()
    {
        ItemsPanel.Children.Clear();
        var items = _itemService.GetItems();

        foreach (var item in items)
        {
            ItemsPanel.Children.Add(CreateItemCard(item));
        }

        UpdateEmptyHint();
    }

    private void UpdateEmptyHint()
    {
        EmptyHint.Visibility = _itemService.GetItems().Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    // ========== 创建现代化卡片 ==========
    private Border CreateItemCard(DesktopItem item)
    {
        var icon = _iconExtractor.ExtractIcon(item.IconPath, item.IconIndex);

        // 图标容器
        var iconBorder = new Border
        {
            Width = 44,
            Height = 44,
            CornerRadius = new CornerRadius(12),
            Background = FindResource("IconContainerBrush") as Brush
                ?? new SolidColorBrush(Colors.LightGray),
            Child = new Image
            {
                Source = icon ?? CreatePlaceholderIcon(),
                Width = 28,
                Height = 28
            }
        };

        // 名称
        var nameText = new TextBlock
        {
            Text = item.Name,
            FontSize = 12,
            FontWeight = FontWeights.Medium,
            Foreground = FindResource("WindowForegroundBrush") as Brush,
            TextAlignment = TextAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 72,
            Margin = new Thickness(2, 6, 2, 0)
        };

        // 布局
        var stackPanel = new StackPanel { Orientation = Orientation.Vertical };
        stackPanel.Children.Add(iconBorder);
        stackPanel.Children.Add(nameText);

        // 外层卡片
        var card = new Border
        {
            Width = 90,
            Height = 90,
            CornerRadius = new CornerRadius(14),
            Background = FindResource("CardBackgroundBrush") as Brush,
            BorderBrush = FindResource("CardBorderBrush") as Brush,
            BorderThickness = new Thickness(1),
            Margin = new Thickness(6),
            Cursor = Cursors.Hand,
            ToolTip = item.TargetPath,
            Child = stackPanel
        };

        // 阴影
        card.Effect = new DropShadowEffect
        {
            BlurRadius = 8,
            ShadowDepth = 2,
            Opacity = 0.08,
            Color = Colors.Black,
            RenderingBias = RenderingBias.Performance
        };

        // 悬停效果
        card.MouseEnter += (_, _) =>
        {
            card.Background = FindResource("CardHoverBackgroundBrush") as Brush;
            if (card.Effect is DropShadowEffect shadow)
            {
                shadow.Opacity = 0.18;
                shadow.BlurRadius = 16;
                shadow.ShadowDepth = 4;
            }
        };
        card.MouseLeave += (_, _) =>
        {
            card.Background = FindResource("CardBackgroundBrush") as Brush;
            if (card.Effect is DropShadowEffect shadow)
            {
                shadow.Opacity = 0.08;
                shadow.BlurRadius = 8;
                shadow.ShadowDepth = 2;
            }
        };
        card.MouseLeftButtonDown += (_, _) => LaunchItem(item);

        // 右键菜单 — 删除
        card.MouseRightButtonDown += (_, e) =>
        {
            var menu = new ContextMenu();
            var deleteItem = new MenuItem
            {
                Header = "删除",
                Foreground = new SolidColorBrush(Color.FromRgb(0xE5, 0x3E, 0x3E))
            };
            deleteItem.Click += (_, _) =>
            {
                _itemService.RemoveItem(item.Id);
                RefreshItems();
            };
            menu.Items.Add(deleteItem);
            menu.IsOpen = true;
        };

        return card;
    }

    // ========== 启动程序 ==========
    private void LaunchItem(DesktopItem item)
    {
        try
        {
            Process.Start(new ProcessStartInfo(item.TargetPath) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"无法启动：{ex.Message}", "错误",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ========== 占位图标 ==========
    private ImageSource CreatePlaceholderIcon()
    {
        var visual = new DrawingVisual();
        using (var ctx = visual.RenderOpen())
        {
            ctx.DrawRoundedRectangle(
                new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xDD)),
                null, new Rect(0, 0, 28, 28), 6, 6);
        }
        var bitmap = new RenderTargetBitmap(28, 28, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        return bitmap;
    }

    // ========== 主题切换 ==========
    private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var themes = new[] { "Light", "Pink", "Acrylic", "Green" };
        var idx = ThemeComboBox.SelectedIndex;
        if (idx < 0 || idx >= themes.Length) return;

        _settings.Theme = themes[idx];
        ApplyTheme(_settings.Theme);
        _onSettingsChanged();
    }

    private static void ApplyTheme(string theme)
    {
        var dict = new ResourceDictionary();
        dict.Source = new Uri($"/PortableDesktop;component/Themes/{theme}Theme.xaml", UriKind.Relative);
        Application.Current.Resources.MergedDictionaries.Clear();
        Application.Current.Resources.MergedDictionaries.Add(dict);
    }

    // ========== 窗口状态保存 ==========
    private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (WindowState == WindowState.Normal)
        {
            _settings.MainWindowWidth = Width;
            _settings.MainWindowHeight = Height;
            _onSettingsChanged();
        }
    }

    private void MainWindow_LocationChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Normal)
        {
            _settings.MainWindowLeft = Left;
            _settings.MainWindowTop = Top;
            _onSettingsChanged();
        }
    }
}
