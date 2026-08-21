using PortableDesktop.Services;
using Xunit;

namespace PortableDesktop.Tests.Services;

public class ShortcutParserServiceTests
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "PDT_Lnk_" + Guid.NewGuid());

    public ShortcutParserServiceTests()
    {
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public void Parse_ReturnsNull_ForNonLnkFile()
    {
        var service = new ShortcutParserService();
        var path = Path.Combine(_tempDir, "test.txt");
        File.WriteAllText(path, "hello");
        var result = service.Parse(path);
        Assert.Null(result);
    }

    [Fact]
    public void Parse_ReturnsTargetPath_ForLnkFile()
    {
        var service = new ShortcutParserService();
        var target = Path.Combine(_tempDir, "target.exe");
        File.WriteAllText(target, "fake exe");
        var lnkPath = Path.Combine(_tempDir, "shortcut.lnk");

        var shellType = Type.GetTypeFromProgID("WScript.Shell");
        dynamic shell = Activator.CreateInstance(shellType!)!;
        var shortcut = shell.CreateShortcut(lnkPath);
        shortcut.TargetPath = target;
        shortcut.Save();

        var result = service.Parse(lnkPath);
        Assert.NotNull(result);
        Assert.Equal(target, result!.TargetPath);
    }
}
