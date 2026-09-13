using PortableDesktop.Models;
using System.IO;
using System.Text.Json;

namespace PortableDesktop.Services;

public class JsonStorageService
{
    private readonly string _basePath;
    private readonly string _itemsPath;
    private readonly string _settingsPath;
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true
    };

    public JsonStorageService(string basePath)
    {
        _basePath = basePath;
        Directory.CreateDirectory(_basePath);
        _itemsPath = Path.Combine(_basePath, "items.json");
        _settingsPath = Path.Combine(_basePath, "settings.json");
    }

    public List<DesktopItem> LoadItems()
    {
        if (!File.Exists(_itemsPath))
            return new List<DesktopItem>();

        try
        {
            var json = File.ReadAllText(_itemsPath);
            if (string.IsNullOrWhiteSpace(json))
                return new List<DesktopItem>();

            return JsonSerializer.Deserialize<List<DesktopItem>>(json, Options) ?? new List<DesktopItem>();
        }
        catch (JsonException)
        {
            // Corrupted JSON: start with an empty collection
            return new List<DesktopItem>();
        }
    }

    public void SaveItems(List<DesktopItem> items)
    {
        var json = JsonSerializer.Serialize(items, Options);
        File.WriteAllText(_itemsPath, json);
    }

    public AppSettings LoadSettings()
    {
        if (!File.Exists(_settingsPath))
            return new AppSettings();

        try
        {
            var json = File.ReadAllText(_settingsPath);
            if (string.IsNullOrWhiteSpace(json))
                return new AppSettings();

            return JsonSerializer.Deserialize<AppSettings>(json, Options) ?? new AppSettings();
        }
        catch (JsonException)
        {
            // Corrupted JSON: reset to defaults
            return new AppSettings();
        }
    }

    public void SaveSettings(AppSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, Options);
        File.WriteAllText(_settingsPath, json);
    }
}
