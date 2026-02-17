# Launcher File Explorer

A modern Xbox-inspired launcher + file explorer desktop app (Python/Tkinter).

## Revamped experience

- **Home + Library flow**:
  - `Home` = Xbox-style content area with rows/sections
  - `Library` = in-app file explorer
- **Home content model**:
  - cards can be `poster` (square-ish) or `banner` (wide)
  - each card can be:
    - `shortcut` (open app/file/video)
    - `collection` (open a folder inside app explorer)
  - cards belong to named rows/sections (ex: `Recently Added`, `Game Deals`)
- **Sidebar**:
  - shortcut list (from items placed as `shortcut`)
  - drive list (`C:\`, `D:\`, etc.) that opens library explorer
- **Explorer layouts (macOS/Finder-like options)**:
  - `List`, `Tiles`, `Icons`
  - `Icons` is a true responsive 2D grid (rows + columns)
- **File operations**:
  - right-click for Open, Preview, Rename, Compress ZIP, Delete, Properties
  - set/remove custom icon image per file via right-click
  - double-click opens files with system default app
- **Preview support**:
  - images, text/code files, and video thumbnails (with OpenCV)
- **Themes**:
  - normal themes (colors/background)
  - preset themes (theme + preloaded rows/cards/icon mappings)
  - gradient rendering for premium visual polish

## Run locally

```bash
python app.py
```

## Build executable (Windows)

1. Double-click `build_executable.bat`
2. Open `dist\LauncherExplorer\LauncherExplorer.exe`

## Requirements

- Python 3.10+
- `Pillow`
- `pyinstaller`
- `opencv-python` (for video thumbnails)
