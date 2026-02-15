# Launcher File Explorer

A modern desktop launcher + file explorer app built with Python/Tkinter.

## What's improved

- Modernized launcher-style UI with cleaner cards and better spacing.
- Add/Edit sections using file/folder picker dialogs (no manual path typing needed).
- Right-click section menu for quick Edit / Change Image / Delete.
- Theme Studio with:
  - color pickers + color wheel (`🎨`) for all theme colors,
  - custom background image,
  - stickers and small GIF support.
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
