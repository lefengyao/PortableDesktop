using PortableDesktop.Models;
using System.IO;

namespace PortableDesktop.Services;

public class DesktopItemService
{
    private readonly JsonStorageService _storage;
    private readonly ShortcutParserService _shortcutParser;
    private List<DesktopItem> _items;

    public DesktopItemService(JsonStorageService storage, ShortcutParserService shortcutParser)
    {
        _storage = storage;
        _shortcutParser = shortcutParser;
        _items = storage.LoadItems();
    }

    public IReadOnlyList<DesktopItem> GetItems() => _items;

    public bool AddFromPath(string path)
    {
        if (_items.Any(i => string.Equals(i.TargetPath, path, StringComparison.OrdinalIgnoreCase)))
            return false;

        DesktopItem? item;
        if (path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
        {
            item = _shortcutParser.Parse(path);
            if (item == null)
                return false;
        }
        else
        {
            item = new DesktopItem
            {
                Name = Path.GetFileNameWithoutExtension(path),
                TargetPath = path,
                IconPath = path
            };
        }

        _items.Add(item);
        _storage.SaveItems(_items);
        return true;
    }

    public bool RemoveItem(string id)
    {
        var item = _items.FirstOrDefault(i => i.Id == id);
        if (item == null)
            return false;

        _items.Remove(item);
        _storage.SaveItems(_items);
        return true;
    }
}
