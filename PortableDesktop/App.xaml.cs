using PortableDesktop.Models;
using PortableDesktop.Services;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;

namespace PortableDesktop;

public partial class App : Application
{
    private const string AppMutexName = "PortableDesktop_SingleInstance_Mutex";
    private static Mutex? _mutex;

    private JsonStorageService _storage = null!;
    private DesktopItemService _itemService = null!;
    private IconExtractorService _iconExtractor = null!;
    private AppSettings _settings = null!;
    private MainWindow _mainWindow = null!;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    private const int SW_RESTORE = 9;

    protected override void OnStartup(StartupEventArgs e)
    {
        // Multi-instance detection
        _mutex = new Mutex(true, AppMutexName, out bool createdNew);
        if (!createdNew)
        {
            // Another instance is running — activate it and exit
            ActivateExistingInstance();
            _mutex = null;
            Shutdown();
            return;
        }

        base.OnStartup(e);

        var dataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PortableDesktop");

        _storage = new JsonStorageService(dataPath);
        _settings = _storage.LoadSettings();
        ValidateAndFixSettings();
        _itemService = new DesktopItemService(_storage, new ShortcutParserService());
        _iconExtractor = new IconExtractorService();

        _mainWindow = new MainWindow(_itemService, _iconExtractor, _settings, SaveSettings);
        _mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Only save if we actually completed startup (skip on multi-instance early exit)
        if (_storage != null && _settings != null)
        {
            SaveSettings();
        }

        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        base.OnExit(e);
    }

    private static readonly string[] ValidThemes = { "Light", "Pink", "Acrylic", "Green" };

    private void ValidateAndFixSettings()
    {
        // Validate theme — if saved theme doesn't exist, fall back to Light
        if (!ValidThemes.Contains(_settings.Theme))
        {
            _settings.Theme = "Light";
            _storage.SaveSettings(_settings);
        }

        // Ensure window positions are on-screen (e.g. after monitor change)
        var screenW = SystemParameters.PrimaryScreenWidth;
        var screenH = SystemParameters.PrimaryScreenHeight;
        if (_settings.MainWindowLeft < -2000 || _settings.MainWindowLeft > screenW + 2000)
            _settings.MainWindowLeft = 100;
        if (_settings.MainWindowTop < -2000 || _settings.MainWindowTop > screenH + 2000)
            _settings.MainWindowTop = 100;
    }

    private void SaveSettings()
    {
        _storage.SaveSettings(_settings);
    }

    private static void ActivateExistingInstance()
    {
        // Find the existing main window by its title
        var handle = FindWindow(null, "便携桌面");
        if (handle != IntPtr.Zero)
        {
            if (IsIconic(handle))
                ShowWindow(handle, SW_RESTORE);
            SetForegroundWindow(handle);
        }
    }
}
