# 发行版归档

按**版本号**分目录存放打包产物。每个版本目录内含 `SHA256SUMS.txt`（四个产物的校验和）。

## 目录约定

```
release/
  <版本号>/
    PortableDesktop-<版本号>-Setup.exe                单文件 EXE 安装程序（推荐给用户）
    PortableDesktop-<版本号>.msi                      MSI 安装包（静默 / 批量部署用）
    PortableDesktop-<版本号>-portable.exe             绿色便携版，双击即用，免安装
    PortableDesktop-<版本号>-framework-dependent.zip  框架依赖版（体积小，需目标机有 .NET 9 桌面运行时）
    SHA256SUMS.txt
```

## 各发行版怎么选

| 文件 | 体积 | 适用场景 | 目标机需求 |
|---|---|---|---|
| `-Setup.exe` | ~58MB | 装机给别人用：有向导、**可选安装位置**、带开始菜单/桌面快捷方式、可从「设置→应用」卸载 | 无（自包含） |
| `.msi` | ~57MB | 脚本批量部署：`msiexec /i xxx.msi /qn` | 无（自包含） |
| `-portable.exe` | ~63MB | 免安装，拷到哪跑哪（U 盘 / 临时机器） | 无（自包含） |
| `-framework-dependent.zip` | ~170KB | 本机开发自测，或目标机已装 .NET 9 运行时 | .NET 9 桌面运行时 |

四个包里装的都是**同一份程序**（自包含三个内嵌的 PortableDesktop.exe 逐字节一致）。

## 1.1.2

- **安装位置语义修正**：向导「选项」页填的是**父目录**，程序会在里面自动新建「便携桌面」子文件夹。
  1.1.1 把填的路径直接当最终目录，装到已有内容的目录时 exe 会跟用户文件混在一起、卸载后目录残留。
- 界面文案同步改为「安装位置（将新建「便携桌面」子文件夹）：」。
- 无管理员的用户级安装，默认装到 `%LocalAppData%\Programs\PortableDesktop`。

**用户数据不受影响**：配置与条目存在 `%LocalAppData%\PortableDesktop\`（`items.json` / `settings.json`），
与安装目录分离，覆盖安装或卸载都不会动它。

## 校验

```bash
cd release/1.1.2 && sha256sum -c SHA256SUMS.txt
```

## 从源码重建

```bash
# 1) 便携版（自包含单文件）
python .dotnet.py publish PortableDesktop/PortableDesktop.csproj -c Release -r win-x64 \
  --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none -o publish-selfcontained

# 2) 框架依赖版
python .dotnet.py publish PortableDesktop/PortableDesktop.csproj -c Release -o publish

# 3) MSI（源文件在仓库根目录）
python .dotnet.py wix build portable-desktop.wxs -arch x64 \
  -ext WixToolset.UI.wixext -ext WixToolset.Util.wixext -o installer/PortableDesktop-<版本号>.msi

# 4) 单文件 EXE 安装程序（源文件在 installer/，完整命令见 Bundle.wxs 头注释）
```

重建前先把 `PortableDesktop.csproj` 的 `<Version>` 与两个 wxs 里的 `Version` 一起升号，
否则 Burn 不认为在升级（同版本重装会被当成已安装）。

> 之前的 1.1.0 / 1.1.1 产物未归档于此——1.1.1 的安装包在 1.1.2 修正后已删除，避免误用。
> 历史版本可从 git 标签恢复源码重建（`v1.0-baseline`，以及 `versions/v1.1-animated-20260913/`）。
