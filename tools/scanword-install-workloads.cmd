@echo off
setlocal

net session >nul 2>&1
if %errorlevel% neq 0 (
    echo Requesting administrator privileges...
    powershell -NoProfile -Command "Start-Process cmd.exe -ArgumentList '/c','\"%~f0\"' -Verb RunAs"
    exit /b
)

set LOGFILE=%TEMP%\scanword-workload-install.log
set ANDROID_SDK=C:\Program Files (x86)\Android\android-sdk

echo START %DATE% %TIME% > "%LOGFILE%"

echo [1/2] dotnet workload install android wasm-tools
dotnet workload install android wasm-tools >> "%LOGFILE%" 2>&1
echo WORKLOAD_EXITCODE=%ERRORLEVEL% >> "%LOGFILE%"

echo [2/2] InstallAndroidDependencies ^(Android SDK API level^)
pushd "%~dp0..\Scanword.Android"
dotnet build -t:InstallAndroidDependencies -f net10.0-android -p:AndroidSdkDirectory="%ANDROID_SDK%" -p:AcceptAndroidSDKLicenses=True >> "%LOGFILE%" 2>&1
echo DEPS_EXITCODE=%ERRORLEVEL% >> "%LOGFILE%"
popd

echo DONE %DATE% %TIME% >> "%LOGFILE%"

echo.
echo ===== Result =====
findstr /C:"EXITCODE" "%LOGFILE%"
echo Full log: %LOGFILE%
pause
