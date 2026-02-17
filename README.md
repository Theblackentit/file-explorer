# Launcher Explorer X (C# WinForms)

This project has been switched from Python to a native **C#/.NET Windows desktop app** so it can be packaged as a clickable `.exe` with a more modern UI baseline.

## What this version includes

- **Xbox-style Home + Library split**
  - `Home`: grouped content rows with **posters** and **banners**
  - `Library`: in-app file explorer (does not open external explorer for navigation)
- **Sidebar shortcuts + drives**
  - Shortcuts come from your configured poster/banner items
  - Double-clicking a drive opens that path in Library view
- **Explorer view modes**
  - List, Tiles, and Icons layouts
  - Scale slider to increase/decrease icon size
- **File operations in right-click menu**
  - Open, Rename, Compress to `.zip`, Delete, Properties
  - Change custom icon image per file/folder
- **Theme Studio**
  - Edit theme name and color values
- **Auto-save**
  - Saves sections, shortcuts, theme, and file icon mappings to `launcher_config.json` on close

## Build executable (Windows)

1. Install **.NET 8 SDK** from Microsoft.
2. Open terminal in this repo.
3. Run:

```bat
build_executable.bat
```

Output executable:

```text
.\dist\LauncherExplorerX\LauncherExplorer.exe
```

## Run from source (Windows)

```bat
dotnet run
```

## Notes

- This is now a Windows-native .NET desktop app (no Python runtime required).
- Configuration is stored beside the executable in `launcher_config.json`.
