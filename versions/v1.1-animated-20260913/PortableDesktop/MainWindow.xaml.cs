using PortableDesktop.Models;
using PortableDesktop.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shell;
using System.Windows.Threading;

namespace PortableDesktop;

public partial class MainWindow : Window
{
    private static readonly TimeSpan SettingsSaveDelay = TimeSpan.FromMilliseconds(400);
    private static readonly TimeSpan DragOverlayIdleDelay = TimeSpan.FromMilliseconds(140);

    private readonly DesktopItemService _itemService;
    private readonly IconExtractorService _iconExtractor;
    private readonly AppSettings _settings;
    private readonly Action _onSettingsChanged;

    /// <summary>绑到图标网格；只增删条目，不再整个重建可视树。</summary>
    private readonly ObservableCollection<DesktopItem> _items = new();

    private readonly DispatcherTimer _settingsSaveTimer;
    private readonly DispatcherTimer _dragOverlayTimer;

    private Effect? _windowShadow;
    private bool _themeReady;
    private bool _dragActive;

    /// <summary>
    /// 供卡片模板绑定用的图标服务（DataTemplate 里用 AncestorType=Window 取到这里）。
    /// 属性名必须与 ItemCard.IconService 的绑定路径一致，改名前先搜一下 XAML。
    /// </summary>
    public IconExtractorService IconService => _iconExtractor;

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
        WindowChrome.SetWindowChrome(this, new WindowChrome
        {
            CaptionHeight = 0,
            ResizeBorderThickness = new Thickness(8),
            CornerRadius = new CornerRadius(0),
            GlassFrameThickness = new Thickness(0),
            UseAeroCaptionButtons = false
        });

        _windowShadow = WindowBorder.Effect;

        // 启动时先隐形，Loaded 后再渐显上浮，避免第一帧"生硬弹出"
        Opacity = 0;

        _settingsSaveTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = SettingsSaveDelay
        };
        _settingsSaveTimer.Tick += (_, _) =>
        {
            _settingsSaveTimer.Stop();
            _onSettingsChanged();
        };

        _dragOverlayTimer = new DispatcherTimer(DispatcherPriority.Input)
        {
            Interval = DragOverlayIdleDelay
        };
        _dragOverlayTimer.Tick += (_, _) =>
        {
            _dragOverlayTimer.Stop();
            _dragActive = false;
            HideDropOverlay();
        };

        // 主题下拉
        ThemeComboBox.ItemsSource = ThemeCatalog.All;
        ThemeComboBox.SelectedValue = ThemeCatalog.Normalize(settings.Theme);
        ThemeComboBox.SelectionChanged += ThemeComboBox_SelectionChanged;
        _themeReady = true;
        ApplyTheme(_settings.Theme);

        // 图标网格
        ItemsHost.ItemsSource = _items;

        // 拖放
        PreviewDragOver += MainWindow_DragOver;
        Drop += MainWindow_Drop;

        StateChanged += (_, _) => UpdateWindowStateVisuals();
        SizeChanged += MainWindow_SizeChanged;
        LocationChanged += MainWindow_LocationChanged;

        Loaded += MainWindow_Loaded;
        Closing += (_, _) => _settingsSaveTimer.Stop();
    }

    // ========== 生命周期 ==========

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        ReloadItems();
        PlayWindowEntrance();
        PlayEmptyStatePulse();
    }

    /// <summary>整窗淡入 + 上浮。</summary>
    private void PlayWindowEntrance()
    {
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        BeginAnimation(OpacityProperty,
            new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(260)) { EasingFunction = ease });
        WindowOffset.BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation(14, 0, TimeSpan.FromMilliseconds(340)) { EasingFunction = ease });
    }

    private void PlayEmptyStatePulse()
    {
        if (FindResource("EmptyStatePulse") is Storyboard pulse)
            pulse.Begin(this, true);
    }

    // ========== 标题栏 ==========

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
            ToggleMaximize();
        else
            DragMove();
    }

    private void MinButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void MaxButton_Click(object sender, RoutedEventArgs e) => ToggleMaximize();

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();

    private void ToggleMaximize() =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    /// <summary>最大化时收掉圆角、边框与投影，避免屏幕边缘出现一圈阴影。</summary>
    private void UpdateWindowStateVisuals()
    {
        var maximized = WindowState == WindowState.Maximized;

        MaxButton.Content = maximized ? "\uE923" : "\uE922";
        WindowBorder.CornerRadius = Resolve<CornerRadius>(
            maximized ? "WindowCornerRadiusMaximized" : "WindowCornerRadius");
        TitleBarBorder.CornerRadius = Resolve<CornerRadius>(
            maximized ? "TitleBarCornerRadiusMaximized" : "TitleBarCornerRadius");
        ContentBorder.CornerRadius = Resolve<CornerRadius>(
            maximized ? "ContentCornerRadiusMaximized" : "ContentCornerRadius");
        WindowBorder.BorderThickness = new Thickness(maximized ? 0 : 1);
        WindowBorder.Effect = maximized ? null : _windowShadow;
    }

    private T Resolve<T>(string key) => (T)FindResource(key);

    // ========== 拖放 ==========

    /// <summary>
    /// 旧版在 DragEnter/DragLeave 上切换遮罩，鼠标在卡片与背景之间移动会不停触发
    /// Leave 导致遮罩闪烁。这里改成"DragOver 续期 + 空闲即隐藏"，只认一个信号源。
    /// </summary>
    private void MainWindow_DragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.None;
            return;
        }

        e.Effects = DragDropEffects.Copy;
        e.Handled = true;

        _dragActive = true;
        ShowDropOverlay();

        _dragOverlayTimer.Stop();
        _dragOverlayTimer.Start();
    }

    private void MainWindow_Drop(object sender, DragEventArgs e)
    {
        _dragOverlayTimer.Stop();
        _dragActive = false;
        HideDropOverlay();

        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            return;

        e.Handled = true;

        var files = (string[])e.Data.GetData(DataFormats.FileDrop)!;
        var added = false;
        foreach (var file in files)
        {
            if (_itemService.AddFromPath(file))
                added = true;
        }

        if (added)
            ReloadItems();
    }

    private void ShowDropOverlay()
    {
        DropOverlay.Visibility = Visibility.Visible;
        DropOverlay.BeginAnimation(OpacityProperty,
            new DoubleAnimation(1, TimeSpan.FromMilliseconds(140))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });
    }

    private void HideDropOverlay()
    {
        if (DropOverlay.Visibility != Visibility.Visible)
            return;

        var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(170));
        fade.Completed += (_, _) =>
        {
            if (_dragActive)
                return;

            DropOverlay.Visibility = Visibility.Collapsed;
            DropOverlay.BeginAnimation(OpacityProperty, null);
        };
        DropOverlay.BeginAnimation(OpacityProperty, fade);
    }

    // ========== 图标网格 ==========

    private void ReloadItems()
    {
        var items = _itemService.GetItems();

        _items.Clear();
        foreach (var item in items)
            _items.Add(item);

        UpdateEmptyState();
    }

    private void UpdateEmptyState()
    {
        var count = _items.Count;
        EmptyState.Visibility = count == 0 ? Visibility.Visible : Visibility.Collapsed;
        CountText.Text = $"{count} 项";
    }

    private void Card_LaunchRequested(object? sender, DesktopItem item) => LaunchItem(item);

    private void Card_RemoveRequested(object? sender, DesktopItem item)
    {
        if (!_itemService.RemoveItem(item.Id))
            return;

        _items.Remove(item);
        UpdateEmptyState();
    }

    private void Card_RevealRequested(object? sender, DesktopItem item)
    {
        try
        {
            var path = File.Exists(item.TargetPath) || Directory.Exists(item.TargetPath)
                ? item.TargetPath
                : item.IconPath;

            if (Directory.Exists(path))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
            }
            else if (File.Exists(path))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
            }
            else
            {
                MessageBox.Show("目标文件已不存在。", "便携桌面",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"无法打开所在位置：{ex.Message}", "错误",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void LaunchItem(DesktopItem item)
    {
        if (string.IsNullOrWhiteSpace(item.TargetPath))
            return;

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

    // ========== 主题 ==========

    private void ThemeComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!_themeReady || ThemeComboBox.SelectedValue is not string themeId)
            return;

        _settings.Theme = ThemeCatalog.Normalize(themeId);
        ApplyTheme(_settings.Theme);
        PlayThemeTransition();

        // 主题切换立刻落盘，不走防抖
        _onSettingsChanged();
    }

    /// <summary>只替换主题字典，保留 Shared.xaml 里的结构与动效。</summary>
    private static void ApplyTheme(string theme)
    {
        var dictionaries = Application.Current.Resources.MergedDictionaries;
        var themeDictionary = new ResourceDictionary { Source = ThemeCatalog.ToResourceUri(theme) };

        for (var i = 0; i < dictionaries.Count; i++)
        {
            var source = dictionaries[i].Source?.OriginalString ?? string.Empty;
            if (source.Contains("/Themes/", StringComparison.OrdinalIgnoreCase) &&
                !source.Contains("Shared.xaml", StringComparison.OrdinalIgnoreCase))
            {
                dictionaries[i] = themeDictionary;
                return;
            }
        }

        dictionaries.Add(themeDictionary);
    }

    /// <summary>切换主题后给内容区一个短渐显，让整片换色不那么突兀。</summary>
    private void PlayThemeTransition()
    {
        ContentBorder.BeginAnimation(OpacityProperty,
            new DoubleAnimation(0.45, 1, TimeSpan.FromMilliseconds(220))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });
    }

    // ========== 窗口状态持久化 ==========

    private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (WindowState != WindowState.Normal || e.NewSize.Width <= 0)
            return;

        _settings.MainWindowWidth = Width;
        _settings.MainWindowHeight = Height;
        ScheduleSettingsSave();
    }

    private void MainWindow_LocationChanged(object? sender, EventArgs e)
    {
        if (WindowState != WindowState.Normal)
            return;

        _settings.MainWindowLeft = Left;
        _settings.MainWindowTop = Top;
        ScheduleSettingsSave();
    }

    /// <summary>拖动/缩放窗口会高频触发位置变化，用防抖把落盘次数压到一次。</summary>
    private void ScheduleSettingsSave()
    {
        _settingsSaveTimer.Stop();
        _settingsSaveTimer.Start();
    }
}
