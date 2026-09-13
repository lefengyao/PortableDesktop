using PortableDesktop.Models;
using PortableDesktop.Services;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace PortableDesktop.Controls;

/// <summary>
/// 单个应用卡片。布局、配色、动效全部在 XAML 里声明，
/// 旧版把这些都写死在 MainWindow 的 C# 代码里，既难改也不响应主题切换。
/// </summary>
public partial class ItemCard : UserControl
{
    private static readonly TimeSpan LaunchCooldown = TimeSpan.FromMilliseconds(600);

    private bool _hovering;
    private bool _entrancePlayed;
    private DateTime _lastLaunch = DateTime.MinValue;

    public ItemCard()
    {
        InitializeComponent();

        // 先隐藏，等 Loaded 时按序号错峰淡入，避免一次性"啪"地全出现
        CardRoot.Opacity = 0;
        Loaded += (_, _) => PlayEntrance();
    }

    // ========== 依赖属性 ==========

    public static readonly DependencyProperty ItemProperty = DependencyProperty.Register(
        nameof(Item), typeof(DesktopItem), typeof(ItemCard),
        new PropertyMetadata(null, OnItemChanged));

    public DesktopItem? Item
    {
        get => (DesktopItem?)GetValue(ItemProperty);
        set => SetValue(ItemProperty, value);
    }

    public static readonly DependencyProperty IconServiceProperty = DependencyProperty.Register(
        nameof(IconService), typeof(IconExtractorService), typeof(ItemCard),
        new PropertyMetadata(null, OnItemChanged));

    /// <summary>图标提取服务，由 MainWindow 注入（走绑定，不用静态全局状态）。</summary>
    public IconExtractorService? IconService
    {
        get => (IconExtractorService?)GetValue(IconServiceProperty);
        set => SetValue(IconServiceProperty, value);
    }

    public static readonly DependencyProperty EntranceIndexProperty = DependencyProperty.Register(
        nameof(EntranceIndex), typeof(int), typeof(ItemCard), new PropertyMetadata(0));

    /// <summary>在列表中的序号，用于入场动画错峰。</summary>
    public int EntranceIndex
    {
        get => (int)GetValue(EntranceIndexProperty);
        set => SetValue(EntranceIndexProperty, value);
    }

    private static void OnItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ItemCard card)
            card.ApplyItem();
    }

    private void ApplyItem()
    {
        if (Item is null || IconService is null)
            return;

        IconImage.Source = IconService.GetIcon(Item.IconPath, Item.IconIndex);
        ToolTip = string.IsNullOrWhiteSpace(Item.TargetPath) ? Item.Name : Item.TargetPath;
    }

    // ========== 交互事件 ==========

    public event EventHandler<DesktopItem>? LaunchRequested;
    public event EventHandler<DesktopItem>? RevealRequested;
    public event EventHandler<DesktopItem>? RemoveRequested;

    private void Card_MouseEnter(object sender, MouseEventArgs e)
    {
        _hovering = true;
        Play("HoverIn");
    }

    private void Card_MouseLeave(object sender, MouseEventArgs e)
    {
        _hovering = false;
        Play("HoverOut");
    }

    private void Card_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // 双击的第二下不再重复播放按压动画
        if (e.ClickCount > 1)
            return;

        Play("Press");
    }

    private void Card_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        // 回弹动画先跑起来，应用启动期间动画还在继续，视觉上更连贯
        Play(_hovering ? "ReleaseHover" : "ReleaseIdle");

        if (DateTime.UtcNow - _lastLaunch < LaunchCooldown)
            return;
        _lastLaunch = DateTime.UtcNow;

        Raise(LaunchRequested);
    }

    private void MenuOpen_Click(object sender, RoutedEventArgs e) => Raise(LaunchRequested);

    private void MenuReveal_Click(object sender, RoutedEventArgs e) => Raise(RevealRequested);

    private void MenuRemove_Click(object sender, RoutedEventArgs e) => Raise(RemoveRequested);

    private void Raise(EventHandler<DesktopItem>? handler)
    {
        if (Item is not null)
            handler?.Invoke(this, Item);
    }

    // ========== 动效 ==========

    private void Play(string storyboardKey)
    {
        if (FindResource(storyboardKey) is Storyboard sb)
            sb.Begin(this, HandoffBehavior.SnapshotAndReplace, true);
    }

    /// <summary>入场：淡入 + 上浮，按序号每 30ms 错开一位（最多错开 14 位）。</summary>
    private void PlayEntrance()
    {
        if (_entrancePlayed)
            return;
        _entrancePlayed = true;

        var delay = TimeSpan.FromMilliseconds(Math.Clamp(EntranceIndex, 0, 14) * 30);
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var sb = new Storyboard { BeginTime = delay };

        var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(240)) { EasingFunction = ease };
        Storyboard.SetTarget(fade, CardRoot);
        Storyboard.SetTargetProperty(fade, new PropertyPath(UIElement.OpacityProperty));
        sb.Children.Add(fade);

        var rise = new DoubleAnimation(10, 0, TimeSpan.FromMilliseconds(280)) { EasingFunction = ease };
        Storyboard.SetTarget(rise, CardOffset);
        Storyboard.SetTargetProperty(rise, new PropertyPath(TranslateTransform.YProperty));
        sb.Children.Add(rise);

        sb.Begin();
    }
}
