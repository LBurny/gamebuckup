# GameBackup Navigation Polish Implementation Plan

> **For agentic workers:** Implement this plan in the current session. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 让搜索框与筛选区严格同宽，并把右上角刷新、设置改成清晰且符合 Playnite 主题的工具按钮。

**Architecture:** 只修改现有 WPF 样式和左栏尺寸。筛选 `ListBox` 改用五行一等分的 `UniformGrid`，由自定义模板提供与搜索框一致的外框；ViewModel、绑定和命令不变。

**Tech Stack:** WPF XAML、PowerShell 结构回归测试、Playnite 10.63 主题资源。

## Global Constraints

- 保持 `net462`，使用 `D:\Code\dotnet\dotnet.exe` 构建。
- 搜索、筛选、刷新和设置行为不变。
- 所有颜色与圆角继续使用 Playnite `DynamicResource`，不新增硬编码主题色。
- 必须重启 Playnite 并截图检查实际效果。

---

### Task 1: 对齐搜索与筛选，重绘工具按钮

**Files:**
- Modify: `tests/GameBackup.Plugin.UI.Tests/GameBackupPageLayout.Tests.ps1`
- Modify: `src/GameBackup.Plugin/UI/GameBackupPage.xaml:283-330`
- Modify: `src/GameBackup.Plugin/UI/Styles.xaml:159-184,289-334`

**Interfaces:**
- Consumes: 现有 `Filters`、`SelectedFilter`、`RefreshCommand`、`OpenSettingsCommand`。
- Produces: 同宽的 34px 搜索框和筛选条、五个等宽筛选项、34px 工具按钮；公共 ViewModel API 不变。

- [ ] **Step 1: 扩展现有回归测试，先描述目标结构**

在现有测试末尾、`Write-Host` 前加入：

```powershell
$filterListOnPage = $left.SelectSingleNode("./w:ListBox[@Style='{StaticResource GB.FilterList}']", $ns)
$filterListStyle = $styles.SelectSingleNode("/w:ResourceDictionary/w:Style[@x:Key='GB.FilterList']", $styleNs)
$uniformGrid = $filterListStyle.SelectSingleNode(".//w:UniformGrid", $styleNs)
$filterOuterBorder = $filterListStyle.SelectSingleNode(".//w:ControlTemplate/w:Border", $styleNs)
$iconStyle = $styles.SelectSingleNode("/w:ResourceDictionary/w:Style[@x:Key='GB.IconButton']", $styleNs)
Assert-True ($filterListOnPage.GetAttribute("Height") -eq "34") "Filter bar height must match the search box."
Assert-True ($null -ne $uniformGrid -and $uniformGrid.GetAttribute("Rows") -eq "1" -and $uniformGrid.GetAttribute("Columns") -eq "5") "Filters must use one row of five equal columns."
Assert-True ($null -ne $filterOuterBorder -and $filterOuterBorder.GetAttribute("CornerRadius") -eq "{DynamicResource ControlCornerRadius}") "Filter bar must have a themed outer border."
Assert-True ($iconStyle.GetPropertyValue("FontSize") -eq "16") "Icon size must remain readable."
```

`GetPropertyValue` 定义为读取指定 `w:Setter[@Property=...]` 的 `Value` 属性；若不存在则返回空字符串。

- [ ] **Step 2: 运行测试并确认因旧结构失败**

Run: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests\GameBackup.Plugin.UI.Tests\GameBackupPageLayout.Tests.ps1`

Expected: 非零退出，报告筛选条高度或 `UniformGrid` 结构不符合目标。

- [ ] **Step 3: 实现同宽筛选条**

- 页面筛选 `ListBox` 设置 `Height="34"`、`Margin="0,0,0,10"`；筛选标签与计数间距由 6 改为 4。
- `GB.FilterList` 使用与搜索框相同的 `ControlBackgroundBrush`、`PanelSeparatorBrush`、1px 边框和主题圆角；模板内 `Border` 设置 `Padding="2"` 并包含 `ItemsPresenter`。
- `ItemsPanel` 改为 `<UniformGrid Rows="1" Columns="5" />`，禁止滚动和换行。
- `GB.FilterItem` 内容水平、垂直居中并拉伸；去掉每项独立边框和外边距，仅保留主题圆角、悬停与选中背景。

- [ ] **Step 4: 实现明确的工具按钮**

- `GB.IconButton` 改为 34×34、16px 图标，按钮间距 6px。
- 使用 `ControlBackgroundBrush` 背景、`PanelSeparatorBrush` 1px 边框和 `ControlCornerRadius`。
- 模板绑定背景、边框；悬停使用 `ButtonIsMouseOverBackgroundBrush`，按下和禁用仅调整透明度。

- [ ] **Step 5: 验证结构测试通过**

Run: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests\GameBackup.Plugin.UI.Tests\GameBackupPageLayout.Tests.ps1`

Expected: 退出码 0，输出 `GameBackup page layout assertions passed.`

### Task 2: 构建并在 Playnite 中验收

**Files:**
- Modify if runtime inspection finds a defect: `src/GameBackup.Plugin/UI/GameBackupPage.xaml`
- Modify if runtime inspection finds a defect: `src/GameBackup.Plugin/UI/Styles.xaml`

**Interfaces:**
- Consumes: Task 1 的 XAML。
- Produces: 已安装并经实际交互验证的新界面。

- [ ] **Step 1: 构建并运行测试**

Run:
```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File build\build.ps1 -Configuration Debug
D:\Code\dotnet\dotnet.exe test tests\GameBackup.Core.Tests\GameBackup.Core.Tests.csproj -c Debug --no-build
```

Expected: 0 warnings、0 errors，18/18 测试通过。

- [ ] **Step 2: 等待 Playnite 完全退出后安装并重启**

Run:
```powershell
$process = Get-Process Playnite.DesktopApp -ErrorAction SilentlyContinue
$process | ForEach-Object { $_.CloseMainWindow() | Out-Null }
Start-Sleep -Seconds 3
Get-Process Playnite.DesktopApp -ErrorAction SilentlyContinue | Stop-Process -Force
powershell.exe -NoProfile -ExecutionPolicy Bypass -File build\dev-install.ps1 -Configuration Debug -NoBuild
Start-Process "D:\Playnite\Playnite\Playnite.DesktopApp.exe"
```

Expected: 安装成功，Playnite 重新加载插件。

- [ ] **Step 3: 实际截图和交互检查**

打开 GameBackup，确认搜索框与筛选条左右边缘及高度一致、五项等宽且不挤；刷新/设置按钮有清晰边界与悬停反馈。依次测试筛选、搜索、刷新和设置，若发现缺陷则返回对应 Task 修复并重新执行全部验证。
