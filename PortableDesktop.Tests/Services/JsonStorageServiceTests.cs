using PortableDesktop.Models;
using PortableDesktop.Services;
using Xunit;

namespace PortableDesktop.Tests.Services;

public class JsonStorageServiceTests
{
    private readonly string _testDir = Path.Combine(Path.GetTempPath(), "PDT_" + Guid.NewGuid());

    public JsonStorageServiceTests()
    {
        Directory.CreateDirectory(_testDir);
    }

    [Fact]
    public void LoadItems_ReturnsEmptyList_WhenFileMissing()
    {
        var service = new JsonStorageService(_testDir);
        var items = service.LoadItems();
        Assert.Empty(items);
    }

    [Fact]
    public void SaveItems_AndLoadItems_PreservesData()
    {
        var service = new JsonStorageService(_testDir);
        var items = new List<DesktopItem>
        {
            new DesktopItem { Name = "Notepad", TargetPath = "notepad.exe" }
        };

        service.SaveItems(items);
        var loaded = service.LoadItems();

        Assert.Single(loaded);
        Assert.Equal("Notepad", loaded[0].Name);
        Assert.Equal("notepad.exe", loaded[0].TargetPath);
    }

    [Fact]
    public void LoadSettings_ReturnsDefaults_WhenFileMissing()
    {
        var service = new JsonStorageService(_testDir);
        var settings = service.LoadSettings();
        Assert.Equal("Light", settings.Theme);
        Assert.Equal(500, settings.MainWindowWidth);
    }
}
