# Launcher File Explorer

A modern desktop launcher + file explorer app built with Python/Tkinter.

## Highlights

- Crunchyroll / ZZZ-inspired dark neon style.
- Full in-app file explorer behavior on the **left side**:
  - Layout toggle: `Tree + Files`, `Tree Only`, `Files Only`
  - View toggle: `Details`, `List`, `Icons`
  - Size slider to scale rows/icons similar to File Explorer icon-size changes
  - In-app folder navigation and file preview (image/text/binary info) without launching OS File Explorer
- Custom file icons/images in explorer view:
  - map by extension (ex: `.exe`) or by exact file path
- Right side section cards support 2 types:
  - `shortcut`: launches app/file externally
  - `collection`: opens the selected folder inside this app's explorer panel
- Section image edit controls:
  - `contain`, `cover`, `original`, `stretch`
  - independent `Scale X` / `Scale Y`
  - live preview
- Theme Studio supports:
  - normal themes (colors + background image)
  - **preset themes** (theme + preloaded sections + file-image mappings)

## Run locally

```bash
python app.py
```

## Build a clickable executable (Windows)

1. Double-click `build_executable.bat`
2. Open `dist\LauncherExplorer\LauncherExplorer.exe`

## Requirements

- Python 3.10+
- Dependencies in `requirements.txt`
