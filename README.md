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

1. Double-click `build_executable.bat`.
2. The script forces the working folder to its own location and builds with PyInstaller.
3. Open:

```text
dist\LauncherExplorer\LauncherExplorer.exe
```

## If `dist` is not created

- Keep the command window open and read the exact error shown by the script.
- Confirm Python is installed:

```bash
python --version
```

- Confirm PyInstaller is installed:

```bash
python -m pip show pyinstaller
```

- Try building manually in the same folder:

```bash
python -m pip install -r requirements.txt
python -m PyInstaller --noconfirm --windowed --name LauncherExplorer app.py
```

- If it succeeds, check both `dist\` and `build\` in that same folder.

## Notes

- Requires Python 3.10+.
- On Linux, opening files uses `xdg-open`.
- On Windows, opening files uses `os.startfile`.
