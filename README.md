# Launcher File Explorer

A modern desktop launcher + file explorer app built with Python/Tkinter.

## What's improved

- Crunchyroll / ZZZ-inspired dark neon style with cleaner cards and stronger accents.
- Add/Edit sections using file/folder pickers (no manual path typing required).
- Right-click card menu for quick Edit / Change Image / Delete.
- Theme Studio with:
  - color wheel pickers (`🎨`) + hex,
  - custom background image,
  - stickers and small GIF support.
- Section image controls in Edit mode:
  - **Image mode**: `contain`, `cover`, `original`, `stretch`
  - **Scale X / Scale Y** sliders to stretch or shrink manually
  - Live preview before saving
- Explorer-style section scaling and sorting:
  - global **Zoom** slider to scale card/file tiles up or down
  - sorting by **Alphabetical**, **Size**, or **Date**
- Full filesystem tree browser with double-click open.

## Run locally

```bash
python app.py
```

## Build a clickable executable (Windows)

1. Double-click `build_executable.bat`.
2. Open:

```text
dist\LauncherExplorer\LauncherExplorer.exe
```

## Troubleshooting missing `dist`

Run in the project folder:

```bash
python --version
python -m pip show pyinstaller
python -m pip install -r requirements.txt
python -m PyInstaller --noconfirm --windowed --name LauncherExplorer app.py
```

Then search:

```bash
dir /s /b LauncherExplorer.exe
```

## Requirements

- Python 3.10+
- Dependencies in `requirements.txt`
