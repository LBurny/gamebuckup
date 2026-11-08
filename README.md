# GameBackup

[![CI](https://github.com/LBurny/gamebuckup/actions/workflows/ci.yml/badge.svg)](https://github.com/LBurny/gamebuckup/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

为 **Playnite 10.x** 打造的存档管理插件：在 Playnite 侧边栏里完成存档定位、备份、恢复与历史管理。目标是保留 Ludusavi 的能力，同时用更原生的界面和更聪明的定位链解决“manifest 里没有这个游戏就找不到存档”的问题。

## 当前进度

**Phase 1 完成 + 平台自动定位 + AI 兜底**

- Playnite 侧边栏整页、设置页、真实游戏库（86 款已安装游戏）、封面缩略图、搜索、状态筛选
- 详情页：存档位置卡片、备份概览、历史记录、主操作区
- **打开页面即自动定位**：本机 86 款已安装游戏自动识别出 52 款（Steam 云存档 9、常见存档目录 41、安装目录 1、本地规则 1），13 款进入待确认，21 款留给 AI
- **按商店 ID 直接推算存档目录**，不消耗 AI 调用：
  - Steam AppID → `userdata/<账号>/<AppID>/remote`（无云存档时只在用户数据目录里确有本地文件才作为候选）
  - 育碧 Connect 的 Uplay ID → `<launcher>/savegames/<账号>/<游戏 ID>`
  - GOG 产品 ID → `%LOCALAPPDATA%\GOG.com\Galaxy\Applications\<ID>\Storage`
  - Epic / Unreal → `%LOCALAPPDATA%\<App>\Saved\SaveGames`
- **常见存档根目录匹配**：Documents、`Documents\My Games`、Saved Games、AppData 的 Local/Roaming/LocalLow，按游戏名归一化（去空格、标点、版本后缀）后比对；命中游戏目录时会自动收窄到 `Saves` 之类的子目录
- **安装目录兜底**：`saves` / `savegame` / `userdata` 等子目录，以及 Unreal 的 `Saved\SaveGames`
- 置信度 ≥ 0.85 且路径存在的直接标为“已定位”；0.75–0.85 标为“待确认”，详情页一键“确认此位置”后写入规则
- **AI 只在本地链条全部低于阈值时调用**，返回结果与本地候选合并；未启用 AI 时仍会展示本地识别结果
- 定位弹窗显示候选来源（Steam / 育碧 Connect / GOG / Epic / 常见存档目录 / 安装目录 / AI）
- 设置持久化（备份目录、保留份数、自动备份、AI 端点/模型/Key），API Key 使用 Windows DPAPI 加密
- **真实的 AI 连接测试**：调用 OpenAI 兼容端点并显示模型与延迟
- **AI 查找存档闭环**：
  - 收集游戏名、安装目录与常见存档根目录的受限清单（只发文件名，不发文件内容；用户名替换为 `<home>`）
  - 按游戏名/安装目录 token 优先展开相关分支，避免被 AppData 里上百个目录淹没
  - 校验返回路径必须存在且位于允许范围内，AI 置信度上限 0.65
  - 候选列表人工确认后写入 `rules.json`，下次启动直接命中
- 已验证：
  - Vampire Survivors → `F:\Steam\userdata\478298662\1794680\remote`（Steam · 自动识别）
  - Hadean Tactics → `C:\Users\17575\AppData\LocalLow\EmberFishGames\Hadean Tactics`（本地规则 · AI 定位，重启 Playnite 后仍保留）
- 主题适配：优先使用 Playnite 的 `TextBrush` / `GlyphBrush` / `PanelSeparatorBrush` / `WindowBackgourndBrush` 等资源，自动跟随亮暗主题与自定义主题
- 单元测试 54 项通过（CI 在 Windows 上自动编译 Core 并运行测试），Release 可打包为 `.pext`

**待实现**：Ludusavi manifest 导入、备份与恢复引擎、Ludusavi 备份互操作、自动备份、会话追踪定位、微软商店/UWP 路径。

## 技术约束

- Playnite **10.63**，插件运行在 **.NET Framework 4.6.2 + WPF**，Playnite 进程为 **x86**
- `GameBackup.Core` 目标 `netstandard2.0`，不引用 Playnite/WPF，可被未来的 CLI 或独立宿主复用
- 不引入 x64 原生依赖

## 项目结构

```
src/GameBackup.Core/      纯逻辑：模型、定位器接口、备份/规则/LLM 能力（后续阶段填充）
src/GameBackup.Plugin/    Playnite 适配、WPF 界面、设置
tests/GameBackup.Core.Tests/
build/build.ps1           编译
build/dev-install.ps1     直接安装到 Playnite Extensions 目录，便于开发
build/pack.ps1            用 Playnite Toolbox 打包 .pext
docs/                     设计与截图
```

## 开发

本机 .NET SDK 位于 `D:\Code\dotnet`（PATH 上的 `C:\Program Files\dotnet` 没有 SDK）。

```powershell
# 编译
powershell -ExecutionPolicy Bypass -File build\build.ps1

# 编译 + 安装到 Playnite（重启 Playnite 生效）
powershell -ExecutionPolicy Bypass -File build\dev-install.ps1

# 测试
$env:DOTNET_ROOT = 'D:\Code\dotnet'
& 'D:\Code\dotnet\dotnet.exe' test tests\GameBackup.Core.Tests -c Debug

# 打包
powershell -ExecutionPolicy Bypass -File build\pack.ps1
```

安装位置：`%APPDATA%\Playnite\Extensions\GameBackup`
设置文件：`%APPDATA%\Playnite\ExtensionsData\8f4a2c1e-6b7d-4e3a-9c25-1d8b7a6f3e21\config.json`

## 设计要点

定位链按可信度从高到低执行，命中高置信结果即短路，只有全部低于阈值时才调用 AI：

```
用户规则 → 商店 ID（Steam AppID / Uplay ID / GOG 产品 ID / Epic AppName）
        → 常见存档根目录 → 安装目录启发式
        → 会话追踪（按游戏运行期间的文件修改反查，后续阶段）→ LLM 兜底
```

自动结果置信度 ≥ 0.85 直接采用；0.75–0.85 需要用户点一次“确认此位置”。AI 返回的候选路径置信度上限为 0.65，必须确认；确认后写入本地 JSON 规则（`rules.json`），下次直接命中。默认只发送游戏名、安装路径和受限目录列表，不发送文件内容。

定位器实现为 `ISaveLocator`，每个来源独立、可插拔；`SaveLocator.CreateDefault()` 决定默认顺序。

详细设计见 [Phase 1 实现计划](docs/superpowers/plans/2026-11-08-gamebackup-phase1.md)。

## 许可证

[MIT](LICENSE) © 2026 Lele Li
