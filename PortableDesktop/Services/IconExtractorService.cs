using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PortableDesktop.Services;

/// <summary>
/// 通过 Win32 Shell API 提取文件/文件夹的真实图标。全流程只用 Shell API，
/// 不再依赖 System.Drawing，因此项目不需要任何 NuGet 包。
///
/// 相比旧版：
/// 1. 提取顺序改为「指定索引 → 外壳关联图标(SHGetFileInfo)」，
///    因此 .png / .txt / .pdf 这类非可执行文件也能拿到正确图标；
///    旧版对非 .lnk 文件走 Icon.ExtractAssociatedIcon，图片、文档类常常拿不到图标；
/// 2. 目标已被移动/删除时，用扩展名向外壳要一个"类型图标"，比灰块占位图更可读；
/// 3. 所有取到的 HICON 句柄都会 DestroyIcon，返回的 BitmapSource 会 Freeze()，
///    既不再泄漏 GDI 句柄，也让位图可跨线程复用、加快渲染；
/// 4. 结果按「路径 + 索引」缓存，重复刷新网格时不再反复走 Shell API。
/// </summary>
public class IconExtractorService
{
    /// <summary>
    /// 进程级共享实例。图标缓存本来就是全局有用的，
    /// 同时它也是 ItemCard 里 IconService 依赖属性的默认值 ——
    /// 万一绑定/注入哪天失效，卡片也会退回用这个实例，图标不会整片消失。
    /// </summary>
    public static IconExtractorService Shared { get; } = new();

    private readonly Dictionary<string, ImageSource> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();

    private static readonly ImageSource Placeholder = CreatePlaceholder();

    /// <summary>取图标；任何失败都返回占位图，调用方无需判空。</summary>
    public ImageSource GetIcon(string? filePath, int iconIndex = 0)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return Placeholder;

        var key = iconIndex + "|" + filePath;
        lock (_gate)
        {
            if (_cache.TryGetValue(key, out var cached))
                return cached;
        }

        var icon = Extract(filePath, iconIndex) ?? Placeholder;

        lock (_gate)
        {
            _cache[key] = icon;
        }
        return icon;
    }

    /// <summary>清空缓存（例如外部替换了图标文件后强制重取）。</summary>
    public void ClearCache()
    {
        lock (_gate)
        {
            _cache.Clear();
        }
    }

    private static ImageSource? Extract(string filePath, int iconIndex)
    {
        try
        {
            if (Directory.Exists(filePath))
                return FromHIcon(ShellIcon(filePath, 0, useFileAttributes: false));

            if (File.Exists(filePath))
            {
                // 1) 显式指定了图标索引（.lnk 的 IconLocation 常见形式）
                if (iconIndex != 0)
                {
                    var large = new IntPtr[1];
                    if (ExtractIconEx(filePath, iconIndex, large, null, 1) > 0 && large[0] != IntPtr.Zero)
                        return FromHIcon(large[0]);
                }

                // 2) 外壳关联图标：对任意文件类型都有效
                return FromHIcon(ShellIcon(filePath, 0, useFileAttributes: false));
            }

            // 3) 目标不存在（快捷方式指向的文件被移动/删除了）：
            //    按扩展名向外壳要一个类型图标，比纯灰块有信息量得多
            return FromHIcon(ShellIcon(filePath, FileAttributeNormal, useFileAttributes: true));
        }
        catch
        {
            // 图标提取失败不应影响界面
            return null;
        }
    }

    /// <summary>SHGetFileInfo 取 32×32 大图标；返回的句柄需由调用方 DestroyIcon。</summary>
    private static IntPtr ShellIcon(string path, uint attributes, bool useFileAttributes)
    {
        var info = new SHFILEINFO();
        var size = (uint)Marshal.SizeOf(info);
        var flags = SHGFI_ICON | SHGFI_LARGEICON | (useFileAttributes ? SHGFI_USEFILEATTRIBUTES : 0u);
        var result = SHGetFileInfo(path, attributes, ref info, size, flags);
        return result != IntPtr.Zero ? info.hIcon : IntPtr.Zero;
    }

    private static ImageSource? FromHIcon(IntPtr hIcon)
    {
        if (hIcon == IntPtr.Zero)
            return null;

        try
        {
            var bitmap = Imaging.CreateBitmapSourceFromHIcon(
                hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());

            // 冻结后渲染线程可直接复用，避免每次绘制都做一次拷贝
            if (bitmap.CanFreeze)
                bitmap.Freeze();

            return bitmap;
        }
        finally
        {
            DestroyIcon(hIcon);
        }
    }

    private static ImageSource CreatePlaceholder()
    {
        var visual = new DrawingVisual();
        using (var ctx = visual.RenderOpen())
        {
            ctx.DrawRoundedRectangle(
                new SolidColorBrush(Color.FromRgb(0xD5, 0xD7, 0xE6)),
                null, new Rect(0, 0, 32, 32), 7, 7);
            ctx.DrawRoundedRectangle(
                new SolidColorBrush(Color.FromRgb(0xEC, 0xED, 0xF6)),
                null, new Rect(7, 7, 18, 18), 4, 4);
        }

        var bitmap = new RenderTargetBitmap(32, 32, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    // ========== Win32 ==========

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(
        string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern uint ExtractIconEx(
        string lpszFile, int nIconIndex, IntPtr[]? phiconLarge, IntPtr[]? phiconSmall, uint nIcons);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    private const uint SHGFI_ICON = 0x000000100;
    private const uint SHGFI_LARGEICON = 0x000000000;
    private const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;
    private const uint FileAttributeNormal = 0x00000080;
}
