# Launcher File Explorer

A desktop app that combines a file explorer with a game-launcher style home screen.

## Features

- Add launcher **sections/cards** with custom images.
- Card images can be shaped as **rectangle, rounded, circle, or hexagon**.
- Browse your full filesystem in a tree view and open files/folders.
- Map custom images to file extensions or specific file paths.
- Use built-in themes or create your own custom theme colors.
- Save all settings in `launcher_config.json`.

## Run locally

```bash
python app.py
```

## Build a clickable executable (Windows)

1. Double-click `build_executable.bat`, or run:

```bash
python -m pip install -r requirements.txt
pyinstaller --noconfirm --windowed --name LauncherExplorer app.py
```

2. Open `dist/LauncherExplorer/LauncherExplorer.exe`.

## Notes

- Requires Python 3.10+.
- On Linux, opening files uses `xdg-open`.
- On Windows, opening files uses `os.startfile`.
