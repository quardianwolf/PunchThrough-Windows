@echo off
setlocal

echo ============================================
echo  PunchThrough Windows - Build
echo ============================================
echo.

:: Self-contained single-file exe. The zapret engine (winws.exe, WinDivert) is embedded
:: from PunchThrough\Assets, and the exe installs itself on first run - no installer needed.
dotnet publish PunchThrough\PunchThrough.csproj -c Release -r win-x64 -p:PublishSingleFile=true --self-contained -o publish\win-x64
if errorlevel 1 (
    echo ERROR: dotnet publish failed!
    exit /b 1
)

echo.
echo Build complete: publish\win-x64\PunchThrough.exe
