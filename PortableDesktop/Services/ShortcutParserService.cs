using PortableDesktop.Models;
using System.IO;

namespace PortableDesktop.Services;

public class ShortcutParserService
{
    public DesktopItem? Parse(string lnkPath)
    {
        if (!lnkPath.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
            return null;

        try
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null)
                return null;

            dynamic shell = Activator.CreateInstance(shellType)!;
            dynamic shortcut = shell.CreateShortcut(lnkPath);
            string targetPath = shortcut.TargetPath;
            string iconLocation = shortcut.IconLocation ?? string.Empty;

            // 解析 IconLocation 格式: "path,index" 或 ",index"（空路径表示无自定义图标）
            var parts = iconLocation.Split(',');
            var iconPath = parts.Length > 0 ? parts[0].Trim() : string.Empty;
            var iconIndex = parts.Length > 1 && int.TryParse(parts[1], out var idx) ? idx : 0;

            // 如果图标路径为空，回退到目标文件路径
            if (string.IsNullOrEmpty(iconPath))
            {
                iconPath = targetPath;
                iconIndex = 0;
            }

            return new DesktopItem
            {
                Name = Path.GetFileNameWithoutExtension(lnkPath),
                TargetPath = targetPath,
                IconPath = iconPath,
                IconIndex = iconIndex
            };
        }
        catch
        {
            return null;
        }
    }
}
