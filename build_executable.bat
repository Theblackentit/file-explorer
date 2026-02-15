@echo off
python -m pip install -r requirements.txt
pyinstaller --noconfirm --windowed --name LauncherExplorer app.py
echo Build complete. See dist\\LauncherExplorer\\LauncherExplorer.exe
pause
