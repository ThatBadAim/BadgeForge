#!/usr/bin/env bash
#
# Builds the Windows executables:
#  1. portable.exe (and BadgeForge-Portable.exe / BadgeForge.exe) - runs directly without installing
#  2. installer.exe (and BadgeForge-Setup.exe) - installs to machine, adds Start Menu/Desktop shortcuts
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
cp "$OUTPUT/BadgeForge.exe" "$OUTPUT/portable.exe"

echo "==> 2/2 Publishing setup installer..."
INSTALLER_TEMP="$ROOT/BadgeForge.Installer/bin/installer-publish"
rm -rf "$INSTALLER_TEMP"
dotnet publish "$ROOT/BadgeForge.Installer/BadgeForge.Installer.csproj" \
    --configuration Release \
    --runtime "$RID" \
    --output "$INSTALLER_TEMP" \
    --nologo

mv "$INSTALLER_TEMP/BadgeForge.Installer.exe" "$OUTPUT/BadgeForge-Setup.exe"
cp "$OUTPUT/BadgeForge-Setup.exe" "$OUTPUT/installer.exe"
rm -rf "$INSTALLER_TEMP"

echo
echo "========================================================="
echo " Portable Exe (Run Direct):  $OUTPUT/portable.exe  ($(du -h "$OUTPUT/portable.exe" | cut -f1))"
echo "                             ($OUTPUT/BadgeForge.exe)"
echo " Installer Exe (Install):    $OUTPUT/installer.exe ($(du -h "$OUTPUT/installer.exe" | cut -f1))"
echo "                             ($OUTPUT/BadgeForge-Setup.exe)"
echo "========================================================="
echo "• portable.exe:   Double-click to use the app immediately without installing."
echo "• installer.exe:  Installs to the machine with Start Menu & Desktop shortcuts."
echo
