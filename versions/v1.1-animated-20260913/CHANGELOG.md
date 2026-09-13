# 便携桌面 v1.1 · 动画与视觉升级

> 快照日期：2026-09-13　·　基线：`versions/v1.0-baseline-20260913`（git 标签 `v1.0-baseline`）

本版聚焦四件事：**修掉图标顶格的布局问题**、**把命令式 UI 拆成可维护的 XAML**、
**加入一套克制的动效**、**顺手清掉几个真实缺陷**。API、数据文件格式
（`%LocalAppData%\PortableDesktop\items.json` / `settings.json`）与旧版完全兼容，可直接覆盖升级。

---

## 一、图标在卡片里顶格（用户反馈的问题）

**原因**：旧版 `MainWindow.CreateItemCard()` 把「图标 + 名称」的 `StackPanel` 直接作为
固定高度 90px 卡片的子元素。`StackPanel` 默认 `VerticalAlignment=Stretch`，
内容按自然顺序从顶部堆叠，图标（44px）+ 名称（约 16px）只占约 66px，
**底部空出约 24px**，视觉上就是"图标顶在上面、下面一大片空白"。

**修法**：卡片改用两行 `Grid`（`*` / `Auto`），图标盘放进弹性行并 `VerticalAlignment="Center"`，
名称固定贴底。图标于是在剩余空间里垂直居中，上下留白均衡。

```
旧：StackPanel 顶部堆叠        新：Grid 弹性行居中 + 名称贴底
┌──────────┐                  ┌──────────┐
│  ┌────┐  │ ← 图标顶到最上     │          │
│  │icon│  │                  │  ┌────┐  │ ← 上下留白均衡
│  └────┘  │                  │  │icon│  │
│  名称     │                  │  └────┘  │
│          │ ← 空 24px         │  名称     │
└──────────┘                  └──────────┘
```

卡片尺寸同时从 90×90 调整为 98×110，图标盘 44→56px、圆角 12→18、
图标 28→32px，四列布局密度不变。

## 二、代码结构

| 改动 | 说明 |
| --- | --- |
| 新增 `Controls/ItemCard.xaml(.cs)` | 卡片从 106 行 C# 手写代码变成 XAML 控件；颜色、圆角、阴影、动效全部声明式，`DynamicResource` 直接响应主题切换 |
| 新增 `Themes/Shared.xaml` | 与主题无关的「结构」层：圆角几何、滚动条 / 下拉框 / 菜单 / 窗口按钮模板、动效。文件头写明约定：**结构在 Shared，颜色只在主题里** |
| 新增 `Models/ThemeCatalog.cs` | 主题的单一真源。旧版在 `App.xaml.cs` 与 `MainWindow` 各维护一份主题名字符串数组，改主题要动两处、容易失配 |
| `MainWindow.xaml.cs` 重构 | 删除 `CreateItemCard`/`CreatePlaceholderIcon`/`FindResource` 取色的全部命令式代码（-150 行），改用 `ItemsControl` + `ObservableCollection<DesktopItem>` 数据绑定；`RefreshItems()` 不再重建整棵可视树 |
| 主题切换只换颜色字典 | 旧版 `MergedDictionaries.Clear()` 会把所有字典清掉重建；现在按 Source 定位主题字典原地替换，`Shared.xaml` 保持不变 |

## 三、动效（全部走 GPU 合成的 Scale/Translate/Opacity，不触发重新布局）

- **窗口启动**：整窗 opacity 0→1 并上浮 14px（260 / 340ms，CubicEase Out）
- **卡片入场**：淡入 + 上浮 10px，按序号每 30ms 错峰（最多错开 14 张）
- **卡片悬停**：scale 1→1.055、投影 11→20 模糊加深、高亮层渐显、图标 1→1.08（160ms）
- **卡片按下**：scale →0.94，抬起用 `BackEase` 回弹（70ms / 240ms）
- **主题切换**：内容区 opacity 0.45→1 短渐显（220ms），换色不突兀
- **拖拽遮罩**：淡入 140ms / 淡出 170ms
- **空状态**：占位图标 opacity 0.4↔1 呼吸（1.7s，AutoReverse）
- **窗口按钮**：悬停底色渐显、按下缩到 0.88；关闭按钮悬停变实心红底白字
- **滚动条**：6px 细胶囊滑轨，悬停/拖动加深
- **右键菜单**：圆角 + 投影 + 选项悬停高亮

## 四、顺手修掉的真实缺陷

1. **图标提取**：旧版对非 `.lnk` 文件调 `Icon.ExtractAssociatedIcon`，图片/文档类常常拿不到图标；
   现在统一走 `SHGetFileInfo` 外壳关联图标，任意文件类型都能取到。
   另外旧版取到 HICON 后从不 `DestroyIcon`，**每次刷新网格都在泄漏 GDI 句柄**，现已配对释放。
2. **图标缓存 + Freeze**：结果按「路径 + 索引」缓存，位图 `Freeze()` 后可被渲染线程直接复用。
   旧版每次 `RefreshItems()` 都要为每张卡片重新走一遍 Shell API。
3. **设置写盘防抖**：旧版 `LocationChanged`/`SizeChanged` 每次都 `File.WriteAllText`，
   拖动窗口时一秒能写几十次 JSON；现在 400ms 防抖，主题切换仍即时落盘。
4. **拖拽遮罩闪烁**：旧版在 `DragEnter`/`DragLeave` 上切换遮罩，鼠标在卡片与背景间移动会
   反复触发 `DragLeave` 导致闪烁；改为「`PreviewDragOver` 续期 + 140ms 空闲即隐藏」，只认一个信号源。
5. **最大化视觉**：最大化时收掉圆角、边框与外投影，屏幕边缘不再出现一圈阴影。
6. **`MaxButton.Content`**：裸 PUA 字符 `""` 改成显式转义 `"\uE923"` / `"\uE922"`，不怕编辑器破坏。
7. **新增**：右键菜单支持「打开 / 打开所在文件夹 / 从面板移除」，
   目标不存在时按扩展名取类型图标而不是灰块，标题栏实时显示条目数量。

## 五、验证

本机沙箱内 NuGet 无法运行（`ProgramData` 等环境变量被剥离，NuGet 解析机器级设置目录时抛
`Value cannot be null (Parameter 'path1')`），因此改用「跳过包资产解析」的方式做编译校验：

```bash
dotnet build PortableDesktop/PortableDesktop.csproj -c Debug --no-restore \
  -p:SkipResolvePackageAssets=true \
  -p:GenerateDependencyFile=false \
  -p:GenerateRuntimeConfigurationFiles=false
```

结果：**已成功生成。0 个警告，0 个错误。**

8 个 XAML 全部成功编译为 BAML：`App.baml`、`MainWindow.baml`、`Controls/ItemCard.baml`、
`Themes/{Light,Pink,Acrylic,Green}Theme.baml`、`Themes/Shared.baml`。

> 注意：该命令**只是本沙箱的编译校验手段**，产出的 exe 缺少 `deps.json`/`runtimeconfig.json`，不能直接运行。
> 在正常环境（有网络、NuGet 可用）请直接 `dotnet build` / `dotnet run`，无需任何特殊参数。

## 六、文件清单

新增：

```
PortableDesktop/Controls/ItemCard.xaml
PortableDesktop/Controls/ItemCard.xaml.cs
PortableDesktop/Models/ThemeCatalog.cs
PortableDesktop/Themes/Shared.xaml
docs/ui-preview-v1.1.html      ← 浏览器里直接看新版 UI（含 4 套主题与动效）
```

改写：`MainWindow.xaml`、`MainWindow.xaml.cs`、`App.xaml`、`App.xaml.cs`、
`Services/IconExtractorService.cs`、`Themes/*Theme.xaml`（四套色板重新生成，键集合统一）

未改动：`Models/DesktopItem.cs`、`Models/AppSettings.cs`、`Services/DesktopItemService.cs`、
`Services/JsonStorageService.cs`、`Services/ShortcutParserService.cs`、`PortableDesktop.csproj`
