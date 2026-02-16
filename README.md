# Launcher File Explorer

A modern desktop launcher + file explorer app built with Python/Tkinter.

## Highlights

- Modernized dark UI with smoother typography and refined controls (buttons, dropdowns, table headers, sliders).
- Added subtle gradients across header/panes for a more premium Microsoft/Hoyoverse-style visual finish.
- Full in-app file explorer behavior on the **left side**:
  - Layout toggle: `Tree + Files`, `Tree Only`, `Files Only`
  - View toggle: `Details`, `List`, `Icons`
  - Icons mode now uses a true grid (rows and columns) instead of a single vertical list
  - Size slider to scale rows/icons similar to File Explorer icon-size changes
  - In-app folder navigation and file preview (without forcing OS File Explorer)
- File previews:
  - image preview for image files
  - video thumbnail preview for common video formats (when OpenCV is available)
  - text preview for common code/text formats
- Double-click behavior:
  - folders open in-app
  - openable files launch with system default app
- Right-click context menu for files includes: Open, Preview, Rename, Compress to ZIP, Delete, Properties, and icon customization.
- Custom file icons/images in explorer view:
  - map by extension (ex: `.exe`) or exact file path
  - right-click any file to set/remove a custom icon image
- Right side section cards support 2 types:
  - `shortcut`: launches app/file externally
  - `collection`: opens selected folder inside this app's explorer panel
- Theme system supports:
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
- Optional for video thumbnails: `opencv-python`
