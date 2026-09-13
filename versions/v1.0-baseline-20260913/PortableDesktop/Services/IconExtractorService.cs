using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PortableDesktop.Services;

public class IconExtractorService
{
    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SHGetFileInfo(
        string pszPath,
        uint dwFileAttributes,
        ref SHFILEINFO psfi,
        uint cbFileInfo,
        uint uFlags);

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern uint ExtractIconEx(
        string lpszFile,
        int nIconIndex,
        IntPtr[]? phiconLarge,
        IntPtr[]? phiconSmall,
        uint nIcons);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
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

    private const uint SHGFI_ICON = 0x100;
    private const uint SHGFI_LARGEICON = 0x0;

    public ImageSource? ExtractIcon(string filePath, int iconIndex = 0)
    {
        try
        {
            // Validate: empty path → nothing to extract
            if (string.IsNullOrWhiteSpace(filePath))
                return null;

            Icon? icon = null;

            if (Directory.Exists(filePath))
            {
                // Use Shell API for folders
                var shfi = new SHFILEINFO();
                var cb = (uint)Marshal.SizeOf(shfi);
                var result = SHGetFileInfo(filePath, 0, ref shfi, cb, SHGFI_ICON | SHGFI_LARGEICON);
                if (result != IntPtr.Zero && shfi.hIcon != IntPtr.Zero)
                {
                    icon = Icon.FromHandle(shfi.hIcon);
                }
            }
            else if (File.Exists(filePath))
            {
                // Try to extract a specific icon by index first (supports .exe, .dll, .ico, etc.)
                if (iconIndex != 0)
                {
                    var largeIcons = new IntPtr[1];
                    var count = ExtractIconEx(filePath, iconIndex, largeIcons, null, 1);
                    if (count > 0 && largeIcons[0] != IntPtr.Zero)
                    {
                        icon = Icon.FromHandle(largeIcons[0]);
                    }
                }

                // Fallback: extract the default associated icon
                if (icon == null)
                {
                    icon = Icon.ExtractAssociatedIcon(filePath);
                }
            }
            else
            {
                return null;
            }

            if (icon == null)
                return null;

            var imageSource = Imaging.CreateBitmapSourceFromHIcon(
                icon.Handle,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());

            return imageSource;
        }
        catch
        {
            return null;
        }
    }
}
