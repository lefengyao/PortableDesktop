namespace PortableDesktop.Models;

/// <summary>
/// 一套主题的元数据：内部 Id（对应 Themes/{Id}Theme.xaml）与界面显示名。
/// ToString 直接返回显示名 —— 下拉框、日志、调试器里看到的就是「浅色」而不是 record 展开式。
/// </summary>
public sealed record ThemeInfo(string Id, string DisplayName)
{
    public override string ToString() => DisplayName;
}

/// <summary>
/// 主题的单一真源。窗口下拉、启动校验、资源字典路径三处都从这里取，
/// 避免像旧版那样在 App 与 MainWindow 里各维护一份字符串数组而失配。
/// </summary>
public static class ThemeCatalog
{
    public const string DefaultId = "Light";

    public static IReadOnlyList<ThemeInfo> All { get; } = new[]
    {
        new ThemeInfo("Light", "浅色"),
        new ThemeInfo("Pink", "粉色"),
        new ThemeInfo("Acrylic", "毛玻璃"),
        new ThemeInfo("Green", "护眼绿")
    };

    /// <summary>把任意（可能非法或为空的）主题标识规整为合法 Id。</summary>
    public static string Normalize(string? id) =>
        All.Any(t => string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase))
            ? All.First(t => string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase)).Id
            : DefaultId;

    /// <summary>资源字典相对路径，例如 /PortableDesktop;component/Themes/LightTheme.xaml</summary>
    public static Uri ToResourceUri(string? id) =>
        new($"/PortableDesktop;component/Themes/{Normalize(id)}Theme.xaml", UriKind.Relative);
}
