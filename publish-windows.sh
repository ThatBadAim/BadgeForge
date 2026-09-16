#!/usr/bin/env bash
#
# Builds the portable Windows exe.
#
# The result is one self-contained BadgeForge.exe: the .NET runtime, Avalonia and SkiaSharp's native libraries are
# all packed inside it, so the printer PC needs no .NET install, no internet access and no installer. Copy the exe
# across and run it.
#
# Usage:  ./publish-windows.sh [runtime-identifier]     (default: win-x64; use win-arm64 for an ARM PC)
#
set -euo pipefail

RID="${1:-win-x64}"
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
OUTPUT="$ROOT/publish/$RID"

case "$RID" in
    win-*) ;;
    *) echo "This script builds the Windows exe; '$RID' is not a Windows runtime identifier." >&2; exit 1 ;;
esac

rm -rf "$OUTPUT"
dotnet publish "$ROOT/BadgeForge.App/BadgeForge.App.csproj" \
    --configuration Release \
    --runtime "$RID" \
    --output "$OUTPUT" \
    --nologo

# A single-file exe carries its own bundle, so renaming it is safe and BadgeForge.exe is what an operator expects
mv "$OUTPUT/BadgeForge.App.exe" "$OUTPUT/BadgeForge.exe"

echo
echo "Portable exe: $OUTPUT/BadgeForge.exe  ($(du -h "$OUTPUT/BadgeForge.exe" | cut -f1))"
echo "Copy that one file to the Windows PC and double-click it. Nothing else is needed."
echo
echo "First run on a PC it was copied to: Windows SmartScreen may warn that the publisher is unknown, because the"
echo "exe is not code-signed. Choose 'More info' then 'Run anyway'. If it came over a network share or download,"
echo "right-click it first, choose Properties, and tick Unblock."
