# Graph Report - Printer Application  (2026-09-16)

## Corpus Check
- 148 files · ~94,518 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 2521 nodes · 6031 edges · 123 communities (117 shown, 5 thin omitted)
- Extraction: 89% EXTRACTED · 11% INFERRED · 0% AMBIGUOUS · INFERRED: 676 edges (avg confidence: 0.83)
- Token cost: 0 input · 0 output

## Community Hubs (Navigation)
- MainWindowViewModel
- .RenderAsync
- InteractiveBadgeCanvas
- .OnPropertyChanged
- MainWindow
- PersonItem
- TemplateDefinition
- BadgeRecord
- TextLayoutEngine
- MainWindowViewModelTests
- Task
- BatchPrintRequest
- ImportSummaryViewModel
- SpoolerCorrelationResult
- BatchPrintEngine
- CardRenderer
- BadgeForge.Core.Templates.Layers
- BadgeForge.Core.Printing.Models
- TemplateMigrationPipeline
- PeopleColumnHeaderUiTests
- ImageSourceLoader
- BadgeForge.App
- .ValidateBatch
- BatchCheckpoint
- MockCardPrinterDriver
- IdpSmart31CardPrinterDriver
- PrinterCapabilities
- PreFlightValidationReport
- PhotoMatchingOptions
- LayerRegistry
- .Render
- .GoldenImage_FixedTemplateAndData_MatchesReferencePixelForPixel
- BatchCardItem
- IReadOnlyList
- TextLayer
- .Snap
- .RunSafelyAsync
- BadgeForge.Core.Data.Models
- .AddDataFieldLayer
- MainWindowPrintingTests
- IRibbonModeController
- IdpSmart31StatusMonitor
- CardPrintJob
- CardFormat
- StaticImageLayer
- PeopleColumnOrderTests
- BatchExecutionState
- ImageAdjustmentTests
- WindowsSpooler
- PrintPathFidelityTests
- TemplateLayer
- ImageAdjustments
- BarcodeValidator
- SKPaint
- XlsxWorksheetReader
- PreFlightValidationTests
- BadgeForge.App.ViewModels
- .ExtractTokensFromString
- BadgeForge.Core.Templates.Models
- PhotoCropMode
- Cardholder
- BatchExecutionEventArgs
- PrinterStatus
- PrinterCapabilityMismatchException
- .ShowDialogAsync
- MockStatusMonitor
- App
- Point
- LayerEditingTests
- ValidationIssue
- SpoolerSnapshot
- RosterTable
- BarcodeLayer
- ICardPrinterStatusMonitor
- PrinterState
- .Build
- .PromptAndApplyNewTemplate
- .GetCardBounds
- BundledFonts
- JsonTemplateStorage
- .Draw
- .ReadCsv
- IBatchPrintEngine
- PrintDocumentHelper
- .PollStatusAsync
- ICardPrinterDriver
- .ReplaceSelectedLayer
- TemplateHistory
- RosterImportService
- .EndColumnDrag
- .WriteAllTextAsync
- CardRendererTests
- LayerIconConverter
- .FindDropTarget
- .Replace
- .Draw
- V1NoOpMigrator
- .OnColumnHeaderPointerPressed
- ErrorPromptKind
- .RunAsync
- ResumeDecision
- Harness
- DesignerEditingUiTests
- Rect
- BatchRecordStatus
- .RunOnUiThread
- publish-windows.sh
- .CreateThumbnail
- PhotoLayer
- DesignerDataAndExportViewModelTests
- .Render
- .AddImageLayerFromFileAsync
- BatchRecordCheckpoint
- .RenderCardAsync
- ImageDiffResult
- .Open
- PersonFieldCell
- CardOutcome
- HeadlessTestApp
- TextAlignment
- .SelectingALayer_BringsTheLayerTabBackFromTheDataTab
- WaitOutcome

## God Nodes (most connected - your core abstractions)
1. `MainWindowViewModel` - 444 edges
2. `MainWindow` - 121 edges
3. `InteractiveBadgeCanvas` - 78 edges
4. `TemplateDefinition` - 77 edges
5. `BatchPrintEngine` - 70 edges
6. `MockCardPrinterDriver` - 67 edges
7. `TextLayer` - 67 edges
8. `TemplateLayer` - 63 edges
9. `PersonItem` - 51 edges
10. `BadgeForge.Core.Templates.Layers` - 46 edges

## Surprising Connections (you probably didn't know these)
- `InteractiveBadgeCanvas` --references--> `CardFormat`  [EXTRACTED]
  BadgeForge.App/Controls/InteractiveBadgeCanvas.cs → BadgeForge.Core.Printing/Models/CardFormat.cs
- `InteractiveBadgeCanvas` --references--> `TemplateLayer`  [EXTRACTED]
  BadgeForge.App/Controls/InteractiveBadgeCanvas.cs → BadgeForge.Core.Templates/Layers/TemplateLayer.cs
- `Harness` --references--> `InteractiveBadgeCanvas`  [EXTRACTED]
  BadgeForge.UiTests/DesignerEditingUiTests.cs → BadgeForge.App/Controls/InteractiveBadgeCanvas.cs
- `Harness` --references--> `InteractiveBadgeCanvas`  [EXTRACTED]
  BadgeForge.UiTests/InlineTextEditorUiTests.cs → BadgeForge.App/Controls/InteractiveBadgeCanvas.cs
- `Harness` --references--> `MainWindow`  [EXTRACTED]
  BadgeForge.UiTests/DesignerEditingUiTests.cs → BadgeForge.App/MainWindow.axaml.cs

## Import Cycles
- None detected.

## Communities (123 total, 5 thin omitted)

### Community 0 - "MainWindowViewModel"
Cohesion: 0.01
Nodes (141): ActiveDriver, ActiveFormat, AllPeopleIncluded, AvailableFormats, AvailablePrinters, BarcodeContent, BarcodeIncludeText, BarcodePrintsAsPureBlack (+133 more)

### Community 1 - ".RenderAsync"
Cohesion: 0.06
Nodes (52): ArgumentOutOfRangeException, CancellationTokenSource, Task, BatchCardRenderService, IBatchCardRenderService, CancellationToken, CardRenderer, IProgress (+44 more)

### Community 2 - "InteractiveBadgeCanvas"
Cohesion: 0.07
Nodes (19): InteractiveBadgeCanvas, Bitmap, Format, InlineEditor, IsInPointerGesture, ViewModel, Zoom, Bitmap (+11 more)

### Community 3 - ".OnPropertyChanged"
Cohesion: 0.12
Nodes (6): ColumnMappingItem, CsvColumn, TemplateToken, EventArgs, ViewModelBase, INotifyPropertyChanged

### Community 4 - "MainWindow"
Cohesion: 0.08
Nodes (3): MainWindow, ViewModel, RoutedEventArgs

### Community 5 - "PersonItem"
Cohesion: 0.08
Nodes (20): PersonItem, Cells, ErrorMessage, HasPhoto, HasPrintStatus, IdentifierText, IsBlank, IsIncluded (+12 more)

### Community 6 - "TemplateDefinition"
Cohesion: 0.10
Nodes (17): PreviewRequest, TemplateDefinition, CreatedAtUtc, Id, Layers, Metadata, ModifiedAtUtc, Name (+9 more)

### Community 7 - "BadgeRecord"
Cohesion: 0.12
Nodes (15): BadgeRecord, BatchIndex, Fields, ResolvedPhotoPath, RowNumber, Dictionary, RosterService, IEnumerable (+7 more)

### Community 8 - "TextLayoutEngine"
Cohesion: 0.13
Nodes (14): TextLayoutEngine, TextLayoutResult, Func, IReadOnlyList, List, TextMeasurer, TextOverflowMode, Ellipsis (+6 more)

### Community 9 - "MainWindowViewModelTests"
Cohesion: 0.11
Nodes (4): MainWindowViewModelTests, Fact, SKColor, Task

### Community 10 - "Task"
Cohesion: 0.08
Nodes (12): ConfirmPrintWithWarningsAsync, PrintCheckResult, PrintJobInfo, CancellationToken, CancellationTokenSource, Dictionary, IReadOnlyDictionary, List (+4 more)

### Community 11 - "BatchPrintRequest"
Cohesion: 0.11
Nodes (25): ArgumentException, IReadOnlyList, BatchPrintRequest, AutoResumeFromCheckpoint, BatchName, BatchRunId, CardCompletionTimeout, Cards (+17 more)

### Community 12 - "ImportSummaryViewModel"
Cohesion: 0.06
Nodes (38): ImportChoice, Append, Cancel, Replace, ImportColumnSummary, ImportPreviewCell, IsEmpty, ImportPreviewRow (+30 more)

### Community 13 - "SpoolerCorrelationResult"
Cohesion: 0.09
Nodes (21): ISpoolerJobCorrelator, SpoolerCorrelationResult, Elapsed, ErrorMessage, ExpectedJobName, IsCorrelated, MatchedJobName, RetriesAttempted (+13 more)

### Community 14 - "BatchPrintEngine"
Cohesion: 0.20
Nodes (12): BatchPrintEngine, CurrentCheckpoint, State, Action, CancellationToken, CancellationTokenSource, Task, TimeSpan (+4 more)

### Community 15 - "CardRenderer"
Cohesion: 0.18
Nodes (14): CardRenderer, DrawMissingPhotoPlaceholders, StrictLayerRendering, CancellationToken, IReadOnlyDictionary, IReadOnlyList, SKBitmap, SKCanvas (+6 more)

### Community 16 - "BadgeForge.Core.Templates.Layers"
Cohesion: 0.14
Nodes (5): BadgeForge.Core.Templates.Layers, BadgeForge.Core.Templates.Enums, BadgeForge.Core.Rendering.Elements, BadgeForge.Core.Templates.Tokens, BadgeForge.Core.Templates.Registry

### Community 17 - "BadgeForge.Core.Printing.Models"
Cohesion: 0.16
Nodes (7): BadgeForge.Core.Printing.Drivers, BadgeForge.Core.Printing.Interfaces, BadgeForge.Core.Printing.Models, BadgeForge.Tests, BadgeForge.Core.Rendering, BadgeForge.Core.Printing.Exceptions, BadgeForge.Core.Printing.Batch

### Community 18 - "TemplateMigrationPipeline"
Cohesion: 0.09
Nodes (18): ITemplateMigrator, SourceVersion, TargetVersion, JsonNode, TemplateMigrationPipeline, ConcurrentDictionary, From, JsonNode (+10 more)

### Community 19 - "PeopleColumnHeaderUiTests"
Cohesion: 0.21
Nodes (12): PeopleFieldColumn, Harness, PeopleColumnHeaderUiTests, Border, Control, Fact, ListBox, Point (+4 more)

### Community 20 - "ImageSourceLoader"
Cohesion: 0.12
Nodes (13): ImageSourceLoader, SupportedExtensions, ImportedImage, Lease, Dictionary, Func, IDisposable, IReadOnlyList (+5 more)

### Community 21 - "BadgeForge.App"
Cohesion: 0.06
Nodes (44): BadgeForge.App, net10.0, SkiaSharp (4.152.0), SkiaSharp.NativeAssets.Linux (4.152.0), Microsoft.NET.Sdk, BadgeForge.Core.Data, net10.0, Microsoft.NET.Sdk (+36 more)

### Community 22 - ".ValidateBatch"
Cohesion: 0.21
Nodes (10): PhotoMatchingService, PreFlightValidationOptions, AllowMissingPhotoAsWarning, CheckDuplicates, EnforceRequirements, PrimaryKeyToken, PreFlightValidationService, Dictionary (+2 more)

### Community 23 - "BatchCheckpoint"
Cohesion: 0.10
Nodes (20): BatchCheckpoint, BatchName, BatchRunId, CompletedCards, FailedCards, IsComplete, LastUpdatedAtUtc, PendingCards (+12 more)

### Community 24 - "MockCardPrinterDriver"
Cohesion: 0.13
Nodes (19): MockCardPrinterDriver, AbortedCardCount, DisplayName, DriverId, HoldCardInPrinterAtBatchIndex, MockStatus, ProbedCapabilities, RibbonController (+11 more)

### Community 25 - "IdpSmart31CardPrinterDriver"
Cohesion: 0.13
Nodes (15): IdpSmart31CardPrinterDriver, DisplayName, DriverId, RibbonController, StatusMonitor, TargetPrinterName, CancellationToken, IReadOnlyList (+7 more)

### Community 26 - "PrinterCapabilities"
Cohesion: 0.11
Nodes (19): PrinterCapabilities, DimensionTolerancePixels, DriverName, IsSimulated, PrintableHeightMm, PrintableHeightPixels, PrintableWidthMm, PrintableWidthPixels (+11 more)

### Community 27 - "PreFlightValidationReport"
Cohesion: 0.12
Nodes (15): PreFlightErrorCodes, PreFlightValidationReport, DuplicateIdentifiers, ErrorCount, Errors, HasErrors, HasWarnings, InvalidBarcodes (+7 more)

### Community 28 - "PhotoMatchingOptions"
Cohesion: 0.29
Nodes (6): PhotoMatchingOptions, AllowedExtensions, FallbackImagePath, FilenamePattern, PhotoDirectory, IReadOnlyList

### Community 29 - "LayerRegistry"
Cohesion: 0.11
Nodes (16): ILayerRegistry, IReadOnlyDictionary, Type, LayerJsonConverter, ConcurrentDictionary, JsonSerializerOptions, Type, LayerRegistry (+8 more)

### Community 30 - ".Render"
Cohesion: 0.29
Nodes (7): BarcodeRenderer, IReadOnlyDictionary, SKCanvas, SKRect, BarcodeFormat, BitMatrix, SKRectI

### Community 31 - ".GoldenImage_FixedTemplateAndData_MatchesReferencePixelForPixel"
Cohesion: 0.30
Nodes (5): ImageDiffService, SKBitmap, GoldenImageTests, Fact, Task

### Community 32 - "BatchCardItem"
Cohesion: 0.14
Nodes (16): BatchCardItem, BackBitmap, BackPixelBuffer, BatchIndex, Format, FrontBitmap, FrontPixelBuffer, Label (+8 more)

### Community 33 - "IReadOnlyList"
Cohesion: 0.12
Nodes (4): HashSet, IEnumerable, IReadOnlyList, PropertyChangedEventArgs

### Community 34 - "TextLayer"
Cohesion: 0.08
Nodes (23): AvaloniaPropertyChangedEventArgs, InlineTextEditor, EffectiveFontSizePoints, Layer, StyleKeyOverride, Dictionary, KeyEventArgs, Type (+15 more)

### Community 35 - ".Snap"
Cohesion: 0.20
Nodes (8): LayerSnapper, SnapResult, IEnumerable, List, LayerSnapperTests, Fact, Guide, Position

### Community 37 - "BadgeForge.Core.Data.Models"
Cohesion: 0.19
Nodes (3): BadgeForge.Core.Data.Validation, BadgeForge.Core.Data.Services, BadgeForge.Core.Data.Models

### Community 38 - ".AddDataFieldLayer"
Cohesion: 0.14
Nodes (10): DataFieldKind, Barcode, Photo, QrCode, Text, TokenSyntax, IEnumerable, Regex (+2 more)

### Community 39 - "MainWindowPrintingTests"
Cohesion: 0.32
Nodes (3): MainWindowPrintingTests, Fact, Task

### Community 40 - "IRibbonModeController"
Cohesion: 0.12
Nodes (15): DefaultRibbonModeController, ActiveMode, BlackPixelThreshold, HardwareVerificationNotes, RequiresRealHardwareVerification, ResinDensityAdjustment, IRibbonModeController, ActiveMode (+7 more)

### Community 41 - "IdpSmart31StatusMonitor"
Cohesion: 0.13
Nodes (9): IdpSmart31StatusMonitor, CardsPrintedSincePause, CurrentStatus, IsMonitoring, Func, HashSet, IReadOnlyCollection, TimeSpan (+1 more)

### Community 42 - "CardPrintJob"
Cohesion: 0.08
Nodes (25): CancellationToken, Task, CardPrintJob, BackPixelBuffer, BatchIndex, Format, FrontPixelBuffer, IsDuplex (+17 more)

### Community 43 - "CardFormat"
Cohesion: 0.11
Nodes (15): CardFormat, AspectRatio, CR79, CR80, Dpi, HeightMm, HeightPixels, IsPortrait (+7 more)

### Community 44 - "StaticImageLayer"
Cohesion: 0.14
Nodes (14): IImageLayer, Adjustments, BorderRadius, CropMode, StaticImageLayer, Adjustments, Base64Data, BorderRadius (+6 more)

### Community 45 - "PeopleColumnOrderTests"
Cohesion: 0.11
Nodes (18): PeopleColumnLayout, IsCustomised, From, IReadOnlyList, List, To, BoundTemplate, TemplateBinder (+10 more)

### Community 46 - "BatchExecutionState"
Cohesion: 0.11
Nodes (18): BatchExecutionState, Cancelled, Completed, Idle, PausedByUser, PausedForHopper, PausedOnError, Running (+10 more)

### Community 47 - "ImageAdjustmentTests"
Cohesion: 0.22
Nodes (5): ImageAdjustmentTests, Fact, SKBitmap, SKColor, Task

### Community 48 - "WindowsSpooler"
Cohesion: 0.23
Nodes (10): JobInfo1, SystemTime, WindowsSpooler, Func, IReadOnlyCollection, BufferCall, DllImport, IntPtr (+2 more)

### Community 49 - "PrintPathFidelityTests"
Cohesion: 0.36
Nodes (3): PrintPathFidelityTests, Fact, Task

### Community 50 - "TemplateLayer"
Cohesion: 0.15
Nodes (12): TemplateLayer, HasDynamicTokens, Id, IsLocked, IsVisible, LayerType, Name, Opacity (+4 more)

### Community 51 - "ImageAdjustments"
Cohesion: 0.12
Nodes (14): ImageAdjustments, Brightness, Contrast, FlipHorizontal, FlipVertical, HasColorAdjustments, IsIdentityTransform, None (+6 more)

### Community 52 - "BarcodeValidator"
Cohesion: 0.20
Nodes (11): BarcodeValidator, Regex, BarcodeValidationResult, IBarcodeValidator, BarcodeSymbology, Code128, Code39, Ean13 (+3 more)

### Community 53 - "SKPaint"
Cohesion: 0.21
Nodes (8): PhotoRenderer, IReadOnlyDictionary, SKBitmap, SKCanvas, SKRect, Stream, SKEncodedOrigin, SKPaint

### Community 54 - "XlsxWorksheetReader"
Cohesion: 0.26
Nodes (7): XlsxWorksheetReader, HashSet, IReadOnlyList, List, Stream, XElement, ZipArchive

### Community 55 - "PreFlightValidationTests"
Cohesion: 0.21
Nodes (6): PreFlightValidationBlockedException, Report, PreFlightValidationTests, Fact, Task, InvalidOperationException

### Community 56 - "BadgeForge.App.ViewModels"
Cohesion: 0.21
Nodes (5): BadgeForge.UiTests, BadgeForge.App.ViewModels, BadgeForge.App.Views, BadgeForge.App.Controls, BadgeForge.App

### Community 57 - ".ExtractTokensFromString"
Cohesion: 0.22
Nodes (4): IEnumerable, IEnumerable, IEnumerable, IEnumerable

### Community 58 - "BadgeForge.Core.Templates.Models"
Cohesion: 0.18
Nodes (5): BadgeForge.Core.Templates.Migration, BadgeForge.Core.Templates.Storage, BadgeForge.Core.Templates.Models, BadgeForge.Core.Rendering.Batch, BadgeForge.Core.Rendering.Output

### Community 59 - "PhotoCropMode"
Cohesion: 0.27
Nodes (8): ImageLayout, SKRect, PhotoCropMode, AspectFill, AspectFit, Stretch, InlineData, Theory

### Community 60 - "Cardholder"
Cohesion: 0.13
Nodes (15): Cardholder, AdditionalFields, Department, FullName, Id, IsSelected, JobTitle, PhotoPath (+7 more)

### Community 61 - "BatchExecutionEventArgs"
Cohesion: 0.12
Nodes (16): BatchExecutionEventArgs, CurrentCardIndex, CurrentRecord, CurrentState, Message, PauseReason, PreviousState, TotalCards (+8 more)

### Community 62 - "PrinterStatus"
Cohesion: 0.13
Nodes (14): CancellationToken, Task, PrinterStatus, ActiveJobId, CardsPrintedSincePause, InputHopperCount, IsNativeStatus, IsReadyToPrint (+6 more)

### Community 63 - "PrinterCapabilityMismatchException"
Cohesion: 0.14
Nodes (16): OperatorPauseRequiredException, CardsPrintedInBatch, MaxCardsBeforePause, PrinterCapabilityMismatchException, DriverCanvasHeight, DriverCanvasWidth, DriverDpiX, DriverDpiY (+8 more)

### Community 64 - ".ShowDialogAsync"
Cohesion: 0.13
Nodes (5): List, Task, Task, Window, WindowClosingEventArgs

### Community 65 - "MockStatusMonitor"
Cohesion: 0.13
Nodes (9): MockStatusMonitor, CurrentStatus, IsMonitoring, Func, TimeSpan, PrinterStatusChangedEventArgs, CurrentStatus, PreviousStatus (+1 more)

### Community 66 - "App"
Cohesion: 0.22
Nodes (5): Application, App, Program, AppBuilder, STAThread

### Community 67 - "Point"
Cohesion: 0.23
Nodes (3): IEnumerable, Point, PointerPressedEventArgs

### Community 69 - "ValidationIssue"
Cohesion: 0.17
Nodes (11): ValidationIssue, BatchIndex, ErrorCode, Message, RecordIdentifier, RowNumber, Severity, TargetName (+3 more)

### Community 70 - "SpoolerSnapshot"
Cohesion: 0.22
Nodes (9): SpoolerJob, IsFinished, SpoolerSnapshot, SpoolerStatusMapper, IReadOnlyList, State, InlineData, Theory (+1 more)

### Community 71 - "RosterTable"
Cohesion: 0.12
Nodes (15): RosterFileFormat, Csv, LegacyXls, Xlsx, RosterTable, ColumnCount, Columns, Format (+7 more)

### Community 72 - "BarcodeLayer"
Cohesion: 0.29
Nodes (7): BarcodeLayer, ContentToken, IncludeText, IsPureBlackKResin, IsRequired, LayerType, Symbology

### Community 73 - "ICardPrinterStatusMonitor"
Cohesion: 0.17
Nodes (6): ICardPrinterStatusMonitor, CurrentStatus, IsMonitoring, CancellationToken, Task, TimeSpan

### Community 74 - "PrinterState"
Cohesion: 0.17
Nodes (11): PrinterState, CoverOpen, Error, Offline, OperatorPauseRequired, OutOfCards, OutOfRibbon, PaperJam (+3 more)

### Community 75 - ".Build"
Cohesion: 0.16
Nodes (12): ColumnMapping, Mappings, Dictionary, IReadOnlyDictionary, RosterRow, CsvDataIngestionService, IReadOnlyList, Stream (+4 more)

### Community 76 - ".PromptAndApplyNewTemplate"
Cohesion: 0.14
Nodes (3): Action, KeyEventArgs, TextBox

### Community 77 - ".GetCardBounds"
Cohesion: 0.20
Nodes (4): DragEventArgs, PointerWheelEventArgs, Size, Vector

### Community 78 - "BundledFonts"
Cohesion: 0.33
Nodes (6): BundledFonts, Dictionary, IReadOnlyList, SKFontStyleWeight, SKTypeface, SKFontStyleSlant

### Community 79 - "JsonTemplateStorage"
Cohesion: 0.15
Nodes (12): JsonTemplateStorage, CancellationToken, JsonSerializerOptions, Task, SignatureCaptureLayer, LayerType, PenColorHex, SignatureToken (+4 more)

### Community 80 - ".Draw"
Cohesion: 0.20
Nodes (8): ImageDrawing, SKBitmap, SKCanvas, SKRect, StaticImageRenderer, SKCanvas, SKRect, SKColorFilter

### Community 82 - "IBatchPrintEngine"
Cohesion: 0.33
Nodes (5): IBatchPrintEngine, CurrentCheckpoint, State, CancellationToken, Task

### Community 83 - "PrintDocumentHelper"
Cohesion: 0.25
Nodes (9): PrintDocumentHelper, Bitmap, SKBitmap, SupportedOSPlatform, Graphics, PageSettings, PaperSize, PrintDocument (+1 more)

### Community 84 - ".PollStatusAsync"
Cohesion: 0.33
Nodes (6): DefaultNativeIdpStatusProvider, IsAvailable, INativeIdpStatusProvider, IsAvailable, CancellationToken, Task

### Community 85 - "ICardPrinterDriver"
Cohesion: 0.25
Nodes (8): ICardPrinterDriver, DisplayName, DriverId, RibbonController, StatusMonitor, TargetPrinterName, CancellationToken, Task

### Community 86 - ".ReplaceSelectedLayer"
Cohesion: 0.12
Nodes (3): Func, Height, Width

### Community 87 - "TemplateHistory"
Cohesion: 0.13
Nodes (13): TemplateHistory, CanRedo, CanUndo, RedoLabel, UndoDepth, UndoLabel, TemplateSnapshot, DateTime (+5 more)

### Community 88 - "RosterImportService"
Cohesion: 0.20
Nodes (7): RosterImportService, IReadOnlyList, List, CardholderRosterTests, Fact, InvalidDataException, NotSupportedException

### Community 89 - ".EndColumnDrag"
Cohesion: 0.22
Nodes (3): PointerCaptureLostEventArgs, PointerEventArgs, PointerReleasedEventArgs

### Community 90 - ".WriteAllTextAsync"
Cohesion: 0.36
Nodes (4): AtomicFileWriter, CancellationToken, Task, Encoding

### Community 91 - "CardRendererTests"
Cohesion: 0.39
Nodes (3): CardRendererTests, Fact, Task

### Community 92 - "LayerIconConverter"
Cohesion: 0.13
Nodes (13): BoolToDimOpacityConverter, CultureInfo, Type, LayerIconConverter, CultureInfo, Geometry, Type, LayerVisibilityIconConverter (+5 more)

### Community 93 - ".FindDropTarget"
Cohesion: 0.21
Nodes (7): Button, Control, DragEventArgs, Highlight, OnPhoto, Person, Visual

### Community 94 - ".Replace"
Cohesion: 0.30
Nodes (4): Func, IReadOnlyDictionary, TokenSyntaxTests, Fact

### Community 95 - ".Draw"
Cohesion: 0.33
Nodes (4): PhotoPlaceholder, SKCanvas, SKColor, SKRect

### Community 96 - "V1NoOpMigrator"
Cohesion: 0.33
Nodes (4): V1NoOpMigrator, SourceVersion, TargetVersion, JsonNode

### Community 98 - "ErrorPromptKind"
Cohesion: 0.50
Nodes (4): ErrorPromptKind, Dismiss, OutcomeUnknown, Retry

### Community 99 - ".RunAsync"
Cohesion: 0.29
Nodes (7): BatchStopControlTests, Fact, Func, List, Task, Events, Summary

### Community 100 - "ResumeDecision"
Cohesion: 0.40
Nodes (5): ResumeDecision, AbortCard, Continue, FinishCard, Reprint

### Community 101 - "Harness"
Cohesion: 0.25
Nodes (10): Harness, PeopleListUiTests, Button, Fact, ListBox, PhysicalKey, RawInputModifiers, Task (+2 more)

### Community 102 - "DesignerEditingUiTests"
Cohesion: 0.33
Nodes (6): DesignerEditingUiTests, Harness, Fact, PhysicalKey, RawInputModifiers, Task

### Community 103 - "Rect"
Cohesion: 0.36
Nodes (3): Rect, DrawingContext, FormattedText

### Community 104 - "BatchRecordStatus"
Cohesion: 0.29
Nodes (6): BatchRecordStatus, Failed, Pending, Printing, Skipped, Success

### Community 105 - ".RunOnUiThread"
Cohesion: 0.31
Nodes (8): Harness, InlineTextEditorUiTests, Action, Border, Fact, Func, Task, Window

### Community 107 - ".CreateThumbnail"
Cohesion: 0.15
Nodes (4): AvaloniaBitmap, ObservableCollection, Task, PreviewRequest

### Community 108 - "PhotoLayer"
Cohesion: 0.18
Nodes (10): PhotoLayer, Adjustments, BorderRadius, CropMode, FallbackImagePath, IsRequired, LayerType, SourceToken (+2 more)

### Community 109 - "DesignerDataAndExportViewModelTests"
Cohesion: 0.25
Nodes (5): DesignerDataAndExportViewModelTests, Fact, Layer, Task, Vm

### Community 110 - ".Render"
Cohesion: 0.23
Nodes (8): IEnumerable, TextRenderer, IReadOnlyDictionary, SKCanvas, SKFontStyleWeight, SKRect, SKTypeface, SKFont

### Community 111 - ".AddImageLayerFromFileAsync"
Cohesion: 0.30
Nodes (4): Rect, CropBoxTests, Fact, Task

### Community 112 - "BatchRecordCheckpoint"
Cohesion: 0.17
Nodes (12): BatchRecordCheckpoint, BatchIndex, CompletedAtUtc, CorrelatedJobName, ErrorMessage, Label, RecordId, RetryCount (+4 more)

### Community 113 - ".RenderCardAsync"
Cohesion: 0.31
Nodes (6): ICardRenderer, CancellationToken, IReadOnlyDictionary, IReadOnlyList, SKBitmap, Task

### Community 114 - "ImageDiffResult"
Cohesion: 0.22
Nodes (8): ImageDiffResult, DiffBitmap, IsMatch, MaxChannelDelta, MismatchPercentage, MismatchPixels, TotalPixels, BadgeForge.Core.Rendering.Testing

### Community 115 - ".Open"
Cohesion: 0.42
Nodes (5): Harness, PrintStopUiTests, Fact, Func, Task

### Community 116 - "PersonFieldCell"
Cohesion: 0.33
Nodes (6): PersonFieldCell, Key, Label, Owner, Token, Value

### Community 117 - "CardOutcome"
Cohesion: 0.33
Nodes (6): CardOutcome, Printed, StoppedAfterCardFinished, StoppedBeforeCard, StoppedCardAborted, StoppedCardUnknown

### Community 118 - "HeadlessTestApp"
Cohesion: 0.33
Nodes (5): HeadlessTestApp, App, AppBuilder, Lazy, HeadlessUnitTestSession

### Community 119 - "TextAlignment"
Cohesion: 0.40
Nodes (4): TextAlignment, Center, Left, Right

### Community 120 - ".SelectingALayer_BringsTheLayerTabBackFromTheDataTab"
Cohesion: 0.40
Nodes (4): DesignPageUiTests, Fact, Task, TabControl

### Community 121 - "WaitOutcome"
Cohesion: 0.67
Nodes (3): WaitOutcome, Ready, StopRequested

## Knowledge Gaps
- **630 isolated node(s):** `net10.0`, `Avalonia (12.1.2)`, `Avalonia.Desktop (12.1.2)`, `Avalonia.Themes.Fluent (12.1.2)`, `Avalonia.Fonts.Inter (12.1.2)` (+625 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 901 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **5 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `MainWindowViewModel` connect `MainWindowViewModel` to `.RenderAsync`, `InteractiveBadgeCanvas`, `.OnPropertyChanged`, `MainWindow`, `PersonItem`, `TemplateDefinition`, `BadgeRecord`, `MainWindowViewModelTests`, `Task`, `BatchPrintEngine`, `CardRenderer`, `BadgeForge.Core.Templates.Layers`, `PeopleColumnHeaderUiTests`, `ImageSourceLoader`, `.ValidateBatch`, `IReadOnlyList`, `TextLayer`, `.Snap`, `.AddDataFieldLayer`, `MainWindowPrintingTests`, `CardFormat`, `StaticImageLayer`, `PeopleColumnOrderTests`, `BatchExecutionState`, `ImageAdjustmentTests`, `TemplateLayer`, `ImageAdjustments`, `BadgeForge.Core.Templates.Models`, `Point`, `LayerEditingTests`, `RosterTable`, `BarcodeLayer`, `JsonTemplateStorage`, `ICardPrinterDriver`, `.ReplaceSelectedLayer`, `TemplateHistory`, `RosterImportService`, `ErrorPromptKind`, `Harness`, `DesignerEditingUiTests`, `.RunOnUiThread`, `.CreateThumbnail`, `PhotoLayer`, `DesignerDataAndExportViewModelTests`, `.AddImageLayerFromFileAsync`, `.Open`, `TextAlignment`?**
  _High betweenness centrality (0.568) - this node is a cross-community bridge._
- **Why does `MainWindow` connect `MainWindow` to `.ShowDialogAsync`, `.OnColumnHeaderPointerPressed`, `App`, `MainWindowViewModel`, `.RunSafelyAsync`, `Harness`, `DesignerEditingUiTests`, `.PromptAndApplyNewTemplate`, `BadgeForge.Core.Templates.Layers`, `PeopleColumnHeaderUiTests`, `.Open`, `.SelectingALayer_BringsTheLayerTabBackFromTheDataTab`, `.EndColumnDrag`, `.FindDropTarget`?**
  _High betweenness centrality (0.113) - this node is a cross-community bridge._
- **Why does `BatchPrintEngine` connect `BatchPrintEngine` to `MainWindowViewModel`, `.RunAsync`, `ResumeDecision`, `BatchPrintRequest`, `SpoolerCorrelationResult`, `BatchExecutionState`, `BadgeForge.Core.Printing.Models`, `IBatchPrintEngine`, `CardOutcome`, `BatchCheckpoint`, `MockCardPrinterDriver`, `WaitOutcome`, `BatchExecutionEventArgs`?**
  _High betweenness centrality (0.112) - this node is a cross-community bridge._
- **Are the 8 inferred relationships involving `MainWindowViewModel` (e.g. with `.CropBoxDrag_ChangesTheFrame_ButNotWhereThePictureSitsOnTheCard()` and `.CropBoxDrag_IsOneUndoStep_AndUndoRestoresTheFrameAndThePicture()`) actually correct?**
  _`MainWindowViewModel` has 8 INFERRED edges - model-reasoned connections that need verification._
- **Are the 2 inferred relationships involving `MainWindow` (e.g. with `.OnFrameworkInitializationCompleted()` and `.SelectingALayer_BringsTheLayerTabBackFromTheDataTab()`) actually correct?**
  _`MainWindow` has 2 INFERRED edges - model-reasoned connections that need verification._
- **Are the 20 inferred relationships involving `TemplateDefinition` (e.g. with `.BarcodeTooLongForItsLayer_StaysInsideTheLayerInPreviews_AndIsRejectedForPrinting()` and `.CardSizeThePrinterDoesNotTake_StillFailsLoudlyWhenPrinting()`) actually correct?**
  _`TemplateDefinition` has 20 INFERRED edges - model-reasoned connections that need verification._
- **What connects `net10.0`, `Avalonia (12.1.2)`, `Avalonia.Desktop (12.1.2)` to the rest of the system?**
  _630 weakly-connected nodes found - possible documentation gaps or missing edges._