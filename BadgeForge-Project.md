# BadgeForge — Project Specification

## Overview
BadgeForge is a Windows desktop application that replaces proprietary vendor card software for designing and mass-printing photo ID badges to an IDP SMART-31 direct-to-card (DTC) printer. It supports unattended batch printing from a data list, a drag-and-drop badge editor, and configurable card size/orientation.

## Core Objectives
1. **Batch printing** — load a list (CSV/spreadsheet), start a run, walk away. App handles sequencing, error recovery, and mandatory operator pauses for hopper limits.
2. **Badge editor** — drag/drop image and text elements onto a canvas; save as a reusable template; bind fields to data columns.
3. **Configurable card size/orientation** — user picks card format and landscape/portrait in-app, not hardcoded.
4. **Professional, non-generic UI** — consistent spacing grid, restrained color use, clear visual hierarchy (see Design System below).

## Tech Stack
- .NET 9 / Windows Desktop (WPF or Avalonia)
- SkiaSharp — 300 DPI bitmap rendering
- ZXing.Net — barcode generation
- CsvHelper — CSV/data ingestion
- System.Drawing.Printing + System.Printing — spooler and print queue management

## Hardware & Physical Constraints
- Card format is **configurable data**, not a constant — default profile: CR80 (85.60mm × 53.98mm), 300 DPI, ~1013×638px, ~6px safe margin.
- **Canvas resolution must be queried from the printer driver at runtime** (`PrinterSettings.DefaultPageSettings.PrinterResolution`/`PrintableArea`), validated against the expected profile, and the app must fail loudly (not silently stretch) if they disagree.
- Input hopper: 80 cards. Output hopper: 25 cards. Mandatory operator pause every 20 cards.
- Text/barcodes must render as pure black with anti-aliasing disabled (`SKFont.Edging = Alias`, `Subpixel = false`) to route correctly to the K-resin ribbon panel rather than the YMC dye panels — **this must be verified against real IDP SMART-31 driver behavior before the rendering module is finalized**, as some drivers use a private DEVMODE flag rather than pixel-value detection.

## Building the Portable Windows Exe
Run `./publish-windows.sh` (Linux/macOS) or `publish-windows.cmd` (Windows) to produce
`publish/win-x64/BadgeForge.exe` — a single self-contained file, ~70MB.

- The .NET runtime, Avalonia and SkiaSharp's native libraries are packed inside the exe, so the printer PC needs
  **no .NET install, no internet access and no installer**. Copy that one file across and double-click it.
- Pass a runtime identifier to target something else: `./publish-windows.sh win-arm64`.
- The exe is not code-signed, so on a PC it was copied to, Windows SmartScreen warns that the publisher is unknown
  ("More info" → "Run anyway"), and a copy that arrived over a network share or download needs right-click →
  Properties → **Unblock** first.
- The settings live in a `RuntimeIdentifier.StartsWith('win')` property group in `BadgeForge.App.csproj`, so plain
  `dotnet build` and the test suite are unaffected. `PublishTrimmed` is deliberately off — Avalonia resolves
  controls and bindings by reflection, which the trimmer can't see through.

## Design System / Colour Palette
Use as **accent colours only**, against a neutral white/light-grey UI base — do not recolor the whole interface with these. Consistent 8px spacing grid, one font family, restrained hierarchy (the badge preview is the visual focus of the screen; controls stay quiet).

| Role | Name | Hex |
|---|---|---|
| Primary / header & primary buttons | Grey Olive | `#7A918D` |
| Secondary / active states, borders | Muted Teal | `#93B1A7` |
| Secondary (lighter) / hover states | Muted Teal | `#99C2A2` |
| Success / positive status tags | Tea Green | `#C5EDAC` |
| Light background / subtle highlight | Tea Green | `#DBFEB8` |

Suggested usage: Grey Olive for the top bar and primary "Start Batch" button; Muted Teal for selected-layer outlines and secondary buttons; Tea Green tones reserved for "Done"/"Success" status chips in the batch grid and light section backgrounds — not for large fills.

## Architecture
1. **Printer Hardware Abstraction Layer** (`ICardPrinterDriver`) — capabilities probe, ribbon-mode controller, status monitor, submit method. IDP SMART-31 is one implementation; future printer models are additional implementations, not rewrites.
2. **Template & Schema Engine** (`BadgeForge.Core.Templates`) — JSON-serializable `TemplateDefinition` with versioned schema, layer registry (`TextLayer`, `PhotoLayer`, `StaticImageLayer`, `BarcodeLayer`, extensible to future layer types).
3. **Rendering Engine** (`BadgeForge.Core.Rendering`) — SkiaSharp `CardRenderer`, EXIF-aware photo rotation, aspect-fill cropping, strict 1-bit text/barcode rendering.
4. **Data Ingestion & Pre-Flight** (`BadgeForge.Core.Data`) — CSV mapping, photo-folder matching, validation report (missing fields, missing photos, barcode validity, duplicate IDs), block-on-critical-errors gate.
5. **Sequential Print Spooler Engine** (`BadgeForge.Core.Printing`) — one card at a time, real printer-status gating (not just spooler-empty), job-ID correlation, hopper pause logic, persisted batch checkpoint (resume after crash/jam).
6. **UI Dashboard** — three-panel layout: layer/template editor (left), live preview with safe-area guide + record pagination (center), data mapping/printer/batch-status/controls (right).

## Known Risks / Must-Verify-With-Real-Hardware
- Canvas pixel dimensions vs. driver-reported paper size can mismatch and cause silent bitmap stretching — must assert, not assume.
- K-panel (pure black → resin) routing mechanism is driver-specific — confirm with IDP SDK/docs.
- Spooler-queue-empty is not proof of physical card ejection — use native printer status API if available, spooler polling as fallback only.
- Vendor SDK may be 32-bit-only / COM-based — confirm before committing to a pure .NET 9 process; may need an out-of-process shim.

## Future-Proofing Requirements
- No hardcoded printer model, card size, or DPI anywhere outside the driver/config layer.
- Template schema includes a version field with a migration path.
- Layer types and barcode symbologies are registered, not hardcoded in a switch statement.
- Golden-image regression tests (rendered output hashed/diffed against a reference) protect the rendering pipeline over time.
- Mock printer driver and status monitor allow full batch/pause/error-recovery logic to be tested without hardware.

## Open Decisions To Confirm Before/During Build
- [ ] Exact data source format and how photos are matched to records (filename pattern, column reference)
- [ ] How many distinct badge templates are needed (staff/visitor/contractor/student)
- [ ] Behavior when a photo is missing or malformed (skip+flag / placeholder / block batch)
- [ ] Single-user or multi-user with permissions
- [ ] Whether a persistent print history/reprint log is required
- [ ] Where photos and personal data are stored, and any school data-handling requirements to follow
