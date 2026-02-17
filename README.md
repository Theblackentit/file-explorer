# Launcher Explorer X (C# WinForms)

Launcher Explorer X is a native Windows desktop app (C#/.NET) that keeps the custom launcher mechanics from the Python version while running as a modern executable.

## Highlights

- Xbox-style **Home** and **Library** tabs
- Home supports grouped **posters** and **banners**
- Sidebar contains **Shortcuts** and mounted **Drives**
- In-app file explorer with **List / Tiles / Icons** layouts + scale slider
- Right-click file actions: open, rename, zip, delete, properties, custom icon mapping
- Theme Studio with editable colors and color picker buttons
- Auto-save to `launcher_config.json` on close

## Key UX fixes in this C# version

- Section editor now has dedicated **File** and **Folder** target picker buttons (no broken target selection flow)
- Section and theme dialogs are rebuilt with cleaner table-based layouts
- Main shell styling uses dark gradients and cleaner typography closer to the original Python visual direction

## Build executable (Windows)

1. Install **.NET 8 SDK**.
2. Run from repo root:

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
