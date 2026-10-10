@echo off
setlocal enabledelayedexpansion

set "ANDROID_SDK=C:\Program Files (x86)\Android\android-sdk"
set "ADB=%ANDROID_SDK%\platform-tools\adb.exe"
set "DHU=%ANDROID_SDK%\extras\google\auto\desktop-head-unit.exe"

if not exist "%ADB%" (
    echo ERROR: adb not found at "%ADB%"
    pause
    exit /b 1
)
if not exist "%DHU%" (
    echo ERROR: Desktop Head Unit not found at "%DHU%"
    echo Install via sdkmanager.bat "extras;google;auto"
    pause
    exit /b 1
)

echo Checking for a connected device...
"%ADB%" start-server >nul 2>&1
set "FOUND="
for /f "skip=1 tokens=1,2" %%a in ('"%ADB%" devices') do (
    if "%%b"=="device" (
        set "FOUND=1"
        echo Device found: %%a
    )
)
if not defined FOUND (
    echo.
    echo ERROR: No device connected or unauthorized.
    echo Plug in the phone via USB, or start the emulator, then try again.
    echo.
    pause
    exit /b 1
)

echo Forwarding port 5277...
"%ADB%" forward tcp:5277 tcp:5277
if errorlevel 1 (
    echo ERROR: adb forward failed.
    pause
    exit /b 1
)

echo Starting Desktop Head Unit...
echo Make sure "Start head unit server" is enabled in the Android Auto app.
echo.
"%DHU%"

pause
