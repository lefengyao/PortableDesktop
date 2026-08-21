using PortableDesktop.Models;
using PortableDesktop.Services;
using Xunit;

namespace PortableDesktop.Tests.Services;

public class DesktopItemServiceTests
{
    private readonly string _testDir = Path.Combine(Path.GetTempPath(), "PDT_Svc_" + Guid.NewGuid());

    [Fact]
    public void AddFromExecutable_AddsItemWithCorrectTarget()
    {
        var storage = new JsonStorageService(_testDir);
        var service = new DesktopItemService(storage, new ShortcutParserService());
        var path = Path.Combine(_testDir, "app.exe");
        File.WriteAllText(path, "fake");

        service.AddFromPath(path);
        var items = service.GetItems();

        Assert.Single(items);
        Assert.Equal(path, items[0].TargetPath);
        Assert.Equal("app", items[0].Name);
    }

    [Fact]
    public void AddFromExecutable_DoesNotDuplicate()
    {
        var storage = new JsonStorageService(_testDir);
        var service = new DesktopItemService(storage, new ShortcutParserService());
        var path = Path.Combine(_testDir, "app.exe");
        File.WriteAllText(path, "fake");

        service.AddFromPath(path);
        service.AddFromPath(path);

        Assert.Single(service.GetItems());
    }

    [Fact]
    public void RemoveItem_RemovesById()
    {
        var storage = new JsonStorageService(_testDir);
        var service = new DesktopItemService(storage, new ShortcutParserService());
        var path = Path.Combine(_testDir, "app.exe");
        File.WriteAllText(path, "fake");

        service.AddFromPath(path);
        var id = service.GetItems()[0].Id;
        service.RemoveItem(id);

        Assert.Empty(service.GetItems());
    }
}
