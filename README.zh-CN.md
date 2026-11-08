# GameBackup

[English](README.md) | 中文

[![CI](https://github.com/LBurny/gamebuckup/actions/workflows/ci.yml/badge.svg)](https://github.com/LBurny/gamebuckup/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

GameBackup 是一个 **Playnite 10.x** 存档管理插件。你可以在 Playnite 侧边栏里找到存档、备份、恢复和查看历史。它能识别不同平台的存档位置，实在找不到时还可以交给 AI 兜底。

## 功能

- **自动找存档**：先查你自己的规则，再按 Steam、育碧 Connect、GOG、Epic 的商店 ID、常见存档目录和游戏安装目录依次查找。
- **AI 兜底**：可以接入 OpenAI 兼容接口。确认过的结果会保存为本地规则，下次直接使用。
- **备份与恢复**：可以设置保留多少份备份，查看历史记录，一键回滚。
- **自动备份**：游戏退出时自动备份。已经同步到云端的游戏可以选择跳过。
- **原生界面**：就在 Playnite 侧边栏里，自动跟随当前主题。

## 环境要求

- Playnite 10.63
- .NET 8 SDK（用于编译和测试）

插件基于 .NET Framework 4.6.2，运行在 32 位的 Playnite 进程中。

## 编译

```powershell
powershell -ExecutionPolicy Bypass -File build\build.ps1
dotnet test tests\GameBackup.Core.Tests
```

插件默认从 `D:\Playnite\Playnite` 读取 `Playnite.SDK.dll`。如果 Playnite 装在其他位置，设置 `PlayniteDir` 属性即可。

## 许可证

[MIT](LICENSE) © 2026 Lele Li
