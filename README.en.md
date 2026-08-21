<div align="center">

# 🖥️ Portable Desktop

**A polished Windows desktop launcher that gathers your favorite files and apps into a personal icon panel**

[简体中文](README.md) | English

![.NET](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet&logoColor=white)
![WPF](https://img.shields.io/badge/UI-WPF-5C2D91?logo=microsoft&logoColor=white)
![Windows](https://img.shields.io/badge/Platform-Windows%2010%2B-0078D4?logo=windows11&logoColor=white)
![Tests](https://img.shields.io/badge/Tests-xUnit-512BD4?logo=xunit&logoColor=white)

</div>

---

**Drag and drop** your frequently used files, folders, or app shortcuts into the window — they instantly turn into a grid of beautiful icons. **One click** launches them. A frameless, rounded little window sits on your desktop, with four switchable themes to match your mood.

## ✨ Features

- 🖱️ **Drag & Drop** — Drop any file or shortcut into the window to add it to the icon grid (duplicates are ignored automatically)
- 🔗 **Shortcut Parsing** — Automatically reads the name, target path, and custom icon from `.lnk` files
- 🎯 **Real Icon Extraction** — Extracts the original program icons via the Win32 Shell API, rendered in high quality
- ⚡ **One-Click Launch** — Click any icon to instantly open the corresponding program or file
- 🗑️ **Right-Click Management** — Remove items in one right-click
- 🎨 **Four Themes** — Light / Pink / Acrylic / Eye-care Green; switch anytime, saved on the spot
- 🪟 **Frameless Elegance** — Rounded corners + soft shadow + custom title bar, with dragging, edge resizing, and maximize support
- 📌 **Remembers Everything** — Window position, size, and theme are persisted automatically — no more "lost" windows after changing monitors
- 🔒 **Single Instance** — Launching again simply brings the existing window to the front

All data is stored as plain JSON in `%LocalAppData%\PortableDesktop\` (`items.json` + `settings.json`) — lightweight, transparent, and easy to clean up.

## 📸 Screenshots

<!-- Once screenshots are ready, uncomment below and place the files under docs/screenshots/
| Light | Pink |
|:---:|:---:|
| ![Light theme](docs/screenshots/light.png) | ![Pink theme](docs/screenshots/pink.png) |
| **Acrylic** | **Eye-care Green** |
| ![Acrylic theme](docs/screenshots/acrylic.png) | ![Green theme](docs/screenshots/green.png) |
-->

> 🖼️ Placeholder: save theme screenshots to `docs/screenshots/` and uncomment the block above to display them.

## 🚀 Build & Run

### Prerequisites

- Windows 10 or later
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)

### Run Locally

```bash
git clone <your-repo-url>
cd PortableDesktop
dotnet run --project PortableDesktop
```

### Publish

```bash
# Framework-dependent (small; requires the .NET 9 runtime on the target machine)
dotnet publish PortableDesktop -c Release -o publish

# Or self-contained single file (no runtime installation needed, works out of the box)
dotnet publish PortableDesktop -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
```

### Run Tests

```bash
dotnet test
```

## 🛠️ Tech Stack

| Technology | Purpose |
|------------|---------|
| .NET 9 / WPF | App framework & UI |
| Win32 Shell API (`SHGetFileInfo` / `ExtractIconEx`) | Extracting real file icons |
| `WScript.Shell` COM | Parsing `.lnk` shortcuts |
| `System.Text.Json` | Lightweight local persistence |
| xUnit | Unit testing |

### Project Structure

```
PortableDesktop/
├── PortableDesktop/              # Main app
│   ├── Models/                   # Data models (DesktopItem / AppSettings)
│   ├── Services/                 # Core services (storage / icon extraction / shortcut parsing)
│   ├── Themes/                   # Four theme resource dictionaries
│   ├── MainWindow.xaml           # Main window (frameless rounded UI)
│   └── App.xaml.cs               # Entry point: single-instance control & dependency wiring
├── PortableDesktop.Tests/        # xUnit unit tests
└── PortableDesktop.sln
```
