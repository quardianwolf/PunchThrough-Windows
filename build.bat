@echo off
setlocal

echo ============================================
echo  PunchThrough Windows - Build + Package
echo ============================================
echo.

:: 1. Publish self-contained single-file exe
echo [1/3] Publishing self-contained exe...
dotnet publish PunchThrough\PunchThrough.csproj -c Release -r win-x64 -p:PublishSingleFile=true --self-contained -o publish\win-x64
if errorlevel 1 (
    echo ERROR: dotnet publish failed!
    exit /b 1
)
echo       Done.
echo.

:: 2. Download SpoofDPI if not already present
if not exist "installer\bin\spoofdpi.exe" (
    echo [2/3] Downloading SpoofDPI v0.12.2...
    mkdir installer\bin 2>nul

    powershell -Command ^
        "$url = 'https://github.com/xvzc/SpoofDPI/releases/download/v0.12.2/spoofdpi-windows-amd64.exe'; " ^
        "Write-Host \"Downloading $url\"; " ^
        "Invoke-WebRequest $url -OutFile installer\bin\spoofdpi.exe; " ^
        "Write-Host 'SpoofDPI v0.12.2 downloaded.'"
    if errorlevel 1 (
        echo ERROR: SpoofDPI download failed!
        exit /b 1
    )
) else (
    echo [2/3] SpoofDPI already present, skipping download.
)
echo.

:: 3. Build installer (if Inno Setup is installed)
where iscc >nul 2>&1
if %errorlevel%==0 (
    echo [3/3] Building installer with Inno Setup...
    mkdir dist 2>nul
    iscc installer\setup.iss
    if errorlevel 1 (
        echo ERROR: Inno Setup compilation failed!
        exit /b 1
    )
    echo.
    echo ============================================
    echo  Installer ready: dist\PunchThrough-Setup-1.1.0.exe
    echo ============================================
) else (
    echo [3/3] Inno Setup not found, skipping installer.
    echo       To build installer, install Inno Setup and add iscc to PATH.
    echo       Portable exe is ready at: publish\win-x64\PunchThrough.exe
    echo ============================================
)

echo.
echo Build complete!
