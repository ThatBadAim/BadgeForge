#!/usr/bin/env bash
#
# Builds the Windows executables:
#  1. BadgeForge-Portable.exe (or BadgeForge.exe) - portable single-file, run directly without installing
#  2. BadgeForge-Setup.exe - self-extracting installer that creates Start Menu/Desktop shortcuts & Add/Remove Programs
#
# Usage:  ./publish-windows.sh [runtime-identifier]     (default: win-x64; use win-arm64 for an ARM PC)
#
set -euo pipefail

RID="${1:-win-x64}"
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
OUTPUT="$ROOT/publish/$RID"

case "$RID" in
    win-*) ;;
    *) echo "This script builds Windows executables; '$RID' is not a Windows runtime identifier." >&2; exit 1 ;;
esac

rm -rf "$OUTPUT"

echo "==> 1/2 Publishing portable standalone application..."
dotnet publish "$ROOT/BadgeForge.App/BadgeForge.App.csproj" \
    --configuration Release \
    --runtime "$RID" \
    --output "$OUTPUT" \
    --nologo

mv "$OUTPUT/BadgeForge.App.exe" "$OUTPUT/BadgeForge.exe"
cp "$OUTPUT/BadgeForge.exe" "$OUTPUT/BadgeForge-Portable.exe"

echo "==> 2/2 Publishing setup installer..."
INSTALLER_TEMP="$ROOT/BadgeForge.Installer/bin/installer-publish"
rm -rf "$INSTALLER_TEMP"
dotnet publish "$ROOT/BadgeForge.Installer/BadgeForge.Installer.csproj" \
    --configuration Release \
    --runtime "$RID" \
    --output "$INSTALLER_TEMP" \
    --nologo

mv "$INSTALLER_TEMP/BadgeForge.Installer.exe" "$OUTPUT/BadgeForge-Setup.exe"
rm -rf "$INSTALLER_TEMP"

echo
echo "========================================================="
echo " Portable Run-Direct Exe:  $OUTPUT/BadgeForge-Portable.exe  ($(du -h "$OUTPUT/BadgeForge-Portable.exe" | cut -f1))"
echo "                           ($OUTPUT/BadgeForge.exe)"
echo " Setup Installer Exe:      $OUTPUT/BadgeForge-Setup.exe     ($(du -h "$OUTPUT/BadgeForge-Setup.exe" | cut -f1))"
echo "========================================================="
echo "• Portable: Double-click to run immediately with no installation."
echo "• Setup:    Installs to AppData, creates Desktop/Start Menu shortcuts, and registers in Windows Settings."
echo
