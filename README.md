<div align="center">

# 🖥️ 便携桌面

**一枚精致的 Windows 桌面启动器，把常用文件和程序收进你的专属图标面板**

[简体中文](README.md) | [English](README.en.md)

![.NET](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet&logoColor=white)
![WPF](https://img.shields.io/badge/UI-WPF-5C2D91?logo=microsoft&logoColor=white)
![Windows](https://img.shields.io/badge/Platform-Windows%2010%2B-0078D4?logo=windows11&logoColor=white)
![Tests](https://img.shields.io/badge/Tests-xUnit-512BD4?logo=xunit&logoColor=white)

</div>

---

把常用的文件、文件夹或程序快捷方式**拖进来**，它们就会变成一格格漂亮的图标。之后只需**单击一下**，即可瞬间启动 —— 无边框圆角小窗常驻桌面，四套主题随心切换。

## ✨ 功能特性

- 🖱️ **拖拽添加** — 把任意文件或快捷方式拖进窗口，自动收入图标网格（重复条目自动忽略）
- 🔗 **快捷方式解析** — 自动读取 `.lnk` 的名称、目标路径与自定义图标
- 🎯 **真实图标提取** — 调用 Win32 Shell API 提取程序原装图标，高清晰度渲染
- ⚡ **单击启动** — 点一下图标，立即打开对应的程序或文件
- 🗑️ **右键管理** — 右键菜单即可移除不需要的条目
- 🎨 **四套主题** — 浅色 / 粉色 / 毛玻璃 / 护眼绿，一键切换、即改即存
- 🪟 **精致无边框窗口** — 圆角 + 柔和阴影 + 自定义标题栏，支持拖动、边缘缩放与最大化
- 📌 **记住一切** — 窗口位置、大小与主题选择自动持久化，换显示器也不怕窗口"跑丢"
- 🔒 **单实例运行** — 重复启动时自动唤起已有窗口并置前

数据以纯 JSON 形式保存在 `%LocalAppData%\PortableDesktop\`（`items.json` + `settings.json`），轻量透明、随删随走。

## 📸 界面预览

<!-- 截图准备就绪后，取消下方注释并把文件放入 docs/screenshots/ 即可展示
| 浅色 | 粉色 |
|:---:|:---:|
| ![浅色主题](docs/screenshots/light.png) | ![粉色主题](docs/screenshots/pink.png) |
| **毛玻璃** | **护眼绿** |
| ![毛玻璃主题](docs/screenshots/acrylic.png) | ![护眼绿主题](docs/screenshots/green.png) |
-->

> 🖼️ 截图占位：将各主题截图保存到 `docs/screenshots/` 目录后，取消上方注释即可自动展示。

## 🚀 构建与运行

### 环境要求

- Windows 10 及以上
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)

### 本地运行

```bash
git clone <your-repo-url>
cd PortableDesktop
dotnet run --project PortableDesktop
```

### 发布为独立程序

```bash
# 框架依赖（体积小，需目标机器装有 .NET 9 运行时）
dotnet publish PortableDesktop -c Release -o publish

# 或发布为自包含单文件（免安装运行时，开箱即用）
dotnet publish PortableDesktop -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
```

### 运行测试

```bash
dotnet test
```

## 🛠️ 技术栈

| 技术 | 用途 |
|------|------|
| .NET 9 / WPF | 应用框架与 UI |
| Win32 Shell API (`SHGetFileInfo` / `ExtractIconEx`) | 提取文件真实图标 |
| `WScript.Shell` COM | 解析 `.lnk` 快捷方式 |
| `System.Text.Json` | 轻量本地持久化 |
| xUnit | 单元测试 |

### 项目结构

```
PortableDesktop/
├── PortableDesktop/              # 主程序
│   ├── Models/                   # 数据模型（DesktopItem / AppSettings）
│   ├── Services/                 # 核心服务（存储 / 图标提取 / 快捷方式解析）
│   ├── Themes/                   # 四套主题资源字典
│   ├── MainWindow.xaml           # 主窗口（无边框圆角 UI）
│   └── App.xaml.cs               # 入口：单实例控制与依赖装配
├── PortableDesktop.Tests/        # xUnit 单元测试
└── PortableDesktop.sln
```
