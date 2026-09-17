@echo off
rem
rem Builds the Windows executables:
rem  1. BadgeForge-Portable.exe (BadgeForge.exe) - portable single-file, run directly without installing
rem  2. BadgeForge-Setup.exe - self-extracting installer that creates Start Menu/Desktop shortcuts
rem
rem Usage:  publish-windows.cmd [runtime-identifier]    (default: win-x64; use win-arm64 for an ARM PC)
rem
setlocal

set "RID=%~1"
if "%RID%"=="" set "RID=win-x64"

set "ROOT=%~dp0"
set "OUTPUT=%ROOT%publish\%RID%"

if exist "%OUTPUT%" rmdir /s /q "%OUTPUT%"

echo ==^> 1/2 Publishing portable standalone application...
dotnet publish "%ROOT%BadgeForge.App\BadgeForge.App.csproj" --configuration Release --runtime "%RID%" --output "%OUTPUT%" --nologo
if errorlevel 1 exit /b 1

move /y "%OUTPUT%\BadgeForge.App.exe" "%OUTPUT%\BadgeForge.exe" >nul
copy /y "%OUTPUT%\BadgeForge.exe" "%OUTPUT%\BadgeForge-Portable.exe" >nul

echo ==^> 2/2 Publishing setup installer...
set "INSTALLER_TEMP=%ROOT%BadgeForge.Installer\bin\installer-publish"
if exist "%INSTALLER_TEMP%" rmdir /s /q "%INSTALLER_TEMP%"
dotnet publish "%ROOT%BadgeForge.Installer\BadgeForge.Installer.csproj" --configuration Release --runtime "%RID%" --output "%INSTALLER_TEMP%" --nologo
if errorlevel 1 exit /b 1

move /y "%INSTALLER_TEMP%\BadgeForge.Installer.exe" "%OUTPUT%\BadgeForge-Setup.exe" >nul
if exist "%INSTALLER_TEMP%" rmdir /s /q "%INSTALLER_TEMP%"

echo.
echo =========================================================
echo  Portable Run-Direct Exe:  %OUTPUT%\BadgeForge-Portable.exe
echo                            (%OUTPUT%\BadgeForge.exe)
echo  Setup Installer Exe:      %OUTPUT%\BadgeForge-Setup.exe
echo =========================================================
echo.

endlocal
