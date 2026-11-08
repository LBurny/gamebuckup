# GameBackup

English | [中文](README.zh-CN.md)

[![CI](https://github.com/LBurny/gamebuckup/actions/workflows/ci.yml/badge.svg)](https://github.com/LBurny/gamebuckup/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

GameBackup is a save-game manager plugin for **Playnite 10.x**. It finds, backs up, restores, and tracks save files from the Playnite sidebar. It knows where different stores keep their saves, and it can use AI as a fallback when it cannot find a game on its own.

## Features

- **Finds saves automatically.** It checks your own rules, store IDs from Steam, Ubisoft Connect, GOG and Epic, common save folders, and the game install folder.
- **AI fallback.** You can connect an OpenAI-compatible endpoint. When you confirm a result, GameBackup saves it as a local rule and uses it next time.
- **Backup and restore.** Choose how many backups to keep, browse the history, and roll back with one click.
- **Automatic backup.** Back up when a game exits. You can skip games that already sync saves to the cloud.
- **Native UI.** It lives in the Playnite sidebar and follows the current theme.

## Requirements

- Playnite 10.63
- .NET 8 SDK for building and testing

The plugin targets .NET Framework 4.6.2 and runs inside the 32-bit Playnite process.

## Build

```powershell
powershell -ExecutionPolicy Bypass -File build\build.ps1
dotnet test tests\GameBackup.Core.Tests
```

The plugin looks for `Playnite.SDK.dll` in `D:\Playnite\Playnite`. Set the `PlayniteDir` MSBuild property if your Playnite is installed elsewhere.

## License

[MIT](LICENSE) © 2026 Lele Li
