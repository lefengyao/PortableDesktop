# 便携桌面 · 项目长期记忆

WPF (.NET 9) 桌面启动器。把文件/快捷方式拖进无边框圆角窗口，单击启动。
数据落在 `%LocalAppData%\PortableDesktop\`（`items.json` + `settings.json`），格式向后兼容。

## 架构约定
- `Models/`：`DesktopItem`、`AppSettings`、`ThemeCatalog`（**主题的单一真源**，下拉/校验/资源路径都从这里取）
- `Services/`：`JsonStorageService`（持久化）、`DesktopItemService`（增删与去重）、
  `ShortcutParserService`（WScript.Shell 解析 .lnk）、`IconExtractorService`（Win32 Shell API 取图标 + 缓存）
- `Themes/`：`Shared.xaml` 放**结构**（圆角几何、ScrollBar/ComboBox/ContextMenu/窗口按钮模板、动效）；
  `{Light,Pink,Acrylic,Green}Theme.xaml` **只放颜色**。四套主题的键集合必须完全一致。
- `Controls/ItemCard.xaml(.cs)`：单个应用卡片，布局/配色/动效全在 XAML；通过 `Item`/`IconService`/`EntranceIndex`
  三个依赖属性接数据，用 `LaunchRequested`/`RevealRequested`/`RemoveRequested` 事件回调宿主。
- `MainWindow.xaml(.cs)`：`ItemsControl` + `ObservableCollection<DesktopItem>` 绑定，不再手写可视树。

## 硬性约束
- **卡片 → 窗口的连接靠 XAML 绑定**：`MainWindow` 必须暴露 `public IconExtractorService IconService`
  （DataTemplate 里用 `{Binding IconService, RelativeSource={RelativeSource AncestorType=Window}}` 取）。
  重命名/删掉它不会有任何编译错误，只会让图标整片消失 —— **WPF 绑定失败是静默的**，
  改这里务必实机启动看一眼。`ItemCard.IconService` 的默认值已设为 `IconExtractorService.Shared` 兜底。
- **切换主题只替换颜色字典**：按 `Source` 找到 `Themes/*Theme.xaml` 原地替换，绝不能 `MergedDictionaries.Clear()`
  （会把 `Shared.xaml` 里的模板和动效一起清掉）。
- **卡片布局**：卡片 98×110 / 圆角 16，内边距 9,8,9,9；图标盘 56×56 / 圆角 18；图标 32px；
  两行 `Grid`（`*` + `Auto`），**图标盘必须在弹性行里垂直居中**（这是修掉"图标顶格"的关键），名称贴底。
- 卡片缩放靠 `ScaleTransform`，只能微调到 1.055 —— 卡片间距 10px，再大就会压到邻居。
- 窗口阴影：最大化时必须置 `WindowBorder.Effect = null` 并收掉圆角/边框，否则屏幕边缘会出现一圈阴影。
- `MaxButton.Content` 一律用显式转义 `"\uE923"`/`"\uE922"`，不要贴裸 PUA 字符。

## 本机验证
- **项目零 NuGet 依赖**（图标提取纯 Win32 Shell API，WPF 由 net9.0-windows 自带）——
  不要再给它加包引用，加了就会退回"必须联网还原"的状态。
- 沙箱内 NuGet 不可用（详见用户级 `~/.workbuddy/MEMORY.md`），编译校验用：
  `dotnet build PortableDesktop/PortableDesktop.csproj -c Release --no-restore -p:SkipResolvePackageAssets=true -p:GenerateDependencyFile=false -p:GenerateRuntimeConfigurationFiles=false`
- XAML 是否真编译：看 `obj/<cfg>/net9.0-windows/**/*.baml` 是否包含 `MainWindow` / `Controls/ItemCard` / `Themes/*`。
- **运行验证**：上一条命令产不出 `deps.json` / `runtimeconfig.json`，可在 `bin/<cfg>/net9.0-windows/` 里
  手写一份最小 `deps.json`（零包依赖格式）并沿用旧的 `runtimeconfig.json`，然后启动进程、
  用 `EnumWindows` 枚举该 PID 窗口，能看到标题「便携桌面」的可见窗口即说明 XAML 运行时无恙。
- 单元测试（xUnit）在本沙箱跑不了（需要还原 xunit 包），需在正常环境执行 `dotnet test`。
- `publish/` 里放的是可直接运行的成品（当前是手工补齐的两个 json + 真实构建产物）。

## 版本快照
- **v1.0 基线只存在于 git**：标签 `v1.0-baseline`（提交 58504b8）。快照目录
  `versions/v1.0-baseline-20260913/` 已按用户要求移除，找回用
  `git checkout v1.0-baseline -- versions/v1.0-baseline-20260913`（内含 SHA-256 清单 SNAPSHOT.md）。
- `versions/v1.1-animated-20260913/`：动效 + UI 美化 + 代码重构版，变更见其 `CHANGELOG.md`
- `docs/ui-preview-v1.1.html`：新版 UI 的浏览器预览（不编译也能看效果）
