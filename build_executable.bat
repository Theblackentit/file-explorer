@echo off
setlocal
cd /d "%~dp0"

echo Restoring...
dotnet restore
if errorlevel 1 goto :fail

echo Publishing single-folder build...
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true -o dist\LauncherExplorerX
if errorlevel 1 goto :fail

if exist "dist\LauncherExplorerX\LauncherExplorer.exe" (
  echo.
  echo Build complete:
  echo %cd%\dist\LauncherExplorerX\LauncherExplorer.exe
  exit /b 0
)

echo ERROR: Executable not found in dist folder.
exit /b 1

:fail
echo Build failed.
exit /b 1
