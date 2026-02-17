# Launcher Explorer X (C# WinForms)

Launcher Explorer X is a native Windows desktop app (C#/.NET) that recreates the Python launcher style and behavior while shipping as an executable.

## What this build now mirrors from Python

- Same dark launcher shell structure:
  - top action bar (`+ Add Section`, `Map File Image`, `Theme Studio`, `Refresh`)
  - left sidebar (`Shortcuts`, `Drives`)
  - view toggle (`Home`, `Library`)
  - home toolbar with `Sort` and `Zoom`
- Home cards with poster/banner visuals and `Open`/`Edit`
- In-app file explorer with `List / Tiles / Icons`, zoom, preview, double-click open
- Right-click file actions: rename, zip, delete, custom icon mapping, properties
- Theme Studio with color pickers
- Auto-save to `launcher_config.json`

## Build executable (Windows)

1. Install **.NET 8 SDK**.
2. From repo root run:

```bat
build_executable.bat
```

Output executable:

```text
.\dist\LauncherExplorerX\LauncherExplorer.exe
```

## Run from source

```bat
dotnet run
```
