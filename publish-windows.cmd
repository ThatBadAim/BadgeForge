@echo off
rem
rem Builds the Windows executables:
rem  1. portable.exe (BadgeForge.exe) - run directly without installing
rem  2. installer.exe (BadgeForge-Setup.exe) - installs onto the machine with shortcuts
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
copy /y "%OUTPUT%\BadgeForge.exe" "%OUTPUT%\portable.exe" >nul

echo ==^> 2/2 Publishing setup installer...
set "INSTALLER_TEMP=%ROOT%BadgeForge.Installer\bin\installer-publish"
if exist "%INSTALLER_TEMP%" rmdir /s /q "%INSTALLER_TEMP%"
dotnet publish "%ROOT%BadgeForge.Installer\BadgeForge.Installer.csproj" --configuration Release --runtime "%RID%" --output "%INSTALLER_TEMP%" --nologo
if errorlevel 1 exit /b 1

move /y "%INSTALLER_TEMP%\BadgeForge.Installer.exe" "%OUTPUT%\BadgeForge-Setup.exe" >nul
copy /y "%OUTPUT%\BadgeForge-Setup.exe" "%OUTPUT%\installer.exe" >nul
if exist "%INSTALLER_TEMP%" rmdir /s /q "%INSTALLER_TEMP%"

echo.
echo =========================================================
echo  Portable Exe (Run Direct):  %OUTPUT%\portable.exe
echo                              (%OUTPUT%\BadgeForge.exe)
echo  Installer Exe (Install):    %OUTPUT%\installer.exe
echo                              (%OUTPUT%\BadgeForge-Setup.exe)
echo =========================================================
echo.

endlocal
