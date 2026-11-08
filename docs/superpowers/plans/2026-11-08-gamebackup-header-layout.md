# GameBackup Header Layout Implementation Plan

> **For agentic workers:** Implement this plan in the current session. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 解开 GameBackup 页面顶部的拥挤布局，将搜索归入左侧游戏导航，并让控件外观继续遵循 Playnite 主题。

**Architecture:** 保留现有 ViewModel、命令与自动识别流程，仅调整 WPF XAML。页面外层维持“标题栏 + 主内容”两行；左栏内部改为“搜索 + 筛选 + 游戏列表”，右栏详情不变。新增轻量 PowerShell 结构回归测试，防止两行统计文本和顶部搜索框回流。

**Tech Stack:** WPF XAML、.NET Framework 4.6.2、PowerShell、Playnite 10.63 主题资源。

## Global Constraints

- 插件目标框架保持 `net462`，构建使用 `D:\Code\dotnet\dotnet.exe`。
- 不修改搜索、筛选、刷新、设置及自动识别的现有行为。
- 移除 `{Binding SummaryText}` 与 `{Binding ScanStatus}` 两处页面文本；筛选器数量继续显示。
- 颜色、字体和圆角使用 Playnite `DynamicResource`；不得为顶部新增硬编码主题色。
- 完成后必须在重启后的 Playnite 中实际截图检查，不能只以编译通过作为视觉验收。

---

### Task 1: 重组标题栏和左侧导航

**Files:**
- Create: `tests/GameBackup.Plugin.UI.Tests/GameBackupPageLayout.Tests.ps1`
- Modify: `src/GameBackup.Plugin/UI/GameBackupPage.xaml:267-355`
- Modify: `src/GameBackup.Plugin/UI/Styles.xaml:159-238,299-329`

**Interfaces:**
- Consumes: 现有 `SearchText`、`RefreshCommand`、`OpenSettingsCommand`、`Filters`、`Games` 绑定。
- Produces: 不变的 ViewModel 公共接口；顶部只有标题和两个图标按钮，搜索框成为左栏第一个直接子元素。

- [ ] **Step 1: 编写失败的 XAML 结构回归测试**

创建 PowerShell 测试，使用命名空间感知的 XPath 验证：

```powershell
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
[xml]$page = Get-Content -Raw -LiteralPath (Join-Path $root "src\GameBackup.Plugin\UI\GameBackupPage.xaml")
[xml]$styles = Get-Content -Raw -LiteralPath (Join-Path $root "src\GameBackup.Plugin\UI\Styles.xaml")
$ns = New-Object System.Xml.XmlNamespaceManager($page.NameTable)
$ns.AddNamespace("w", "http://schemas.microsoft.com/winfx/2006/xaml/presentation")
$ns.AddNamespace("x", "http://schemas.microsoft.com/winfx/2006/xaml")
$styleNs = New-Object System.Xml.XmlNamespaceManager($styles.NameTable)
$styleNs.AddNamespace("w", "http://schemas.microsoft.com/winfx/2006/xaml/presentation")
$styleNs.AddNamespace("x", "http://schemas.microsoft.com/winfx/2006/xaml")
function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

$header = $page.SelectSingleNode("/w:UserControl/w:Grid/w:Grid[@Grid.Row='0' and not(@Grid.RowSpan)]", $ns)
$body = $page.SelectSingleNode("/w:UserControl/w:Grid/w:Grid[@Grid.Row='1']", $ns)
$left = $body.SelectSingleNode("./w:DockPanel[@Grid.Column='0']", $ns)
$search = $left.SelectSingleNode("./w:TextBox[@Style='{StaticResource GB.SearchBox}']", $ns)
Assert-True ($page.SelectNodes("//w:TextBlock[@Text='{Binding SummaryText}' or @Text='{Binding ScanStatus}']", $ns).Count -eq 0) "顶部统计说明仍存在"
Assert-True ($header.SelectNodes(".//w:TextBox", $ns).Count -eq 0) "搜索框仍位于标题栏"
Assert-True ($header.SelectNodes(".//w:Button[@Command='{Binding RefreshCommand}' or @Command='{Binding OpenSettingsCommand}']", $ns).Count -eq 2) "刷新或设置按钮丢失"
Assert-True ($null -ne $search -and $search.GetAttribute("DockPanel.Dock") -eq "Top") "搜索框未置于左栏顶部"
$children = @($left.ChildNodes | Where-Object { $_.NodeType -eq [System.Xml.XmlNodeType]::Element })
Assert-True ($children.Count -eq 3 -and $children[0].LocalName -eq "TextBox" -and $children[1].LocalName -eq "ListBox" -and $children[2].LocalName -eq "ListBox") "左栏顺序必须为搜索、筛选、游戏列表"
$searchBorder = $styles.SelectSingleNode("/w:ResourceDictionary/w:Style[@x:Key='GB.SearchBox']//w:Border[@x:Name='Bd']", $styleNs)
$filterBorder = $styles.SelectSingleNode("/w:ResourceDictionary/w:Style[@x:Key='GB.FilterItem']//w:Border[@x:Name='Bd']", $styleNs)
Assert-True ($searchBorder.GetAttribute("CornerRadius") -eq "{DynamicResource ControlCornerRadius}") "搜索框圆角未使用主题资源"
Assert-True ($filterBorder.GetAttribute("CornerRadius") -eq "{DynamicResource ControlCornerRadius}") "筛选器圆角未使用主题资源"
Write-Host "GameBackup page layout assertions passed."
```

- [ ] **Step 2: 运行测试并确认当前实现失败**

Run: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests\GameBackup.Plugin.UI.Tests\GameBackupPageLayout.Tests.ps1`

Expected: 非零退出，首先报告“顶部统计说明仍存在”。

- [ ] **Step 3: 修改页面布局**

在 `GameBackupPage.xaml` 中：
- 标题栏改为 `Margin="24,20,24,16"`；左侧只保留 `GB.PageTitle`，右侧为水平排列的刷新、设置按钮。
- 删除 `SummaryText` 和 `ScanStatus` 两个 `TextBlock`，不删除 ViewModel 中的扫描逻辑。
- 主内容保留三列 `380 / 20 / *`；左栏 `DockPanel` 的直接子元素依次为搜索框、筛选 `ListBox`、游戏 `ListBox`。
- 搜索框设置 `DockPanel.Dock="Top"`、`Height="34"`、`Margin="0,0,0,10"`，不再设置固定 `Width`；其绑定和 `GB.SearchBox` 样式保持不变。
- 筛选器设置 `Margin="0,0,0,8"`，游戏列表继续填充剩余空间。

- [ ] **Step 4: 统一 Playnite 控件外观**

在 `Styles.xaml` 中：
- `GB.IconButton` 使用 32×32 点击区域、14 号 Segoe MDL2 图标和主题 `ControlCornerRadius`；两个按钮通过布局留出 4px 间隔。
- `GB.SearchBox` 的边框圆角由硬编码 `16` 改为 `{DynamicResource ControlCornerRadius}`；保留搜索图标、占位文本和现有主题画刷，并增加键盘焦点时的主题色边框反馈。
- `GB.FilterItem` 的圆角由硬编码 `12` 改为 `{DynamicResource ControlCornerRadius}`，保持现有数量、选中态和换行行为。
- 不新增颜色画刷，不修改右侧详情卡片。

- [ ] **Step 5: 运行结构测试并确认通过**

Run: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests\GameBackup.Plugin.UI.Tests\GameBackupPageLayout.Tests.ps1`

Expected: 退出码 0，输出 `GameBackup page layout assertions passed.`

### Task 2: 编译并进行 Playnite 实际视觉验收

**Files:**
- Create: `docs/ui-header-redesign.png`
- Modify if visual inspection exposes a defect: `src/GameBackup.Plugin/UI/GameBackupPage.xaml`
- Modify if visual inspection exposes a defect: `src/GameBackup.Plugin/UI/Styles.xaml`

**Interfaces:**
- Consumes: Task 1 的页面与样式。
- Produces: 可安装的 Debug 插件及实际运行截图。

- [ ] **Step 1: 编译并运行现有测试**

Run:
```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File build\build.ps1 -Configuration Debug
D:\Code\dotnet\dotnet.exe test tests\GameBackup.Core.Tests\GameBackup.Core.Tests.csproj -c Debug --no-build
```

Expected: `Build succeeded`、0 errors；现有测试全部通过。

- [ ] **Step 2: 完全退出 Playnite 并安装新插件**

Run:
```powershell
$process = Get-Process Playnite.DesktopApp -ErrorAction SilentlyContinue
$process | ForEach-Object { $_.CloseMainWindow() | Out-Null }
Start-Sleep -Seconds 3
$process | Where-Object { -not $_.HasExited } | Stop-Process -Force
powershell.exe -NoProfile -ExecutionPolicy Bypass -File build\dev-install.ps1 -Configuration Debug -NoBuild
```

Expected: 插件复制到 `%APPDATA%\Playnite\Extensions\GameBackup`。

- [ ] **Step 3: 启动 Playnite 并截图检查**

Run: `Start-Process "D:\Playnite\Playnite\Playnite.DesktopApp.exe"`

打开 GameBackup 页面并保存截图为 `docs/ui-header-redesign.png`。检查：标题与按钮不拥挤；两行说明消失；搜索框在左栏且横向填满；筛选数量仍在；搜索、筛选、刷新和设置均可用；当前主题下边框、文字和选中态清晰。若发现视觉缺陷，修改 XAML 后重复 Task 1 Step 5 和 Task 2 全部验证。
