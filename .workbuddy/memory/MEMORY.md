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
- **动画生命周期铁律（2026-09-28 GPU 30% 事故）**：`RepeatBehavior="Forever"` 的动画
  （EmptyStatePulse）只能由 `UpdateEmptyState()` 按 count 启停，别处（尤其 Loaded）不得无条件 Begin ——
  空状态隐藏后时钟仍在每帧驱动全窗重渲染（分层窗口+全窗投影），实测 GPU 30%、CPU 80%/核。
  一次性动画（窗口/卡片入场）播完必须 Completed 里 `BeginAnimation(prop, null)` 摘除
  （HoldEnd 会永远挂住时钟）。用 `_pulseRunning` 标志判断状态，别对未 Begin 的
  Storyboard 调 `GetCurrentState()`（会抛异常）。
- **诊断渲染风暴只能外部观测**：GPU counter 按 pid 过滤 + CPU 时间增量；
  `CompositionTarget.Rendering` 挂 handler 本身就会强制每帧渲染，观察者效应污染实验。
- `MaxButton.Content` 一律用显式转义 `"\uE923"`/`"\uE922"`，不要贴裸 PUA 字符。

## 本机构建与发布
- 当前版本 **1.1.2**（csproj `<Version>`；1.1.1 = v1.1 动效版 + GPU 修复，1.1.2 = 安装程序父目录语义修正）。
- 沙箱里跑 dotnet 必须用 **`.dotnet.py`**（env 补丁：`ProgramFiles(x86)` 等被剥离的变量；
  根因是 NuGet 在 Windows 上读 `PROGRAMFILES(X86)` 取机器级设置目录，bash 设不了带括号名）：
  - 编译：`python .dotnet.py build PortableDesktop/PortableDesktop.csproj -c Release --nologo`
  - 发布自包含单文件：`python .dotnet.py publish PortableDesktop/PortableDesktop.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none -o publish-selfcontained`
- **发行版归档统一在 `release/<版本号>/`**（2026-09-29 老板定的规矩：按版本号整理）：
  - `PortableDesktop-<ver>-Setup.exe` 单文件安装程序（Burn，可选安装位置，推荐发用户）
  - `PortableDesktop-<ver>.msi` 静默部署用 · `PortableDesktop-<ver>-portable.exe` 自包含单文件
  - `PortableDesktop-<ver>-framework-dependent.zip`（约 170KB，需目标机有 .NET 9 桌面运行时）
  - 同目录 `SHA256SUMS.txt`；顶层 `release/README.md` 写用途/选型/重建命令，**发版时同步更新**
  - `.gitignore` 忽略 `release/**/*.{exe,msi,zip}`（二进制不入库），README 与校验和入库
  - 演练：`cd release/<ver> && sha256sum -c SHA256SUMS.txt`
- 构建输出先落临时目录（`.workbuddy/tmp/`）再归档进 `release/`；
  `publish/`、`publish-selfcontained/`、`PortableDesktop/{bin,obj}` 都是可重建的中间件，
  发完包随手清掉（一次清出 154MB）。清完验证可构建性：build sln（0 错误）+ test（8/8）。
- XAML 是否真编译：看 `obj/<cfg>/net9.0-windows/**/*.baml` 是否包含 `MainWindow` / `Controls/ItemCard` / `Themes/*`。
- 单元测试：`python .dotnet.py test PortableDesktop.Tests/PortableDesktop.Tests.csproj`（现在能跑了）。
- **MSI 安装包**（2026-09-29 新增）：源文件根目录 `portable-desktop.wxs`，WiX **6.0.2** 本地工具
  （`.config/dotnet-tools.json`，**别升 v7**——OSMF 付费 EULA 雷区）。扩展 UI/Util 也是 6.0.2
  （`wix extension add -g xxx/6.0.2`，不带版本会拉 7.0.0 不兼容）。构建：
  `python .dotnet.py wix build portable-desktop.wxs -arch x64 -ext WixToolset.UI.wixext -ext WixToolset.Util.wixext -o installer/PortableDesktop-<ver>-Setup.msi`
  - perUser 装到 `%LocalAppData%\Programs\PortableDesktop`，免管理员；数据目录独立，卸载不丢数据
  - 中文必须 `Package/@Codepage="936"`（CLI 没有 -codepage 开关）；Language=2052
  - **Icon 表别引用主 exe 当图标源**——会把 60MB exe 原样再嵌一份，MSI 体积翻倍；用 `PortableDesktop\app.ico`
  - 验证用 `msiexec /a <msi> /qn TARGETDIR=<temp>` 管理解包（非真实安装），比对 SHA256
- **单文件 EXE 安装包**（2026-09-29）：`installer/Bundle.wxs` + `zh-CN.wxl`（中文字符串表，
  从 OpsHelper 原样复用），构建命令见 Bundle.wxs 头注释。要点：主题必须 hyperlinkLargeLicense
  （小主题 Options 页没有目录选择布局）；需要 `-ext WixToolset.BootstrapperApplications.wixext/6.0.2`；
  InstallFolder 用 SetVariable 填实路径（OpsHelper Bundle.wxs 注释里有完整原理）
- **安装位置 = 父目录，不是最终安装目录**（2026-09-29 下午修正，别改回去）：
  `SetVariable` 默认 `[LocalAppDataFolder]Programs`；
  `MsiProperty INSTALLFOLDER="[InstallFolder]\PortableDesktop"`；
  `LaunchTarget="[InstallFolder]\PortableDesktop\PortableDesktop.exe"`。
  退化成"直接当安装目录"会重现老板 2026-09-29 报的问题：用户选到已有内容的目录时
  exe 跟自己的文件混在一起、卸载后目录还在。改语义必须同时升 Bundle/MSI/csproj 三处版本号，
  否则同版本重装 Burn 不认为在升级。
- 安装程序验证工具（都在 `Probe/`，从 OpsHelper/Probe/shot_zh_ui.py 移植）：
  `shot_setup_ui.py`（开 UI 截图 + 读控件，**不点安装**）、
  `e2e_install.py` / `e2e_verify_folder.py`（静默装/卸 + 目录结构断言）。
  五个必踩坑见技能 `wixstdba-ui-verification-windows`。
- ⚠ 沙箱**禁止在工作区外创建目录**（`D:\` 根直接 Permission denied），MSI 会报
  **1303「没有足够的特权来访问目录」**——这是**沙箱伪影，不是真实环境问题**。
  做安装位置实验必须选工作区内的目录，否则会误判成权限 bug。

## 版本快照
- **v1.0 基线只存在于 git**：标签 `v1.0-baseline`（提交 58504b8）。快照目录
  `versions/v1.0-baseline-20260913/` 已按用户要求移除，找回用
  `git checkout v1.0-baseline -- versions/v1.0-baseline-20260913`（内含 SHA-256 清单 SNAPSHOT.md）。
- `versions/v1.1-animated-20260913/`：动效 + UI 美化 + 代码重构版，变更见其 `CHANGELOG.md`
- `docs/ui-preview-v1.1.html`：新版 UI 的浏览器预览（不编译也能看效果）
