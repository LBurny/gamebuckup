# GameBackup

English | [中文](README.zh-CN.md)

[![CI](https://github.com/LBurny/gamebuckup/actions/workflows/ci.yml/badge.svg)](https://github.com/LBurny/gamebuckup/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

GameBackup is a save-game manager plugin for **Playnite 10.x**. It finds, backs up, restores, and tracks save files right from the Playnite sidebar — with platform-aware discovery and an optional AI fallback for games that manifest databases miss.

## Features

- **Smart discovery** — user rules, store IDs (Steam, Ubisoft Connect, GOG, Epic), common save folders, and install-directory heuristics.
- **AI fallback** — optional OpenAI-compatible endpoint; confirmed results are remembered as local rules.
- **Backup & restore** — configurable retention, backup history, and one-click rollback.
- **Automatic backup** — runs when a game exits, with optional skipping for cloud-synced saves.
- **Native UI** — lives in the Playnite sidebar and follows the active theme.

## Requirements

- Playnite 10.63
- .NET 8 SDK (build and tests)

The plugin targets .NET Framework 4.6.2 and runs inside the x86 Playnite process.

## Build

```powershell
powershell -ExecutionPolicy Bypass -File build\build.ps1
dotnet test tests\GameBackup.Core.Tests
```

The plugin build reads `Playnite.SDK.dll` from `D:\Playnite\Playnite`; override with the `PlayniteDir` MSBuild property.

## License

[MIT](LICENSE) © 2026 Lele Li
