@echo off
rem
rem Builds the portable Windows exe, from Windows.
rem
rem The result is one self-contained BadgeForge.exe: the .NET runtime, Avalonia and SkiaSharp's native libraries are
rem all packed inside it, so the printer PC needs no .NET install, no internet access and no installer. Copy the exe
rem across and run it.
rem
rem Usage:  publish-windows.cmd [runtime-identifier]    (default: win-x64; use win-arm64 for an ARM PC)
rem
setlocal

set "RID=%~1"
if "%RID%"=="" set "RID=win-x64"

set "ROOT=%~dp0"
set "OUTPUT=%ROOT%publish\%RID%"

if exist "%OUTPUT%" rmdir /s /q "%OUTPUT%"

dotnet publish "%ROOT%BadgeForge.App\BadgeForge.App.csproj" --configuration Release --runtime "%RID%" --output "%OUTPUT%" --nologo
if errorlevel 1 exit /b 1

rem A single-file exe carries its own bundle, so renaming it is safe and BadgeForge.exe is what an operator expects
move /y "%OUTPUT%\BadgeForge.App.exe" "%OUTPUT%\BadgeForge.exe" >nul

echo.
echo Portable exe: %OUTPUT%\BadgeForge.exe
echo Copy that one file to the Windows PC and double-click it. Nothing else is needed.
echo.
echo First run on a PC it was copied to: Windows SmartScreen may warn that the publisher is unknown, because the
echo exe is not code-signed. Choose "More info" then "Run anyway". If it came over a network share or download,
echo right-click it first, choose Properties, and tick Unblock.

endlocal
