# GameBackup

[English](README.md) | 中文

[![CI](https://github.com/LBurny/gamebuckup/actions/workflows/ci.yml/badge.svg)](https://github.com/LBurny/gamebuckup/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

GameBackup 是一个 **Playnite 10.x** 存档管理插件。它在 Playnite 侧边栏中完成存档的定位、备份、恢复与历史管理，并通过平台感知的识别链和可选的 AI 兜底，覆盖 manifest 数据库里没有的游戏。

## 功能

- **智能定位** — 用户规则、商店 ID（Steam、育碧 Connect、GOG、Epic）、常见存档目录与安装目录启发式。
- **AI 兜底** — 可接入 OpenAI 兼容端点；确认后的结果会写入本地规则，下次直接命中。
- **备份与恢复** — 可配置保留份数、备份历史与一键回滚。
- **自动备份** — 游戏退出时触发，可选择跳过云存档。
- **原生界面** — 集成在 Playnite 侧边栏，自动跟随当前主题。

## 环境要求

- Playnite 10.63
- .NET 8 SDK（编译与测试）

插件目标框架为 .NET Framework 4.6.2，运行在 x86 的 Playnite 进程中。

## 编译

```powershell
powershell -ExecutionPolicy Bypass -File build\build.ps1
dotnet test tests\GameBackup.Core.Tests
```

插件编译从 `D:\Playnite\Playnite` 读取 `Playnite.SDK.dll`，可用 `PlayniteDir` MSBuild 属性覆盖。

## 许可证

[MIT](LICENSE) © 2026 Lele Li
