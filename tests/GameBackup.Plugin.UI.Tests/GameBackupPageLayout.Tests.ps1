$ErrorActionPreference = "Stop"

$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
[xml]$page = Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path $root "src\GameBackup.Plugin\UI\GameBackupPage.xaml")
[xml]$styles = Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path $root "src\GameBackup.Plugin\UI\Styles.xaml")

$ns = New-Object System.Xml.XmlNamespaceManager($page.NameTable)
$ns.AddNamespace("w", "http://schemas.microsoft.com/winfx/2006/xaml/presentation")
$ns.AddNamespace("x", "http://schemas.microsoft.com/winfx/2006/xaml")
$styleNs = New-Object System.Xml.XmlNamespaceManager($styles.NameTable)
$styleNs.AddNamespace("w", "http://schemas.microsoft.com/winfx/2006/xaml/presentation")
$styleNs.AddNamespace("x", "http://schemas.microsoft.com/winfx/2006/xaml")

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) {
        throw $Message
    }
}

$header = $page.SelectSingleNode("/w:UserControl/w:Grid/w:Grid[@Grid.Row='0' and not(@Grid.RowSpan)]", $ns)
$body = $page.SelectSingleNode("/w:UserControl/w:Grid/w:Grid[@Grid.Row='1']", $ns)
$left = $body.SelectSingleNode("./w:DockPanel[@Grid.Column='0']", $ns)
$searchRow = $left.SelectSingleNode("./w:Grid[@DockPanel.Dock='Top']", $ns)
$search = $searchRow.SelectSingleNode("./w:TextBox[@Style='{StaticResource GB.SearchBox}']", $ns)
$searchTools = $searchRow.SelectSingleNode("./w:StackPanel[@Grid.Column='1' and @Orientation='Horizontal']", $ns)

Assert-True ($page.SelectNodes("//w:TextBlock[@Text='{Binding SummaryText}' or @Text='{Binding ScanStatus}']", $ns).Count -eq 0) "Header summary text is still present."
Assert-True ($header.SelectNodes(".//w:TextBox", $ns).Count -eq 0) "The search box is still in the header."
Assert-True ($header.SelectNodes(".//w:Button", $ns).Count -eq 0) "Header must keep the page title only; tools belong beside the search box."
Assert-True ($null -ne $searchRow) "The left column must start with a search row."
Assert-True ($null -ne $search -and $search.GetAttribute("Height") -eq "34") "The search box must keep the 34px control height."
Assert-True ([string]::IsNullOrEmpty($search.GetAttribute("Width"))) "The search box must stretch, not carry a fixed width."
Assert-True ($searchRow.SelectNodes("./w:Grid.ColumnDefinitions/w:ColumnDefinition[@Width='*']", $ns).Count -eq 1) "The search box must share its row so the tools dock to its right."
Assert-True ($null -ne $searchTools -and $searchTools.SelectNodes("./w:Button[@Command='{Binding RefreshCommand}' or @Command='{Binding OpenSettingsCommand}']", $ns).Count -eq 2) "Refresh and settings must sit after the search box."
Assert-True ($searchTools.GetAttribute("VerticalAlignment") -eq "Center") "Search row tools must centre against the search box."

$children = @($left.ChildNodes | Where-Object { $_.NodeType -eq [System.Xml.XmlNodeType]::Element })
Assert-True ($children.Count -eq 4 `
    -and $children[0].LocalName -eq "Grid" `
    -and $children[1].GetAttribute("Style") -eq "{StaticResource GB.FilterList}" `
    -and $children[2].GetAttribute("Style") -eq "{StaticResource GB.SourceFilterList}" `
    -and $children[3].LocalName -eq "ListBox") "Left column order must be search row, status filters, source filters, games."

$searchBorder = $styles.SelectSingleNode("/w:ResourceDictionary/w:Style[@x:Key='GB.SearchBox']//w:Border[@x:Name='Bd']", $styleNs)
$filterBorder = $styles.SelectSingleNode("/w:ResourceDictionary/w:Style[@x:Key='GB.FilterItem']//w:Border[@x:Name='Bd']", $styleNs)
Assert-True ($searchBorder.GetAttribute("CornerRadius") -eq "0") "Search box frame must be square."
Assert-True ($filterBorder.GetAttribute("CornerRadius") -eq "{DynamicResource ControlCornerRadius}") "Status filter selection must stay square."
$sourceItemBorder = $styles.SelectSingleNode("/w:ResourceDictionary/w:Style[@x:Key='GB.SourceFilterItem']//w:Border[@x:Name='Bd']", $styleNs)
Assert-True ($null -ne $sourceItemBorder -and $sourceItemBorder.GetAttribute("CornerRadius") -eq "12") "Source chips must be pills."
Assert-True ($sourceItemBorder.GetAttribute("Height") -eq "24") "Source pills need a fixed height so the radius stays elliptical."
Assert-True ($sourceItemBorder.GetAttribute("Background") -eq "{DynamicResource HoverBrush}") "Source pills need a visible surface so the ellipse reads at rest."

$filterListOnPage = $left.SelectSingleNode("./w:ListBox[@Style='{StaticResource GB.FilterList}']", $ns)
$filterListStyle = $styles.SelectSingleNode("/w:ResourceDictionary/w:Style[@x:Key='GB.FilterList']", $styleNs)
$uniformGrid = $filterListStyle.SelectSingleNode(".//w:UniformGrid", $styleNs)
$filterOuterBorder = $filterListStyle.SelectSingleNode(".//w:ControlTemplate/w:Border", $styleNs)
$iconStyle = $styles.SelectSingleNode("/w:ResourceDictionary/w:Style[@x:Key='GB.ToolButton']", $styleNs)
$iconFontSize = $iconStyle.SelectSingleNode("./w:Setter[@Property='FontSize']", $styleNs).GetAttribute("Value")
$iconBackground = $iconStyle.SelectSingleNode("./w:Setter[@Property='Background']", $styleNs).GetAttribute("Value")
$iconBorder = $iconStyle.SelectSingleNode("./w:Setter[@Property='BorderBrush']", $styleNs).GetAttribute("Value")
$iconBorderThickness = $iconStyle.SelectSingleNode("./w:Setter[@Property='BorderThickness']", $styleNs).GetAttribute("Value")
$iconHeight = $iconStyle.SelectSingleNode("./w:Setter[@Property='Height']", $styleNs).GetAttribute("Value")
$iconWidth = $iconStyle.SelectSingleNode("./w:Setter[@Property='Width']", $styleNs).GetAttribute("Value")
$iconFrame = $iconStyle.SelectSingleNode(".//w:ControlTemplate/w:Border[@x:Name='Bd']", $styleNs)
Assert-True ($filterListOnPage.GetAttribute("Height") -eq "34") "Filter bar height must match the search box."
Assert-True ($null -ne $uniformGrid -and $uniformGrid.GetAttribute("Rows") -eq "1" -and $uniformGrid.GetAttribute("Columns") -eq "5") "Filters must use one row of five equal columns."
Assert-True ($null -ne $filterOuterBorder -and $filterOuterBorder.GetAttribute("CornerRadius") -eq "{DynamicResource ControlCornerRadius}") "Status filter bar must stay square."
Assert-True ($iconFontSize -eq "17") "Search row icons must stay subordinate to the page title."
Assert-True ($iconBackground -eq "{DynamicResource ControlBackgroundBrush}" -and $iconBorder -eq "{DynamicResource PanelSeparatorBrush}" -and $iconBorderThickness -eq "1") "Search row tools must share the search box surface and border."
Assert-True ($iconHeight -eq "34" -and $iconWidth -eq "34") "Search row tools must be square and match the search box height."
Assert-True ($null -ne $iconFrame -and $iconFrame.GetAttribute("CornerRadius") -eq "0") "Search row tool frames must be square."
Assert-True ($null -eq $styles.SelectSingleNode("/w:ResourceDictionary/w:Style[@x:Key='GB.IconButton']", $styleNs)) "The old borderless icon style must be gone."

$headerTop = [int]($header.GetAttribute("Margin").Split(',')[1])
Assert-True ($headerTop -ge 32) "Header must clear the window caption buttons."

$titleGroup = $header.SelectSingleNode("./w:TextBlock[@Style='{StaticResource GB.PageTitle}']", $ns)
Assert-True ($null -ne $titleGroup) "Header must keep the page title."
Assert-True ($header.SelectNodes(".//w:StackPanel", $ns).Count -eq 0) "Header must not keep a tool group beside the title."
Assert-True ($null -eq $page.SelectSingleNode("//w:Border[@Style='{StaticResource GB.ToolGroup}']", $ns)) "Search row tools must not sit inside a background box."
Assert-True ($null -eq $styles.SelectSingleNode("/w:ResourceDictionary/w:Style[@x:Key='GB.ToolGroup']", $styleNs)) "Unused tool group style must be removed."

$sourceList = $left.SelectSingleNode("./w:ListBox[@Style='{StaticResource GB.SourceFilterList}']", $ns)
Assert-True ($null -ne $sourceList) "Game list needs a source filter."
Assert-True ($sourceList.GetAttribute("ItemsSource") -eq "{Binding Sources}" -and $sourceList.GetAttribute("SelectedItem") -eq "{Binding SelectedSource}") "Source filter must bind to the view model."
Assert-True ($sourceList.GetAttribute("PreviewMouseLeftButtonDown") -eq "OnSourceFilterClicked") "Source chips must toggle off when clicked again."
$sourcePanel = $styles.SelectSingleNode("/w:ResourceDictionary/w:Style[@x:Key='GB.SourceFilterList']//w:WrapPanel", $styleNs)
Assert-True ($null -ne $sourcePanel) "Source filter must wrap when the column narrows."
Assert-True ($null -ne $styles.SelectSingleNode("/w:ResourceDictionary/w:Style[@x:Key='GB.SourceFilterItem']", $styleNs)) "Source filter items need their own style."
$viewModelCode = Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path $root "src\GameBackup.Plugin\UI\ViewModels\GameBackupViewModel.cs")
Assert-True ($viewModelCode -match 'SelectedSource' -and $viewModelCode -match 'Sources') "View model must expose source filter state."
Assert-True ($viewModelCode -notmatch '\u5168\u90e8\u6765\u6e90') "Source filter must not duplicate the all status chip."
$pageCode = Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path $root "src\GameBackup.Plugin\UI\GameBackupPage.xaml.cs")
Assert-True ($pageCode -match 'OnSourceFilterClicked') "Source toggle handler must exist in code-behind."
Assert-True ($viewModelCode -match 'var keep = SelectedGame;' -and $viewModelCode -match 'Games\.Contains\(keep\)') "Filtering must restore the previously selected game instead of jumping to the first row."

$backupStart = $pageCode.IndexOf("private async Task StartBackupAsync")
$backupEnd = $pageCode.IndexOf("private void ", $backupStart)
Assert-True ($backupStart -ge 0 -and $backupEnd -gt $backupStart) "Backup routine must be a single bounded method."
$backupBody = $pageCode.Substring($backupStart, $backupEnd - $backupStart)
Assert-True ($backupBody -notmatch 'RefreshCounters') "A finished backup must update counts without rebuilding the list under the user."
Assert-True ($pageCode -match 'ApplyBackupPresence' -and $viewModelCode -match 'ApplyBackupPresence') "Backup state must be seeded for every row when the page loads."
Assert-True ($viewModelCode -match 'LastBackupUtc') "Rows must carry the backup timestamp so the list can sort by it."
Assert-True ($viewModelCode -match 'SelectedFilter\?\.Key == "backedup"[\s\S]{0,220}OrderByDescending\(x => x\.LastBackupUtc') "The backed-up view must put the newest backup first."
Assert-True ($viewModelCode -match 'public void RefreshBackupOrder') "Backup updates must refresh the backed-up ordering."
Assert-True ($pageCode -match 'RefreshBackupOrder') "The page must re-sort once a backup finishes."
Assert-True ($viewModelCode -match '_applyingFilter') "Filtering must guard against re-entrant rebuilds."
$summaryBody = [regex]::Match($pageCode, 'private void ApplySummary[\s\S]*?\r?\n        }').Value
Assert-True ($summaryBody -notmatch 'RefreshBackupOrder') "Refreshing the selected game summary must never re-sort the list."

$settingsRaw = Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path $root "src\GameBackup.Plugin\Settings\SettingsView.xaml")
Assert-True ($settingsRaw -notmatch '\u6240\u6709\u9009\u9879\u90fd\u4f1a\u7acb\u5373\u4fdd\u5b58\u5728\u672c\u673a') "Settings subtitle must be removed."
Assert-True ($settingsRaw -match 'AskOnGameExit' -and $settingsRaw -match 'NotifyAlways' -and $settingsRaw -match 'NotifyFailuresOnly') "Settings must expose the exit prompt and notification mode."

[xml]$settingsXml = $settingsRaw
$settingsNs = New-Object System.Xml.XmlNamespaceManager($settingsXml.NameTable)
$settingsNs.AddNamespace("w", "http://schemas.microsoft.com/winfx/2006/xaml/presentation")
$settingsNs.AddNamespace("x", "http://schemas.microsoft.com/winfx/2006/xaml")
$notifyRow = $settingsXml.SelectSingleNode("//w:StackPanel[w:RadioButton[contains(@IsChecked,'NotifyAlways')]]", $settingsNs)
Assert-True ($null -ne $notifyRow -and $notifyRow.GetAttribute("Margin") -eq "0,10,0,0") "Notification radios must breathe like the automation controls above."
$notifyHelp = $notifyRow.NextSibling
while ($null -ne $notifyHelp -and $notifyHelp.NodeType -ne [System.Xml.XmlNodeType]::Element) {
    $notifyHelp = $notifyHelp.NextSibling
}
Assert-True ($null -ne $notifyHelp `
    -and $notifyHelp.GetAttribute("Style") -eq "{StaticResource GB.Tiny}" `
    -and $notifyHelp.GetAttribute("Margin") -eq "24,6,0,0") "Notification section needs the same indented description as the automation rows."

$settingsModel = Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path $root "src\GameBackup.Plugin\Settings\GameBackupSettings.cs")
Assert-True ($settingsModel -match 'BackupNotificationMode' -and $settingsModel -match 'ExitBackupChoices') "Settings must persist notification mode and per-game exit choices."
$settingsVm = Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path $root "src\GameBackup.Plugin\Settings\SettingsViewModel.cs")
Assert-True ($settingsVm -notmatch 'new GameBackupSettings') "Saving settings must not discard unrelated persisted values."

Assert-True ($settingsModel -match 'SkipCloudSyncedBackups' -and $settingsModel -match 'SkipCloudPlatforms') "Settings must persist the cloud-save skip option and its platforms."
Assert-True ($null -ne $settingsXml.SelectSingleNode("//w:CheckBox[@IsChecked='{Binding SkipCloudSyncedBackups}']", $settingsNs)) "Settings need the cloud-save skip checkbox."
$platformBoxes = $settingsXml.SelectNodes("//w:CheckBox[@IsChecked='{Binding SkipEpic}' or @IsChecked='{Binding SkipGog}' or @IsChecked='{Binding SkipOrigin}' or @IsChecked='{Binding SkipSteam}' or @IsChecked='{Binding SkipUbisoft}']", $settingsNs)
Assert-True ($platformBoxes.Count -eq 5) "Every cloud platform must have its own checkbox."
Assert-True ($null -ne $settingsXml.SelectSingleNode("//w:StackPanel[@IsEnabled='{Binding SkipCloudSyncedBackups}']", $settingsNs)) "Platform checkboxes must follow the master checkbox."
Assert-True ($settingsVm -match 'SkipEpic' -and $settingsVm -match 'SkipUbisoft') "Settings view model must expose one property per platform."
Assert-True ($settingsVm -match 'settings\.SkipCloudPlatforms = [\w\.]*CloudPlatforms\.All') "Saving must write the selected platform keys."

$pluginCode = Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path $root "src\GameBackup.Plugin\GameBackupPlugin.cs")
Assert-True ($pluginCode -match 'GetGameMenuItems' -and $pluginCode -match 'GameMenuItem') "Game library needs the backup/restore context menu."
Assert-True ($pluginCode -match 'OnGameStopped' -and $pluginCode -match 'MessageBoxOption') "Game exit must offer the four-way backup prompt."
Assert-True ($pluginCode -match 'public bool IsCloudBackupSkipped' -and $pluginCode -match 'CloudPlatforms\.ShouldSkip') "The skip decision must live behind the host and delegate to the Core rule."
Assert-True ($pluginCode -match 'if \(IsCloudBackupSkipped\(game\.Source\?\.Name\)\)') "Backups must bail out for skipped cloud platforms."
Assert-True ($pageCode -match 'IsCloudBackupSkipped\(row\.Source\)') "Manual backups from the page must be skipped as well."

$exitStart = $pluginCode.IndexOf("public override void OnGameStopped")
$exitEnd = $pluginCode.IndexOf("private enum ExitChoice", $exitStart)
Assert-True ($exitStart -ge 0 -and $exitEnd -gt $exitStart) "Game exit handling must stay a single bounded method."
$exitBody = $pluginCode.Substring($exitStart, $exitEnd - $exitStart)
$skipIndex = $exitBody.IndexOf("IsCloudBackupSkipped")
Assert-True ($skipIndex -ge 0) "Game exit must honour the cloud-save skip option."
Assert-True ($skipIndex -lt $exitBody.IndexOf("ExitBackupChoices") -and $skipIndex -lt $exitBody.IndexOf("AskExitBackup")) "Skipped platforms must exit before any prompt or remembered choice."
$cloudCatalog = Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path $root "src\GameBackup.Core\Backup\CloudPlatforms.cs")
Assert-True ([regex]::Matches($cloudCatalog, 'new CloudPlatform\(').Count -eq 5) "The platform catalog must cover the five supported stores."
Assert-True ($cloudCatalog -match 'Matches\(') "The catalog must resolve Playnite source names."

$buttonStyles = @("GB.PrimaryButton", "GB.SecondaryButton")
foreach ($styleKey in $buttonStyles) {
    $styleNode = $styles.SelectSingleNode("/w:ResourceDictionary/w:Style[@x:Key='$styleKey']", $styleNs)
    $heightNode = $styleNode.SelectSingleNode("./w:Setter[@Property='Height']", $styleNs)
    Assert-True ($null -ne $heightNode -and $heightNode.GetAttribute("Value") -eq "32") "$styleKey must use the shared 32px control height."
}

$detail = $page.SelectSingleNode("//w:DataTemplate[@x:Key='DetailTemplate']", $ns)
$actionRow = $detail.SelectSingleNode("./w:Grid/w:StackPanel[@Grid.Row='1' and @Orientation='Horizontal']", $ns)
Assert-True ($null -ne $actionRow -and $actionRow.SelectNodes("./w:Button", $ns).Count -eq 2) "The detail action row must contain only the primary backup and restore actions."

$splitter = $body.SelectSingleNode("./w:GridSplitter[@Grid.Column='1']", $ns)
Assert-True ($null -ne $splitter) "Left and right panes must be separated by a draggable splitter."
Assert-True ($splitter.GetAttribute("ResizeDirection") -eq "Columns" -and $splitter.GetAttribute("ResizeBehavior") -eq "PreviousAndNext") "Splitter must resize the two neighbouring columns."
Assert-True ($splitter.GetAttribute("Cursor") -eq "SizeWE") "Splitter must advertise a horizontal resize cursor."
Assert-True ($null -eq $splitter.SelectSingleNode(".//w:Border", $ns)) "Splitter must not draw a permanent divider line."
Assert-True ($splitter.GetAttribute("DragCompleted") -eq "OnSplitterDragCompleted") "Splitter must persist its position after dragging."

$listColumn = $body.SelectSingleNode("./w:Grid.ColumnDefinitions/w:ColumnDefinition[@x:Name='ListPaneColumn']", $ns)
Assert-True ($null -ne $listColumn) "Game list column must be named so its width can be restored."
Assert-True ((Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path $root "src\GameBackup.Plugin\Settings\GameBackupSettings.cs")) -match 'ListPaneWidth') "Settings must store the list pane width."
Assert-True ((Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path $root "src\GameBackup.Plugin\UI\GameBackupPage.xaml.cs")) -match 'ListPaneWidth') "Page must apply and save the list pane width."

$bodyColumns = $body.SelectNodes("./w:Grid.ColumnDefinitions/w:ColumnDefinition", $ns)
Assert-True ([int]$bodyColumns[0].GetAttribute("MinWidth") -ge 240) "Game list column must keep a usable minimum width."
Assert-True ([int]$bodyColumns[2].GetAttribute("MinWidth") -ge 320) "Detail column must keep a usable minimum width."

$saveCard = $detail.SelectSingleNode(".//w:Border[@x:Name='SaveLocationCard']", $ns)
Assert-True ($null -ne $saveCard) "Save location card is missing."
$saveActions = $saveCard.SelectSingleNode(".//w:StackPanel[@Orientation='Horizontal' and .//w:Button[contains(@Command,'LocateCommand')]]", $ns)
Assert-True ($null -ne $saveActions) "Save location actions must sit in one horizontal row."
$openFolder = $saveActions.SelectSingleNode("./w:Button[contains(@Command,'OpenFolderCommand')]", $ns)
Assert-True ($null -ne $openFolder) "Open folder must share the save location action row."
$relocate = @($saveActions.SelectNodes("./w:Button[contains(@Command,'LocateCommand')]", $ns) | Where-Object { $_.GetAttribute("Visibility") -eq "{Binding HasLocation, Converter={StaticResource BoolToVisibility}}" })
Assert-True ($relocate.Count -eq 1) "Relocate must be the always-visible locate action."
Assert-True ($relocate[0].GetAttribute("Style") -eq $openFolder.GetAttribute("Style") -and $relocate[0].GetAttribute("Style") -eq "{StaticResource GB.SecondaryButton}") "Relocate must use the same framed button as open folder."
Assert-True ($null -eq $styles.SelectSingleNode("/w:ResourceDictionary/w:Style[@x:Key='GB.LinkButton']", $styleNs)) "The borderless link style must be gone."

$backupCard = $detail.SelectSingleNode(".//w:Border[@x:Name='BackupOverviewCard']", $ns)
Assert-True ($null -ne $backupCard) "Backup overview card is missing."
Assert-True ($backupCard.SelectNodes(".//w:Button", $ns).Count -eq 0) "Backup overview must not duplicate the backup action."

$historyCard = $detail.SelectSingleNode(".//w:Border[@x:Name='HistoryCard']", $ns)
Assert-True ($null -ne $historyCard) "History card is missing."
$historyItems = $historyCard.SelectSingleNode(".//w:ItemsControl[@ItemsSource='{Binding Versions}']", $ns)
Assert-True ($null -ne $historyItems) "History card must render the backup timeline."
Assert-True ($historyItems.GetAttribute("Visibility") -eq "{Binding HasVersions, Converter={StaticResource BoolToVisibility}}") "Timeline must hide when there are no backups."

$locateVm = Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path $root "src\GameBackup.Plugin\UI\ViewModels\LocateViewModel.cs")
Assert-True ($locateVm -match 'CancelCommand = new RelayCommand\(_ => CloseDialog\(\)\)') "The cancel button must close the locate dialog, not only abort the request."
Assert-True ($locateVm -match 'private void CloseDialog\(\)[\s\S]{0,200}_close\(\);') "Closing the locate dialog must invoke the host close callback."
$stopBody = [regex]::Match($locateVm, 'public void Stop\(\)[\s\S]{0,200}?\r?\n        }').Value
Assert-True ($stopBody -notmatch 'CloseDialog') "Host-driven Stop must not bounce back into the close handler."

Assert-True ($pluginCode -match 'RevealBackups') "Game menu needs the backup folder action."
Assert-True ($pluginCode -match 'Process\.Start\("explorer\.exe"') "Opening a folder must hand off to Explorer."
Assert-True ([regex]::Matches($pluginCode, 'new GameMenuItem').Count -eq 3) "Game menu must offer backup, restore and reveal-backups."

# The reveal action must open a dialog like 「重新查找」 instead of jumping to Explorer.
$revealBody = [regex]::Match($pluginCode, 'private void RevealBackups[\s\S]*?\r?\n        private void OpenFolder').Value
Assert-True ($revealBody -match 'CreateWindow\(new WindowCreationOptions') "Reveal must create a Playnite-themed window."
Assert-True ($revealBody -match 'ShowDialog\(\)' -and $revealBody -match 'BackupFilesViewModel') "Reveal must show the backup files dialog."
Assert-True ($revealBody -notmatch 'Process\.Start') "Reveal must not jump straight to Explorer."

[xml]$backupFiles = Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path $root "src\GameBackup.Plugin\UI\BackupFilesView.xaml")
$backupNs = New-Object System.Xml.XmlNamespaceManager($backupFiles.NameTable)
$backupNs.AddNamespace("w", "http://schemas.microsoft.com/winfx/2006/xaml/presentation")
$backupNs.AddNamespace("x", "http://schemas.microsoft.com/winfx/2006/xaml")
Assert-True ($null -ne $backupFiles.SelectSingleNode("//w:ItemsControl[@ItemsSource='{Binding Versions}']", $backupNs)) "Backup dialog must list every version."
Assert-True ($null -ne $backupFiles.SelectSingleNode("//w:Button[contains(@Command,'OpenVersionCommand')]", $backupNs)) "Each version needs its own open-folder action."
Assert-True ($null -ne $backupFiles.SelectSingleNode("//w:Button[contains(@Command,'CloseCommand')]", $backupNs)) "Backup dialog must expose a close action."
Assert-True ($null -ne $backupFiles.SelectSingleNode("//w:TextBlock[@Style='{StaticResource GB.PageTitle}']", $backupNs)) "Backup dialog must reuse the locate dialog header style."
$backupVm = Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path $root "src\GameBackup.Plugin\UI\ViewModels\BackupFilesViewModel.cs")
Assert-True ($backupVm -match 'Directory') "Backup versions must carry their folder path."
Assert-True ($backupVm -match 'ShowEmptyHint') "Backup dialog needs an empty state."

Write-Host "GameBackup page layout assertions passed."
