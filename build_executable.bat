@echo off
setlocal

REM Always run from the script directory so dist is created here.
cd /d "%~dp0"

echo [1/3] Working directory: %cd%
echo [2/3] Installing dependencies...
python -m pip install -r requirements.txt
if errorlevel 1 (
  echo.
  echo Build failed during dependency install.
  pause
  exit /b 1
)

echo [3/3] Building executable...
python -m PyInstaller --noconfirm --windowed --name LauncherExplorer app.py
if errorlevel 1 (
  echo.
  echo Build failed during PyInstaller step.
  echo Check the output above for the exact error.
  pause
  exit /b 1
)

echo.
echo Build complete.
echo Executable folder: %cd%\dist\LauncherExplorer
if exist "%cd%\dist\LauncherExplorer\LauncherExplorer.exe" (
  echo EXE created: %cd%\dist\LauncherExplorer\LauncherExplorer.exe
) else (
  echo WARNING: Build finished but EXE was not found at the expected path.
)

pause
