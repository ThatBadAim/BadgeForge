# Graph Report - Printer Application  (2026-09-15)

## Corpus Check
- 128 files · ~75,927 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 2134 nodes · 4871 edges · 108 communities (96 shown, 11 thin omitted)
- Extraction: 90% EXTRACTED · 10% INFERRED · 0% AMBIGUOUS · INFERRED: 496 edges (avg confidence: 0.83)
- Token cost: 0 input · 0 output

## Community Hubs (Navigation)
- MainWindowViewModel
- .RenderAsync
- InteractiveBadgeCanvas
- .RequestLivePreviewUpdate
- MainWindow
- PersonItem
- TemplateDefinition
- BadgeRecord
- TextLayoutEngine
- MainWindowViewModelTests
- Task
- MockCardPrinterDriver
- .AddPerson
- SpoolerCorrelationResult
- BatchPrintEngine
- CardRenderer
- BadgeForge.Core.Templates.Layers
- BadgeForge.Core.Printing.Models
- TemplateMigrationPipeline
- PersonFieldCell
- ImageSourceLoader
- BadgeForge.App
- .ValidateBatch
- .SaveCheckpointAsync
- BatchPrintRequest
- IdpSmart31CardPrinterDriver
- PrinterCapabilities
- PreFlightValidationReport
- SKPaint
- LayerRegistry
- BarcodeValidator
- .GoldenImage_FixedTemplateAndData_MatchesReferencePixelForPixel
- BatchCardItem
- .Open
- InlineTextEditor
- .Snap
- .RunSafelyAsync
- MainWindowViewModel.cs
- .Replace
- MainWindowPrintingTests
- IRibbonModeController
- IdpSmart31StatusMonitor
- .SubmitCardAsync
- CardFormat
- StaticImageLayer
- DesignerDataAndExportViewModelTests
- BatchExecutionState
- ImageAdjustmentTests
- WindowsSpooler
- .Render
- TemplateSerializationTests.cs
- ImageAdjustments
- .Render
- Rect
- XlsxWorksheetReader
- CardPrintJob
- InteractiveBadgeCanvas.cs
- BarcodeLayer
- .ReplaceSelectedLayer
- PhotoCropMode
- Cardholder
- BatchExecutionEventArgs
- PrinterStatus
- PrinterCapabilityMismatchException
- Task
- MockStatusMonitor
- BadgeForge.App.ViewModels
- CardholderMapper
- RosterImportService
- ValidationIssue
- SpoolerSnapshot
- TemplateLayer
- BatchRecordCheckpoint
- ICardPrinterStatusMonitor
- PrinterState
- ColumnMapping
- .PromptAndApplyNewTemplate
- .GetCardBounds
- BundledFonts
- .Draw
- TextLayer
- .ShowDialogAsync
- IBatchPrintEngine
- .ConfigurePrintDocument
- .PollStatusAsync
- ICardPrinterDriver
- .RenderCardAsync
- PhotoLayer
- CardholderRosterTests
- .OnPointerMoved
- .WriteAllTextAsync
- CardRendererTests
- BoolToDimOpacityConverter
- .FindDropTarget
- LayerIconConverter
- LayerVisibilityIconConverter
- V1NoOpMigrator
- BatchCardRenderServiceTests.cs
- ErrorPromptKind
- PrinterStatusChangedEventArgs
- ResumeDecision
- .FocusPersonField
- PrinterException
- BatchRecordStatus
- .Draw
- CardFormatTests
- HeadlessTestApp

## God Nodes (most connected - your core abstractions)
1. `MainWindowViewModel` - 378 edges
2. `MainWindow` - 95 edges
3. `TemplateDefinition` - 74 edges
4. `InteractiveBadgeCanvas` - 71 edges
5. `TextLayer` - 67 edges
6. `TemplateLayer` - 60 edges
7. `MockCardPrinterDriver` - 53 edges
8. `BatchPrintEngine` - 51 edges
9. `PersonItem` - 49 edges
10. `BadgeForge.Core.Templates.Layers` - 41 edges

## Surprising Connections (you probably didn't know these)
- `InlineTextEditor` --references--> `TextLayer`  [EXTRACTED]
  BadgeForge.App/Controls/InlineTextEditor.cs → BadgeForge.Core.Templates/Layers/TextLayer.cs
- `InteractiveBadgeCanvas` --references--> `CardFormat`  [EXTRACTED]
  BadgeForge.App/Controls/InteractiveBadgeCanvas.cs → BadgeForge.Core.Printing/Models/CardFormat.cs
- `InteractiveBadgeCanvas` --references--> `TemplateLayer`  [EXTRACTED]
  BadgeForge.App/Controls/InteractiveBadgeCanvas.cs → BadgeForge.Core.Templates/Layers/TemplateLayer.cs
- `Harness` --references--> `InteractiveBadgeCanvas`  [EXTRACTED]
  BadgeForge.UiTests/InlineTextEditorUiTests.cs → BadgeForge.App/Controls/InteractiveBadgeCanvas.cs
- `Harness` --references--> `MainWindow`  [EXTRACTED]
  BadgeForge.UiTests/PeopleListUiTests.cs → BadgeForge.App/MainWindow.axaml.cs

## Import Cycles
- None detected.

## Communities (108 total, 11 thin omitted)

### Community 0 - "MainWindowViewModel"
Cohesion: 0.01
Nodes (131): ActiveDriver, ActiveFormat, AllPeopleIncluded, AvailableFormats, AvailablePrinters, BarcodeContent, BarcodeIncludeText, BarcodePrintsAsPureBlack (+123 more)

### Community 1 - ".RenderAsync"
Cohesion: 0.05
Nodes (53): ArgumentOutOfRangeException, CancellationTokenSource, Task, BatchCardRenderService, IBatchCardRenderService, CancellationToken, CardRenderer, IEnumerable (+45 more)

### Community 2 - "InteractiveBadgeCanvas"
Cohesion: 0.08
Nodes (17): InteractiveBadgeCanvas, Bitmap, Format, InlineEditor, IsInPointerGesture, ViewModel, Zoom, EventArgs (+9 more)

### Community 3 - ".RequestLivePreviewUpdate"
Cohesion: 0.11
Nodes (4): Action, EventArgs, LayerEditingTests, Fact

### Community 4 - "MainWindow"
Cohesion: 0.10
Nodes (3): MainWindow, ViewModel, RoutedEventArgs

### Community 5 - "PersonItem"
Cohesion: 0.05
Nodes (29): ColumnMappingItem, CsvColumn, TemplateToken, AvaloniaBitmap, ObservableCollection, PersonItem, Cells, ErrorMessage (+21 more)

### Community 6 - "TemplateDefinition"
Cohesion: 0.10
Nodes (23): TemplateDefinition, CreatedAtUtc, Id, Layers, Metadata, ModifiedAtUtc, Name, SchemaVersion (+15 more)

### Community 7 - "BadgeRecord"
Cohesion: 0.12
Nodes (15): BadgeRecord, BatchIndex, Fields, ResolvedPhotoPath, RowNumber, Dictionary, RosterService, IEnumerable (+7 more)

### Community 8 - "TextLayoutEngine"
Cohesion: 0.13
Nodes (14): TextLayoutEngine, TextLayoutResult, Func, IReadOnlyList, List, TextMeasurer, TextOverflowMode, Ellipsis (+6 more)

### Community 9 - "MainWindowViewModelTests"
Cohesion: 0.11
Nodes (5): Dictionary, MainWindowViewModelTests, Fact, SKColor, Task

### Community 10 - "Task"
Cohesion: 0.10
Nodes (9): ConfirmPrintWithWarningsAsync, PreviewRequest, PrintJobInfo, CancellationToken, CancellationTokenSource, IReadOnlyDictionary, List, Task (+1 more)

### Community 11 - "MockCardPrinterDriver"
Cohesion: 0.12
Nodes (24): MockCardPrinterDriver, DisplayName, DriverId, MockStatus, ProbedCapabilities, RibbonController, SimulatedPrintDelayMs, SimulatePaperJamAtBatchIndex (+16 more)

### Community 12 - ".AddPerson"
Cohesion: 0.12
Nodes (6): HashSet, IEnumerable, IReadOnlyList, PropertyChangedEventArgs, PeopleFieldColumn, Label

### Community 13 - "SpoolerCorrelationResult"
Cohesion: 0.09
Nodes (21): ISpoolerJobCorrelator, SpoolerCorrelationResult, Elapsed, ErrorMessage, ExpectedJobName, IsCorrelated, MatchedJobName, RetriesAttempted (+13 more)

### Community 14 - "BatchPrintEngine"
Cohesion: 0.14
Nodes (22): BatchCheckpoint, BatchName, BatchRunId, CompletedCards, FailedCards, IsComplete, LastUpdatedAtUtc, PendingCards (+14 more)

### Community 15 - "CardRenderer"
Cohesion: 0.18
Nodes (14): CardRenderer, DrawMissingPhotoPlaceholders, StrictLayerRendering, CancellationToken, IReadOnlyDictionary, IReadOnlyList, SKBitmap, SKCanvas (+6 more)

### Community 16 - "BadgeForge.Core.Templates.Layers"
Cohesion: 0.14
Nodes (7): BadgeForge.Core.Templates.Layers, BadgeForge.Core.Templates.Enums, BadgeForge.Core.Rendering.Elements, BadgeForge.Core.Templates.Tokens, BadgeForge.Core.Rendering, BadgeForge.Core.Templates.Models, BadgeForge.Core.Templates.Registry

### Community 17 - "BadgeForge.Core.Printing.Models"
Cohesion: 0.21
Nodes (6): BadgeForge.Core.Printing.Drivers, BadgeForge.Core.Printing.Interfaces, BadgeForge.Core.Printing.Models, BadgeForge.Tests, BadgeForge.Core.Printing.Exceptions, BadgeForge.Core.Printing.Batch

### Community 18 - "TemplateMigrationPipeline"
Cohesion: 0.09
Nodes (19): ITemplateMigrator, SourceVersion, TargetVersion, JsonNode, TemplateMigrationPipeline, ConcurrentDictionary, JsonNode, TemplateSerializationTests (+11 more)

### Community 19 - "PersonFieldCell"
Cohesion: 0.16
Nodes (16): PersonFieldCell, Key, Label, Owner, Token, Value, Harness, PeopleListUiTests (+8 more)

### Community 20 - "ImageSourceLoader"
Cohesion: 0.12
Nodes (13): ImageSourceLoader, SupportedExtensions, ImportedImage, Lease, Dictionary, Func, IDisposable, IReadOnlyList (+5 more)

### Community 21 - "BadgeForge.App"
Cohesion: 0.06
Nodes (44): BadgeForge.App, net10.0, SkiaSharp (4.152.0), SkiaSharp.NativeAssets.Linux (4.152.0), Microsoft.NET.Sdk, BadgeForge.Core.Data, net10.0, Microsoft.NET.Sdk (+36 more)

### Community 22 - ".ValidateBatch"
Cohesion: 0.13
Nodes (16): PhotoMatchingOptions, AllowedExtensions, FallbackImagePath, FilenamePattern, PhotoDirectory, IReadOnlyList, PhotoMatchingService, PreFlightValidationOptions (+8 more)

### Community 23 - ".SaveCheckpointAsync"
Cohesion: 0.23
Nodes (7): IBatchCheckpointStore, CancellationToken, Task, JsonBatchCheckpointStore, CancellationToken, JsonSerializerOptions, Task

### Community 24 - "BatchPrintRequest"
Cohesion: 0.16
Nodes (16): BatchPrintRequest, AutoResumeFromCheckpoint, BatchName, BatchRunId, CardCompletionTimeout, Cards, CheckpointFilePath, Driver (+8 more)

### Community 25 - "IdpSmart31CardPrinterDriver"
Cohesion: 0.16
Nodes (13): IdpSmart31CardPrinterDriver, DisplayName, DriverId, RibbonController, StatusMonitor, TargetPrinterName, CancellationToken, IReadOnlyList (+5 more)

### Community 26 - "PrinterCapabilities"
Cohesion: 0.11
Nodes (17): SKBitmap, PrinterCapabilities, DriverName, IsSimulated, PrintableHeightMm, PrintableHeightPixels, PrintableWidthMm, PrintableWidthPixels (+9 more)

### Community 27 - "PreFlightValidationReport"
Cohesion: 0.09
Nodes (21): PreFlightErrorCodes, PreFlightValidationBlockedException, Report, PreFlightValidationReport, DuplicateIdentifiers, ErrorCount, Errors, HasErrors (+13 more)

### Community 28 - "SKPaint"
Cohesion: 0.21
Nodes (8): PhotoRenderer, IReadOnlyDictionary, SKBitmap, SKCanvas, SKRect, Stream, SKEncodedOrigin, SKPaint

### Community 29 - "LayerRegistry"
Cohesion: 0.10
Nodes (17): ArgumentException, ILayerRegistry, IReadOnlyDictionary, Type, LayerJsonConverter, ConcurrentDictionary, JsonSerializerOptions, Type (+9 more)

### Community 30 - "BarcodeValidator"
Cohesion: 0.34
Nodes (4): BarcodeValidator, Regex, BarcodeValidationResult, IBarcodeValidator

### Community 31 - ".GoldenImage_FixedTemplateAndData_MatchesReferencePixelForPixel"
Cohesion: 0.15
Nodes (13): ImageDiffResult, DiffBitmap, IsMatch, MaxChannelDelta, MismatchPercentage, MismatchPixels, TotalPixels, ImageDiffService (+5 more)

### Community 32 - "BatchCardItem"
Cohesion: 0.12
Nodes (17): BatchCardItem, BackBitmap, BackPixelBuffer, BatchIndex, Format, FrontBitmap, FrontPixelBuffer, Label (+9 more)

### Community 33 - ".Open"
Cohesion: 0.31
Nodes (8): Harness, InlineTextEditorUiTests, Action, Fact, Func, Task, Window, Border

### Community 34 - "InlineTextEditor"
Cohesion: 0.11
Nodes (11): AvaloniaPropertyChangedEventArgs, InlineTextEditor, EffectiveFontSizePoints, Layer, StyleKeyOverride, Dictionary, KeyEventArgs, Type (+3 more)

### Community 35 - ".Snap"
Cohesion: 0.20
Nodes (8): LayerSnapper, SnapResult, IEnumerable, List, LayerSnapperTests, Fact, Guide, Position

### Community 37 - "MainWindowViewModel.cs"
Cohesion: 0.26
Nodes (3): BadgeForge.Core.Data.Validation, BadgeForge.Core.Data.Services, BadgeForge.Core.Data.Models

### Community 38 - ".Replace"
Cohesion: 0.08
Nodes (22): DataFieldKind, Barcode, Photo, QrCode, Text, BoundTemplate, TemplateBinder, Dictionary (+14 more)

### Community 39 - "MainWindowPrintingTests"
Cohesion: 0.32
Nodes (3): MainWindowPrintingTests, Fact, Task

### Community 40 - "IRibbonModeController"
Cohesion: 0.12
Nodes (15): DefaultRibbonModeController, ActiveMode, BlackPixelThreshold, HardwareVerificationNotes, RequiresRealHardwareVerification, ResinDensityAdjustment, IRibbonModeController, ActiveMode (+7 more)

### Community 41 - "IdpSmart31StatusMonitor"
Cohesion: 0.16
Nodes (8): IdpSmart31StatusMonitor, CardsPrintedSincePause, CurrentStatus, IsMonitoring, Func, HashSet, TimeSpan, Timer

### Community 42 - ".SubmitCardAsync"
Cohesion: 0.13
Nodes (12): CancellationToken, Task, CardPrintResult, CompletedAtUtc, ErrorMessage, JobId, OperatorPauseTriggered, SpoolerJobId (+4 more)

### Community 43 - "CardFormat"
Cohesion: 0.14
Nodes (13): CardFormat, AspectRatio, CR79, CR80, Dpi, HeightMm, HeightPixels, IsPortrait (+5 more)

### Community 44 - "StaticImageLayer"
Cohesion: 0.13
Nodes (14): IImageLayer, Adjustments, BorderRadius, CropMode, StaticImageLayer, Adjustments, Base64Data, BorderRadius (+6 more)

### Community 45 - "DesignerDataAndExportViewModelTests"
Cohesion: 0.25
Nodes (5): DesignerDataAndExportViewModelTests, Fact, Layer, Task, Vm

### Community 46 - "BatchExecutionState"
Cohesion: 0.11
Nodes (18): BatchExecutionState, Cancelled, Completed, Idle, PausedByUser, PausedForHopper, PausedOnError, Running (+10 more)

### Community 47 - "ImageAdjustmentTests"
Cohesion: 0.22
Nodes (5): ImageAdjustmentTests, Fact, SKBitmap, SKColor, Task

### Community 48 - "WindowsSpooler"
Cohesion: 0.25
Nodes (9): JobInfo1, SystemTime, WindowsSpooler, Func, BufferCall, DllImport, IntPtr, JobInfo1 (+1 more)

### Community 49 - ".Render"
Cohesion: 0.29
Nodes (7): BarcodeRenderer, IReadOnlyDictionary, SKCanvas, SKRect, BarcodeFormat, BitMatrix, SKRectI

### Community 51 - "ImageAdjustments"
Cohesion: 0.12
Nodes (14): ImageAdjustments, Brightness, Contrast, FlipHorizontal, FlipVertical, HasColorAdjustments, IsIdentityTransform, None (+6 more)

### Community 52 - ".Render"
Cohesion: 0.27
Nodes (7): TextRenderer, IReadOnlyDictionary, SKCanvas, SKFontStyleWeight, SKRect, SKTypeface, SKFont

### Community 53 - "Rect"
Cohesion: 0.24
Nodes (6): IBrush, IEnumerable, Rect, DrawingContext, FormattedText, Point

### Community 54 - "XlsxWorksheetReader"
Cohesion: 0.26
Nodes (7): XlsxWorksheetReader, HashSet, IReadOnlyList, List, Stream, XElement, ZipArchive

### Community 55 - "CardPrintJob"
Cohesion: 0.17
Nodes (13): CardPrintJob, BackPixelBuffer, BatchIndex, Format, FrontPixelBuffer, IsDuplex, JobId, Label (+5 more)

### Community 56 - "InteractiveBadgeCanvas.cs"
Cohesion: 0.22
Nodes (5): TextAlignment, Center, Left, Right, BadgeForge.App.Controls

### Community 57 - "BarcodeLayer"
Cohesion: 0.12
Nodes (15): BarcodeSymbology, Code128, Code39, Ean13, Pdf417, QrCode, UpcA, BarcodeLayer (+7 more)

### Community 59 - "PhotoCropMode"
Cohesion: 0.16
Nodes (8): ImageLayout, Height, SKRect, Width, PhotoCropMode, AspectFill, AspectFit, Stretch

### Community 60 - "Cardholder"
Cohesion: 0.14
Nodes (13): Cardholder, AdditionalFields, Department, FullName, Id, IsSelected, JobTitle, PhotoPath (+5 more)

### Community 61 - "BatchExecutionEventArgs"
Cohesion: 0.13
Nodes (15): BatchExecutionEventArgs, CurrentCardIndex, CurrentRecord, CurrentState, Message, PauseReason, PreviousState, TotalCards (+7 more)

### Community 62 - "PrinterStatus"
Cohesion: 0.13
Nodes (14): CancellationToken, Task, PrinterStatus, ActiveJobId, CardsPrintedSincePause, InputHopperCount, IsNativeStatus, IsReadyToPrint (+6 more)

### Community 63 - "PrinterCapabilityMismatchException"
Cohesion: 0.18
Nodes (11): PrinterCapabilityMismatchException, DriverCanvasHeight, DriverCanvasWidth, DriverDpiX, DriverDpiY, ExpectedCanvasHeight, ExpectedCanvasWidth, ExpectedDpi (+3 more)

### Community 65 - "MockStatusMonitor"
Cohesion: 0.20
Nodes (5): MockStatusMonitor, CurrentStatus, IsMonitoring, Func, TimeSpan

### Community 66 - "BadgeForge.App.ViewModels"
Cohesion: 0.12
Nodes (8): Application, App, Program, AppBuilder, BadgeForge.UiTests, BadgeForge.App.ViewModels, BadgeForge.App, STAThread

### Community 68 - "RosterImportService"
Cohesion: 0.27
Nodes (7): RosterFileFormat, Csv, LegacyXls, Xlsx, RosterImportService, IReadOnlyList, RosterFileFormat

### Community 69 - "ValidationIssue"
Cohesion: 0.17
Nodes (11): ValidationIssue, BatchIndex, ErrorCode, Message, RecordIdentifier, RowNumber, Severity, TargetName (+3 more)

### Community 70 - "SpoolerSnapshot"
Cohesion: 0.22
Nodes (9): SpoolerJob, IsFinished, SpoolerSnapshot, SpoolerStatusMapper, IReadOnlyList, State, InlineData, Theory (+1 more)

### Community 71 - "TemplateLayer"
Cohesion: 0.11
Nodes (17): TemplateLayer, HasDynamicTokens, Id, IsLocked, IsVisible, LayerType, Name, Opacity (+9 more)

### Community 72 - "BatchRecordCheckpoint"
Cohesion: 0.17
Nodes (12): BatchRecordCheckpoint, BatchIndex, CompletedAtUtc, CorrelatedJobName, ErrorMessage, Label, RecordId, RetryCount (+4 more)

### Community 73 - "ICardPrinterStatusMonitor"
Cohesion: 0.17
Nodes (6): ICardPrinterStatusMonitor, CurrentStatus, IsMonitoring, CancellationToken, Task, TimeSpan

### Community 74 - "PrinterState"
Cohesion: 0.17
Nodes (11): PrinterState, CoverOpen, Error, Offline, OperatorPauseRequired, OutOfCards, OutOfRibbon, PaperJam (+3 more)

### Community 75 - "ColumnMapping"
Cohesion: 0.23
Nodes (8): ColumnMapping, Mappings, Dictionary, IReadOnlyDictionary, CsvDataIngestionService, IReadOnlyList, Stream, TextReader

### Community 77 - ".GetCardBounds"
Cohesion: 0.22
Nodes (4): DragEventArgs, PointerWheelEventArgs, Size, Vector

### Community 78 - "BundledFonts"
Cohesion: 0.33
Nodes (6): BundledFonts, Dictionary, IReadOnlyList, SKFontStyleWeight, SKTypeface, SKFontStyleSlant

### Community 79 - ".Draw"
Cohesion: 0.20
Nodes (8): ImageDrawing, SKBitmap, SKCanvas, SKRect, StaticImageRenderer, SKCanvas, SKRect, SKColorFilter

### Community 80 - "TextLayer"
Cohesion: 0.14
Nodes (13): TextLayer, Alignment, ColorHex, FontSize, FontWeight, IsPureBlackKResin, IsRequired, LayerType (+5 more)

### Community 82 - "IBatchPrintEngine"
Cohesion: 0.38
Nodes (5): IBatchPrintEngine, CurrentCheckpoint, State, CancellationToken, Task

### Community 83 - ".ConfigurePrintDocument"
Cohesion: 0.38
Nodes (6): PrintDocumentHelper, SKBitmap, SupportedOSPlatform, Graphics, PrintDocument, Rectangle

### Community 84 - ".PollStatusAsync"
Cohesion: 0.33
Nodes (6): DefaultNativeIdpStatusProvider, IsAvailable, INativeIdpStatusProvider, IsAvailable, CancellationToken, Task

### Community 85 - "ICardPrinterDriver"
Cohesion: 0.17
Nodes (11): PrintCheckResult, ICardPrinterDriver, DisplayName, DriverId, RibbonController, StatusMonitor, TargetPrinterName, CancellationToken (+3 more)

### Community 86 - ".RenderCardAsync"
Cohesion: 0.31
Nodes (6): ICardRenderer, CancellationToken, IReadOnlyDictionary, IReadOnlyList, SKBitmap, Task

### Community 87 - "PhotoLayer"
Cohesion: 0.15
Nodes (11): PhotoLayer, Adjustments, BorderRadius, CropMode, FallbackImagePath, IsRequired, LayerType, SourceToken (+3 more)

### Community 88 - "CardholderRosterTests"
Cohesion: 0.31
Nodes (4): CardholderRosterTests, Fact, InvalidDataException, NotSupportedException

### Community 90 - ".WriteAllTextAsync"
Cohesion: 0.36
Nodes (4): AtomicFileWriter, CancellationToken, Task, Encoding

### Community 91 - "CardRendererTests"
Cohesion: 0.39
Nodes (3): CardRendererTests, Fact, Task

### Community 92 - "BoolToDimOpacityConverter"
Cohesion: 0.32
Nodes (4): BoolToDimOpacityConverter, CultureInfo, Type, BadgeForge.App.Converters

### Community 93 - ".FindDropTarget"
Cohesion: 0.21
Nodes (7): Button, Control, DragEventArgs, Highlight, OnPhoto, Person, Visual

### Community 94 - "LayerIconConverter"
Cohesion: 0.38
Nodes (5): LayerIconConverter, CultureInfo, Geometry, Type, IValueConverter

### Community 95 - "LayerVisibilityIconConverter"
Cohesion: 0.47
Nodes (4): LayerVisibilityIconConverter, CultureInfo, Geometry, Type

### Community 96 - "V1NoOpMigrator"
Cohesion: 0.33
Nodes (4): V1NoOpMigrator, SourceVersion, TargetVersion, JsonNode

### Community 98 - "ErrorPromptKind"
Cohesion: 0.50
Nodes (4): ErrorPromptKind, Dismiss, OutcomeUnknown, Retry

### Community 99 - "PrinterStatusChangedEventArgs"
Cohesion: 0.50
Nodes (4): PrinterStatusChangedEventArgs, CurrentStatus, PreviousStatus, EventArgs

### Community 100 - "ResumeDecision"
Cohesion: 0.67
Nodes (3): ResumeDecision, Continue, Reprint

### Community 103 - "PrinterException"
Cohesion: 0.32
Nodes (7): OperatorPauseRequiredException, CardsPrintedInBatch, MaxCardsBeforePause, PrinterException, PrinterHardwareFaultException, FaultCode, Exception

### Community 104 - "BatchRecordStatus"
Cohesion: 0.29
Nodes (6): BatchRecordStatus, Failed, Pending, Printing, Skipped, Success

### Community 105 - ".Draw"
Cohesion: 0.33
Nodes (4): PhotoPlaceholder, SKCanvas, SKColor, SKRect

### Community 109 - "HeadlessTestApp"
Cohesion: 0.33
Nodes (5): HeadlessTestApp, App, AppBuilder, Lazy, HeadlessUnitTestSession

## Knowledge Gaps
- **572 isolated node(s):** `net10.0`, `Avalonia (12.1.2)`, `Avalonia.Desktop (12.1.2)`, `Avalonia.Themes.Fluent (12.1.2)`, `Avalonia.Fonts.Inter (12.1.2)` (+567 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 814 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **11 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `MainWindowViewModel` connect `MainWindowViewModel` to `.RenderAsync`, `InteractiveBadgeCanvas`, `.RequestLivePreviewUpdate`, `MainWindow`, `PersonItem`, `TemplateDefinition`, `BadgeRecord`, `MainWindowViewModelTests`, `Task`, `.AddPerson`, `BatchPrintEngine`, `CardRenderer`, `BadgeForge.Core.Templates.Layers`, `PersonFieldCell`, `ImageSourceLoader`, `.ValidateBatch`, `.Open`, `.Snap`, `MainWindowViewModel.cs`, `.Replace`, `MainWindowPrintingTests`, `CardFormat`, `StaticImageLayer`, `DesignerDataAndExportViewModelTests`, `BatchExecutionState`, `ImageAdjustmentTests`, `ImageAdjustments`, `Rect`, `InteractiveBadgeCanvas.cs`, `BarcodeLayer`, `.ReplaceSelectedLayer`, `PhotoCropMode`, `BadgeForge.App.ViewModels`, `RosterImportService`, `TemplateLayer`, `TextLayer`, `ICardPrinterDriver`, `PhotoLayer`, `.OnPointerMoved`, `BatchCardRenderServiceTests.cs`, `ErrorPromptKind`?**
  _High betweenness centrality (0.568) - this node is a cross-community bridge._
- **Why does `BatchPrintEngine` connect `BatchPrintEngine` to `MainWindowViewModel`, `BatchCardItem`, `ResumeDecision`, `MockCardPrinterDriver`, `SpoolerCorrelationResult`, `BatchExecutionState`, `BadgeForge.Core.Printing.Models`, `IBatchPrintEngine`, `.SaveCheckpointAsync`, `BatchPrintRequest`?**
  _High betweenness centrality (0.103) - this node is a cross-community bridge._
- **Why does `ICardPrinterDriver` connect `ICardPrinterDriver` to `MainWindowViewModel`, `.RenderAsync`, `ICardPrinterDriver.cs`, `IRibbonModeController`, `ICardPrinterStatusMonitor`, `Task`, `MockCardPrinterDriver`, `CardRenderer`, `.RenderCardAsync`, `BatchPrintRequest`, `IdpSmart31CardPrinterDriver`?**
  _High betweenness centrality (0.090) - this node is a cross-community bridge._
- **Are the 5 inferred relationships involving `MainWindowViewModel` (e.g. with `.ViewModel_AddImageFromFile_EmbedsImageAndSizesFrameToAspect()` and `.ViewModel_ImageEdits_UpdateTemplateLayer()`) actually correct?**
  _`MainWindowViewModel` has 5 INFERRED edges - model-reasoned connections that need verification._
- **Are the 18 inferred relationships involving `TemplateDefinition` (e.g. with `.BarcodeTooLongForItsLayer_StaysInsideTheLayerInPreviews_AndIsRejectedForPrinting()` and `.CardSizeThePrinterDoesNotTake_StillFailsLoudlyWhenPrinting()`) actually correct?**
  _`TemplateDefinition` has 18 INFERRED edges - model-reasoned connections that need verification._
- **Are the 14 inferred relationships involving `TextLayer` (e.g. with `.CreateTemplate()` and `.PureBlackText_RendersStrictlyNonAntiAliased_ForKResinPanel()`) actually correct?**
  _`TextLayer` has 14 INFERRED edges - model-reasoned connections that need verification._
- **What connects `net10.0`, `Avalonia (12.1.2)`, `Avalonia.Desktop (12.1.2)` to the rest of the system?**
  _572 weakly-connected nodes found - possible documentation gaps or missing edges._