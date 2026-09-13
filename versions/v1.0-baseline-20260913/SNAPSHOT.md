# 快照清单 · v1.0-baseline

> 这是「便携桌面」**优化前**的原始版本冻结副本，仅作对照与回滚用，不参与编译。

- 快照日期：2026-09-13
- 对应 Git 提交：`8c3d0a4c5b9f032fe65fe26ac043fb9ea7f028c5`（标签 `v1.0-baseline`）
- 文件数：17

## 文件校验（SHA-256）

| 文件 | 大小 (B) | SHA-256 |
| --- | ---: | --- |
| `PortableDesktop/app.ico` | 16629 | `f9f16465f4763b5d4c276d9ec751612c64fd350bdefc2b15d653d2f54eb384ef` |
| `PortableDesktop/App.xaml` | 497 | `a71f8dee15a88b231155e00dfdcebbf1f76f6b2db2742b5faa789e833af36ed1` |
| `PortableDesktop/App.xaml.cs` | 3636 | `d5eb675e67e14a0987e82eb2de4906f85b32439627892f34d0b180530d2f0e78` |
| `PortableDesktop/AssemblyInfo.cs` | 643 | `3fac2af92385f676e9a941befd23fe38f0a5fa6167d4a3a88f07e730c5595ca3` |
| `PortableDesktop/MainWindow.xaml` | 13627 | `5d44fafd78fdff65ee7870b9032538ef30ac2c9846f6702401e492784b715114` |
| `PortableDesktop/MainWindow.xaml.cs` | 11442 | `66a7a04f289c45d9a8e9137a81d17e142f1a47dc875fa38058b0321fa90023a6` |
| `PortableDesktop/Models/AppSettings.cs` | 331 | `ba811e0a8f8afc609faf81ca9c3a737e1810b821a477ed39130ded90920a2876` |
| `PortableDesktop/Models/DesktopItem.cs` | 339 | `71b439953f9b19504392e5d018b19336cfdb3f0f10f61511c7da09b31bc8cb2e` |
| `PortableDesktop/PortableDesktop.csproj` | 512 | `d7db24fee6c9682ae15352ed4866c54aeefefea0613be19ab8776084d09f64b6` |
| `PortableDesktop/Services/DesktopItemService.cs` | 1514 | `0bf379b60ca45ede9b016f76dcbda217e81f167164796513d9ff3e8a2adb4d4f` |
| `PortableDesktop/Services/IconExtractorService.cs` | 3250 | `b89f20fd6b72b1205f364d02d3fed049fcea8b808ef39c2edec3cf713623a4e0` |
| `PortableDesktop/Services/JsonStorageService.cs` | 2111 | `b224f39d20623b3bee55a41fe9ac9a02b7e52d6cd3f743abac28ee67e848b249` |
| `PortableDesktop/Services/ShortcutParserService.cs` | 1557 | `342744b9651dfb2c327acfe5466ddfd53414118f58a3ee67b8b7a87c15d7df0f` |
| `PortableDesktop/Themes/AcrylicTheme.xaml` | 3606 | `6d6103871687d0cda9f9475982bc4714548cd99e0aee1349968129e8f1baccaf` |
| `PortableDesktop/Themes/GreenTheme.xaml` | 3606 | `3d01d27cd56ecc10895c36f272558dd539e1b20c5f37244dde7acbf0185d7621` |
| `PortableDesktop/Themes/LightTheme.xaml` | 3594 | `888f281ebeafaf7851d85ea15b47937a7e0b2269cb7a0935aed053be37c9548d` |
| `PortableDesktop/Themes/PinkTheme.xaml` | 3575 | `d6236db435dd8f42b2ff203ddf8a7222cfe1686ba8bd5a82b13438947497eefd` |

## 对照要点（下一版修的问题）

1. **图标在卡片内整体偏上顶格**：`MainWindow.CreateItemCard` 用 `StackPanel` 直接塞进固定高 90 的卡片，内容自然从顶部堆叠，底部留出约 24px 空白。
2. **卡片构建全在 C# 里手写**：颜色、圆角、阴影、悬停逻辑硬编码，主题切换只能靠 `FindResource` 且不响应动态变更。
3. **图标重复提取**：`RefreshItems()` 每次重建都重新调用 Win32 提取图标，无缓存；提取出的 HICON 句柄未释放。
4. **设置高频落盘**：窗口拖动/缩放时 `LocationChanged`/`SizeChanged` 每次都写 JSON。
5. **主题常量重复三处**：`App.xaml.cs`、`MainWindow` 各维护一份主题名数组，易失配。
6. **`MaxButton.Content` 使用裸 PUA 字符**：源码里是字面量 `\ue923`/`\ue922`，易被编辑器破坏。
7. **无任何动效**：悬停仅瞬间换色，无过渡、无入场、无按压反馈。
