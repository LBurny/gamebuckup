# GameBackup Phase 1（骨架 + UI 壳）Implementation Plan

> **For agentic workers:** Implement this plan in the current session. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 交付一个可在 Playnite 10.63 中加载的 GameBackup 插件骨架，带与 Playnite 主题一致的侧边栏完整页面（游戏列表、详情、设置），为后续定位链与备份引擎提供稳定边界。

**Architecture:** 单进程分层插件：`GameBackup.Core`（netstandard2.0，无 UI/无 Playnite 依赖）+ `GameBackup.Plugin`（net462 + WPF + Playnite.SDK）。Core 通过接口暴露能力，Plugin 负责 Playnite 适配、线程调度与 WPF 界面。

**Tech Stack:** C# / .NET SDK 8.0.426（编译 net462）、WPF、Playnite.SDK 10.63、xUnit（Core 测试）、PowerShell（构建/安装脚本）。

## Global Constraints

- 目标框架：`net462`（插件）、`netstandard2.0`（Core）；语言版本 `latest`。
- 不引入任何 x64 原生依赖；Playnite 为 x86 进程。
- Core 不得引用 `Playnite.SDK`、`PresentationFramework`、`System.Windows`。
- 所有用户可见文案集中管理，Phase 1 用 `zh-CN` 资源字典，键名英文。
- UI 颜色/字体/圆角一律优先使用 Playnite 主题资源，禁止硬编码主题色；仅保留兜底资源。
- 所有长操作异步、可取消；Playnite 事件处理不得抛出异常。
- 构建使用 `D:\Code\dotnet\dotnet.exe`（PATH 上的 `dotnet` 无 SDK）。
- 插件目录名固定为 `GameBackup`，扩展 Id 固定为 `GameBackup`。

---

## File Structure

```
H:\Softwares\GameBackup\
  GameBackup.sln
  Directory.Build.props
  global.json
  README.md
  docs\superpowers\plans\2026-11-08-gamebackup-phase1.md
  src\GameBackup.Core\
    GameBackup.Core.csproj
    Models\GameDescriptor.cs
    Models\SaveCandidate.cs
    Models\Enums.cs
    Models\LocateResult.cs
    GameDescriptorFactory.cs
  src\GameBackup.Plugin\
    GameBackup.Plugin.csproj
    extension.yaml
    icon.png
    GameBackupPlugin.cs
    Adapters\PlayniteGameSource.cs
    Adapters\ImageLoader.cs
    Settings\GameBackupSettings.cs
    Settings\SettingsViewModel.cs
    Settings\SettingsView.xaml(.cs)
    UI\ThemeBridge.cs
    UI\Styles.xaml
    UI\Infrastructure\ObservableObject.cs
    UI\Infrastructure\RelayCommand.cs
    UI\GameBackupPage.xaml(.cs)
    UI\ViewModels\GameBackupViewModel.cs
    UI\ViewModels\GameRowViewModel.cs
    UI\Converters\Converters.cs
  tests\GameBackup.Core.Tests\
    GameBackup.Core.Tests.csproj
    GameDescriptorTests.cs
    LocateResultTests.cs
  build\build.ps1
  build\dev-install.ps1
```

---

### Task 1: 解决方案骨架与构建管线

**Files:**
- Create: `GameBackup.sln`, `Directory.Build.props`, `global.json`, `README.md`
- Create: `src/GameBackup.Core/GameBackup.Core.csproj`
- Create: `src/GameBackup.Plugin/GameBackup.Plugin.csproj`
- Create: `tests/GameBackup.Core.Tests/GameBackup.Core.Tests.csproj`
- Create: `build/build.ps1`, `build/dev-install.ps1`

**Interfaces:**
- Produces: `dotnet build GameBackup.sln` 成功；`build/dev-install.ps1` 将产物复制到 `%APPDATA%\Playnite\Extensions\GameBackup`。

- [ ] **Step 1: 创建 `global.json`**

```json
{
  "sdk": {
    "version": "8.0.426",
    "rollForward": "latestPatch"
  }
}
```

- [ ] **Step 2: 创建 `Directory.Build.props`**

```xml
<Project>
  <PropertyGroup>
    <LangVersion>latest</LangVersion>
    <Nullable>disable</Nullable>
    <ImplicitUsings>disable</ImplicitUsings>
    <Deterministic>true</Deterministic>
    <PlayniteDir Condition="'$(PlayniteDir)' == ''">D:\Playnite\Playnite</PlayniteDir>
  </PropertyGroup>
</Project>
```

- [ ] **Step 3: 创建 Core 项目**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <AssemblyName>GameBackup.Core</AssemblyName>
    <RootNamespace>GameBackup.Core</RootNamespace>
  </PropertyGroup>
</Project>
```

- [ ] **Step 4: 创建 Plugin 项目**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net462</TargetFramework>
    <UseWPF>true</UseWPF>
    <AssemblyName>GameBackup.Plugin</AssemblyName>
    <RootNamespace>GameBackup.Plugin</RootNamespace>
    <PlatformTarget>AnyCPU</PlatformTarget>
    <Prefer32Bit>true</Prefer32Bit>
    <GenerateAssemblyInfo>true</GenerateAssemblyInfo>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\GameBackup.Core\GameBackup.Core.csproj" />
    <PackageReference Include="Microsoft.NETFramework.ReferenceAssemblies.net462" Version="1.0.3" PrivateAssets="all" />
    <Reference Include="Playnite.SDK">
      <HintPath>$(PlayniteDir)\Playnite.SDK.dll</HintPath>
      <Private>false</Private>
    </Reference>
  </ItemGroup>
</Project>
```

- [ ] **Step 5: 创建测试项目**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
    <ProjectReference Include="..\..\src\GameBackup.Core\GameBackup.Core.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 6: 构建验证**

Run:
```powershell
$env:DOTNET_ROOT='D:\Code\dotnet'
& 'D:\Code\dotnet\dotnet.exe' build GameBackup.sln -c Debug
```
Expected: `Build succeeded`，0 errors。

- [ ] **Step 7: 创建设置-安装脚本**

`build/build.ps1`：调用 `dotnet build`，参数 `-Configuration Debug|Release`。
`build/dev-install.ps1`：清空并重建 `%APPDATA%\Playnite\Extensions\GameBackup`，复制 `GameBackup.Plugin.dll`、`GameBackup.Core.dll`、`extension.yaml`、`icon.png` 及依赖 DLL；提示“重启 Playnite 生效”。

---

### Task 2: Core 基础模型

**Files:**
- Create: `src/GameBackup.Core/Models/GameDescriptor.cs`
- Create: `src/GameBackup.Core/Models/Enums.cs`
- Create: `src/GameBackup.Core/Models/SaveCandidate.cs`
- Create: `src/GameBackup.Core/Models/LocateResult.cs`
- Create: `src/GameBackup.Core/GameDescriptorFactory.cs`
- Test: `tests/GameBackup.Core.Tests/GameDescriptorTests.cs`, `tests/GameBackup.Core.Tests/LocateResultTests.cs`

**Interfaces:**
- Produces:
  - `GameDescriptor`：`Key/Name/InstallDirectory/Source/StoreGameId/Platform/IsInstalled/Fingerprint`
  - `GameDescriptorFactory.Create(...) -> GameDescriptor`
  - `SaveStatus`：`Unknown | Candidate | Confirmed | Stale | Error`
  - `SaveCandidate`、`LocateResult`

- [ ] **Step 1: 写失败测试**

```csharp
[Fact]
public void Fingerprint_changes_when_install_directory_changes()
{
    var a = GameDescriptorFactory.Create("id", "Game", @"C:\Games\A", "Steam", "10", "PC", true);
    var b = GameDescriptorFactory.Create("id", "Game", @"C:\Games\B", "Steam", "10", "PC", true);
    Assert.NotEqual(a.Fingerprint, b.Fingerprint);
}

[Fact]
public void Fingerprint_is_stable_across_case_and_separator_changes()
{
    var a = GameDescriptorFactory.Create("id", "Game", @"C:\Games\A", "Steam", "10", "PC", true);
    var b = GameDescriptorFactory.Create("id", "Game", @"c:/games/a/", "Steam", "10", "PC", true);
    Assert.Equal(a.Fingerprint, b.Fingerprint);
}
```

- [ ] **Step 2: 运行测试确认失败**

Run: `& 'D:\Code\dotnet\dotnet.exe' test tests\GameBackup.Core.Tests -c Debug`
Expected: 编译失败，`GameDescriptorFactory` 不存在。

- [ ] **Step 3: 实现模型**

关键实现（Fingerprint 归一化）：

```csharp
public static GameDescriptor Create(string key, string name, string installDirectory, string source,
    string storeGameId, string platform, bool isInstalled)
{
    var normalized = NormalizePath(installDirectory);
    var raw = string.Join("|", key ?? "", name ?? "", normalized, storeGameId ?? "");
    return new GameDescriptor
    {
        Key = key, Name = name ?? "", InstallDirectory = installDirectory ?? "",
        Source = source ?? "", StoreGameId = storeGameId ?? "",
        Platform = platform ?? "", IsInstalled = isInstalled,
        Fingerprint = Sha1Hex(raw)
    };
}

private static string NormalizePath(string path) =>
    (path ?? "").Replace('/', '\\').TrimEnd('\\').ToLowerInvariant();
```

`Sha1Hex` 使用 `System.Security.Cryptography.SHA1`，输出小写十六进制。

- [ ] **Step 4: 运行测试确认通过**

Run: `& 'D:\Code\dotnet\dotnet.exe' test tests\GameBackup.Core.Tests -c Debug`
Expected: PASS（2 tests）。

---

### Task 3: UI 主题桥与设计系统

**Files:**
- Create: `src/GameBackup.Plugin/UI/ThemeBridge.cs`
- Create: `src/GameBackup.Plugin/UI/Styles.xaml`
- Create: `src/GameBackup.Plugin/UI/Infrastructure/ObservableObject.cs`
- Create: `src/GameBackup.Plugin/UI/Infrastructure/RelayCommand.cs`
- Create: `src/GameBackup.Plugin/UI/Converters/Converters.cs`

**Interfaces:**
- Produces:
  - `ThemeBridge.Ensure(FrameworkElement target)`：为缺失的 Playnite 主题键注入兜底资源。
  - XAML 资源键：`GB.Card`、`GB.CardBorder`、`GB.Pill`、`GB.PrimaryButton`、`GB.SecondaryButton`、`GB.IconButton`、`GB.Chip`、`GB.ListRow`、`GB.SectionTitle`、`GB.Muted`。
  - `BoolToVisibilityConverter`、`NullToVisibilityConverter`、`PathToImageConverter`、`StatusBrushConverter`。

**Design tokens（必须使用 DynamicResource）：**

| 用途 | Playnite 键 | 兜底 |
|---|---|---|
| 正文 | `TextBrush` | `#E6E6E6` |
| 次要文字 | `TextBrush` + Opacity 0.62 | 同左 |
| 面板背景 | `ControlBackgroundBrush` | `#252526` |
| 页面背景 | `WindowBaseBackgroundBrush` | `#1E1E1E` |
| 分隔线 | `PanelSeparatorBrush` | `#3A3A3A` |
| 强调色 | `HighlightGlyphBrush` | `#4C9AFF` |
| 悬停 | `HoverBrush` | `#33FFFFFF` |
| 主按钮背景 | `ButtonBackgroundBrush` | `#333333` |
| 圆角 | `ControlCornerRadius` | `4` |

- [ ] **Step 1: 实现 `ThemeBridge`**

对 `TextBrush/ControlBackgroundBrush/WindowBaseBackgroundBrush/PanelSeparatorBrush/HighlightGlyphBrush/HoverBrush/ButtonBackgroundBrush/ButtonIsMouseOverBackgroundBrush/GlyphBrush/ControlCornerRadius/ControlCornerRadiusSmall/FontFamily/FontSize/FontSizeSmall/FontSizeLarge` 逐一 `TryFindResource`；缺失则 `target.Resources[key] = fallback`。颜色键为 `SolidColorBrush`（`Freeze()`），圆角为 `CornerRadius`，字体为 `FontFamily`。

- [ ] **Step 2: 实现 `Styles.xaml`**

包含：卡片（圆角、1px 边框、透明背景）、主/次/图标按钮（悬停色、按下色、禁用透明度）、筛选 Chip（选中时强调色底）、列表行（选中左侧 2px 强调条）、状态 Pill、标题/正文/次要文字样式。所有颜色引用 `DynamicResource`。

- [ ] **Step 3: 实现基础设施**

`ObservableObject`（`INotifyPropertyChanged` + `Set`）、`RelayCommand`（同步 + 异步版本，带 `CanExecute` 与 `IsRunning`）、转换器。

- [ ] **Step 4: 构建验证**

Run: `& 'D:\Code\dotnet\dotnet.exe' build src\GameBackup.Plugin -c Debug`
Expected: 成功。

---

### Task 4: Playnite 适配层与插件入口

**Files:**
- Create: `src/GameBackup.Plugin/extension.yaml`
- Create: `src/GameBackup.Plugin/icon.png`
- Create: `src/GameBackup.Plugin/GameBackupPlugin.cs`
- Create: `src/GameBackup.Plugin/Adapters/PlayniteGameSource.cs`
- Create: `src/GameBackup.Plugin/Adapters/ImageLoader.cs`
- Create: `src/GameBackup.Plugin/Settings/GameBackupSettings.cs`

**Interfaces:**
- Produces:
  - `PlayniteGameSource.Snapshot()` 返回 `IReadOnlyList<GameDescriptor>`（在 UI 线程调用）。
  - `ImageLoader.Load(string path, int decodeWidth)` 返回冻结的 `BitmapImage`。
  - `GameBackupSettings`：`BackupRoot`、`AutoBackupOnExit`、`KeepBackups`、`EnableLlm`、`LlmEndpoint`、`LlmModel`、`LlmApiKeyProtected`。

- [ ] **Step 1: 扩展清单**

```yaml
Id: GameBackup
Name: GameBackup
Author: <your name>
Version: 0.1.0
Module: GameBackup.Plugin.dll
Type: GenericPlugin
Icon: icon.png
Links:
  - Name: Project
    Url: https://example.invalid/gamebackup
```

- [ ] **Step 2: 插件类**

`GameBackupPlugin : GenericPlugin`，构造函数保存 `IPlayniteAPI`；重写 `GetSidebarItems()` 返回单个 `SidebarItem { Title="GameBackup", Type=View, Icon=..., Opened=() => _page }`；`GetGameMenuItems()` 暂时返回空列表（Phase 3 接入）。

- [ ] **Step 3: 游戏快照**

`PlayniteGameSource.Snapshot()` 遍历 `api.Database.Games`，过滤 `IsInstalled != false`（含 `Manual` 游戏），用 `GameDescriptorFactory.Create` 映射：
- `key = "playnite:" + game.Id`
- `streamGameId`：若 `game.PluginId` 对应 Steam 库则取 `game.GameId`，否则空字符串
- `source = game.Source?.Name`，`platform = game.Platforms?.FirstOrDefault()?.Name`

- [ ] **Step 4: 构建并安装验证**

Run:
```powershell
& 'D:\Code\dotnet\dotnet.exe' build GameBackup.sln -c Debug
powershell -ExecutionPolicy Bypass -File build\dev-install.ps1
```
启动 Playnite，确认侧边栏出现 GameBackup 项。

---

### Task 5: 游戏库页面（列表 + 头部 + 筛选）

**Files:**
- Create: `src/GameBackup.Plugin/UI/ViewModels/GameRowViewModel.cs`
- Create: `src/GameBackup.Plugin/UI/ViewModels/GameBackupViewModel.cs`
- Create: `src/GameBackup.Plugin/UI/GameBackupPage.xaml(.cs)`

**Interfaces:**
- Produces:
  - `GameBackupViewModel`：`Games`、`FilteredGames`、`SelectedGame`、`SearchText`、`Filter`、`SummaryText`、`RefreshCommand`、`OpenSettingsCommand`。
  - `GameRowViewModel`：`Descriptor`、`Name`、`CoverPath`、`SourceLabel`、`Status`、`LastBackupText`、`BackupSizeText`。

- [ ] **Step 1: ViewModel 逻辑**

筛选优先级：搜索（大小写不敏感，匹配名称/来源）→ 状态筛选 → 排序（已定位优先，其次名称）。

- [ ] **Step 2: 页面布局**

```
Grid Rows: Auto(header) / *
  Header: 标题 + 统计 + 搜索框 + 刷新 + 设置
  Body: Grid Columns 320 / 1
    Left: 筛选 Chips + ListBox(虚拟化) + 空状态
    Right: 详情面板（Task 6）
```

- [ ] **Step 3: 列表项模板**

44×60 封面（圆角 4、`UniformToFill`，无封面显示首字母方块）→ 名称 + 来源 → 状态点 + 相对时间 → 右侧备份大小。选中行左侧 2px 强调条。

- [ ] **Step 4: 封面加载**

`PathToImageConverter` 使用 `ImageLoader.Load(path, 96)`；空路径返回 `null`，模板回退到字母块。

- [ ] **Step 5: 构建 + 目视验证**

Run: `& 'D:\Code\dotnet\dotnet.exe' build src\GameBackup.Plugin -c Debug`
Expected: 页面显示真实 Playnite 游戏，搜索/筛选可用。

---

### Task 6: 详情面板（Playnite 风格）

**Files:**
- Modify: `src/GameBackup.Plugin/UI/GameBackupPage.xaml`
- Modify: `src/GameBackup.Plugin/UI/ViewModels/GameBackupViewModel.cs`

**Interfaces:**
- Consumes: `GameRowViewModel`、`SaveStatus`
- Produces: `LocateCommand`、`BackupCommand`、`RestoreCommand`（Phase 1 仅弹提示或切换占位状态）

- [ ] **Step 1: Hero 区**

封面图（右侧 140×186，低透明度）或纯色渐变背景；左侧：游戏名（`FontSizeLarge`/24）、来源 Pill、安装目录（省略中间）、状态 Pill。

- [ ] **Step 2: 信息卡片**

- “存档位置”：未定位时显示图标 + “尚未定位存档” + 副文案 + “查找存档”主按钮；已定位时显示路径、来源徽章、打开/重新定位/编辑规则。
- “备份概览”：最近备份、份数、总大小、保留策略；右侧“立即备份”主按钮与“恢复…”次按钮。
- “历史记录”：空状态插画式图标 + 文案；有数据时为时间线列表。

- [ ] **Step 3: 状态映射**

`Unknown → 灰点/“未定位”`；`Candidate → 黄点/“待确认”`；`Confirmed → 绿点/“已确认”`；`Stale → 橙点/“路径失效”`；`Error → 红点/“错误”`。颜色优先取主题强调色，黄/红使用本地兜底色。

- [ ] **Step 4: 构建验证**

Expected: 选中不同游戏详情正确切换，无布局跳动。

---

### Task 7: 设置页

**Files:**
- Create: `src/GameBackup.Plugin/Settings/SettingsViewModel.cs`
- Create: `src/GameBackup.Plugin/Settings/SettingsView.xaml(.cs)`
- Modify: `src/GameBackup.Plugin/GameBackupPlugin.cs`

**Interfaces:**
- Produces: `SettingsViewModel.Load(GameBackupSettings)`、`Save()`；插件 `GetSettingsView()` 返回 `SettingsView`。

- [ ] **Step 1: 设置模型与持久化**

使用 `GenericPlugin.LoadPluginSettings<GameBackupSettings>()` / `SavePluginSettings`。API Key 用 `ProtectedData.Protect(..., DataProtectionScope.CurrentUser)` 后 Base64 存储；保存/读取失败时返回空字符串并记录日志。

- [ ] **Step 2: 设置界面分区**

备份（目录选择、保留份数）、自动化（退出后自动备份）、智能定位（允许联网、端点、模型、API Key、“测试连接”占位）、关于（版本、备份目录打开）。

- [ ] **Step 3: 构建 + 保存/重启验证**

Expected: 修改设置 → 重启 Playnite → 值保留。

---

### Task 8: 打包与开发安装

**Files:**
- Modify: `build/build.ps1`
- Create: `build/pack.ps1`

- [ ] **Step 1: 开发安装脚本**

复制产物 + `extension.yaml` + `icon.png` 到 `%APPDATA%\Playnite\Extensions\GameBackup`，删除旧 DLL 避免残留。

- [ ] **Step 2: 打包脚本**

`& "$PlayniteDir\Toolbox.exe" pack <输出目录> <临时目录>` 生成 `.pext`；预期产物 `dist\GameBackup_0.1.0.pext`。

- [ ] **Step 3: 启动验证**

启动 `D:\Playnite\Playnite\Playnite.DesktopApp.exe`，检查 `extensions.log` 无 `GameBackup` 错误，侧边栏页面显示正常。

---

## Phase 2–5（后续独立计划，本次不实现）

- **Phase 2 定位链**：`ManifestStore/Index`、`UserRuleLocator`、`ManifestLocator`、`SteamLocator`、`CommonRootLocator`、`InstallDirProbe`、候选确认 UI、规则回写。
  - 2026-11-08 已落地：`ISaveLocator` + Steam/Uplay/GOG/Epic 商店 ID 定位、常见根目录与安装目录启发式、自动定位与待确认状态、确认回写规则、AI 仅作兜底。manifest 导入仍待做。
- **Phase 3 备份引擎**：Ludusavi 兼容布局、`mapping.yaml`、zip/simple、保留策略、恢复预览、互操作测试。
- **Phase 4 LLM**：OpenAI 兼容客户端、提示词、受限清单、响应校验、脱敏、规则生成。连接测试与定位闭环已接通。
- **Phase 5 自动化**：会话追踪定位、退出自动备份、通知/进度、性能与稳定性打磨。
