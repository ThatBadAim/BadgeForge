# Graph Report - Printer Application  (2026-09-16)

## Corpus Check
- 149 files · ~96,937 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 2554 nodes · 6145 edges · 118 communities (106 shown, 11 thin omitted)
- Extraction: 89% EXTRACTED · 11% INFERRED · 0% AMBIGUOUS · INFERRED: 680 edges (avg confidence: 0.83)
- Token cost: 0 input · 0 output

## Community Hubs (Navigation)
- MainWindowViewModel
- .RenderAsync
- InteractiveBadgeCanvas
- TemplateLayer
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
- .ResumeAsync
- IdpSmart31CardPrinterDriver
- PrinterCapabilities
- PreFlightValidationReport
- PhotoMatchingOptions
- LayerRegistry
- .Render
- .GoldenImage_FixedTemplateAndData_MatchesReferencePixelForPixel
- BatchCardItem
- .AddPerson
- InlineTextEditor
- .OpenTwoColourCard
- .RunSafelyAsync
- BadgeForge.Core.Data.Models
- .Replace
- MainWindowPrintingTests
- IRibbonModeController
- IdpSmart31StatusMonitor
- CardPrintJob
- CardFormat
- PhotoLayer
- PeopleColumnOrderTests
- BatchExecutionState
- ImageAdjustmentTests
- WindowsSpooler
- PrintPathFidelityTests
- CsvDataIngestionService
- ImageAdjustments
- BarcodeValidator
- SKPaint
- XlsxWorksheetReader
- PreFlightValidationTests
- BadgeForge.App.ViewModels
- .ExtractTokensFromString
- BatchCardRenderServiceTests.cs
- .ComputeContentRect
- Cardholder
- BatchExecutionEventArgs
- PrinterStatus
- PrinterCapabilityMismatchException
- .ShowDialogAsync
- MockStatusMonitor
- App
- .HitTestImageLayer
- TemplateSerializationTests.cs
- ValidationIssue
- SpoolerSnapshot
- RosterTable
- BarcodeLayer
- ICardPrinterStatusMonitor
- PrinterState
- .Build
- .OnDesignerKeyDown
- .GetCardBounds
- .PromptAndApplyNewTemplate
- .CustomLayer_CanBeRegisteredAndDeserialized_WithoutTouchingCoreCode
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
- .CreateDefaultTemplate
- .Draw
- V1NoOpMigrator
- .OnColumnHeaderPointerPressed
- ErrorPromptKind
- MockCardPrinterDriver
- ResumeDecision
- Harness
- DesignerEditingUiTests
- Rect
- .CreateText
- .RunOnUiThread
- publish-windows.sh
- .OnPropertyChanged
- TextLayer
- .AddImageLayerFromFileAsync
- .RenderCardAsync
- .Open
- CardOutcome
- HeadlessTestApp
- TextAlignment
- .SelectingALayer_BringsTheLayerTabBackFromTheDataTab
- WaitOutcome

## God Nodes (most connected - your core abstractions)
1. `MainWindowViewModel` - 448 edges
2. `MainWindow` - 123 edges
3. `InteractiveBadgeCanvas` - 82 edges
4. `TemplateDefinition` - 77 edges
5. `TextLayer` - 72 edges
6. `BatchPrintEngine` - 70 edges
7. `MockCardPrinterDriver` - 67 edges
8. `TemplateLayer` - 63 edges
9. `PersonItem` - 51 edges
10. `BadgeForge.Core.Templates.Layers` - 47 edges

## Surprising Connections (you probably didn't know these)
- `InlineTextEditor` --references--> `TextLayer`  [EXTRACTED]
  BadgeForge.App/Controls/InlineTextEditor.cs → BadgeForge.Core.Templates/Layers/TextLayer.cs
- `InteractiveBadgeCanvas` --references--> `CardFormat`  [EXTRACTED]
  BadgeForge.App/Controls/InteractiveBadgeCanvas.cs → BadgeForge.Core.Printing/Models/CardFormat.cs
- `InteractiveBadgeCanvas` --references--> `TemplateLayer`  [EXTRACTED]
  BadgeForge.App/Controls/InteractiveBadgeCanvas.cs → BadgeForge.Core.Templates/Layers/TemplateLayer.cs
- `Harness` --references--> `InteractiveBadgeCanvas`  [EXTRACTED]
  BadgeForge.UiTests/DesignerEditingUiTests.cs → BadgeForge.App/Controls/InteractiveBadgeCanvas.cs
- `Harness` --references--> `InteractiveBadgeCanvas`  [EXTRACTED]
  BadgeForge.UiTests/InlineTextEditorUiTests.cs → BadgeForge.App/Controls/InteractiveBadgeCanvas.cs

## Import Cycles
- None detected.

## Communities (118 total, 11 thin omitted)

### Community 0 - "MainWindowViewModel"
Cohesion: 0.01
Nodes (141): ActiveDriver, ActiveFormat, AllPeopleIncluded, AvailableFormats, AvailablePrinters, BarcodeContent, BarcodeIncludeText, BarcodePrintsAsPureBlack (+133 more)

### Community 1 - ".RenderAsync"
Cohesion: 0.05
Nodes (53): ArgumentOutOfRangeException, CancellationTokenSource, Task, BatchCardRenderService, IBatchCardRenderService, CancellationToken, CardRenderer, IEnumerable (+45 more)

### Community 2 - "InteractiveBadgeCanvas"
Cohesion: 0.08
Nodes (18): InteractiveBadgeCanvas, Bitmap, Format, InlineEditor, IsInPointerGesture, ViewModel, Zoom, Bitmap (+10 more)

### Community 3 - "TemplateLayer"
Cohesion: 0.07
Nodes (17): EventArgs, Label, TemplateLayer, HasDynamicTokens, Id, IsLocked, IsVisible, LayerType (+9 more)

### Community 4 - "MainWindow"
Cohesion: 0.08
Nodes (3): MainWindow, ViewModel, RoutedEventArgs

### Community 5 - "PersonItem"
Cohesion: 0.08
Nodes (20): PersonItem, Cells, ErrorMessage, HasPhoto, HasPrintStatus, IdentifierText, IsBlank, IsIncluded (+12 more)

### Community 6 - "TemplateDefinition"
Cohesion: 0.10
Nodes (21): TemplateDefinition, CreatedAtUtc, Id, Layers, Metadata, ModifiedAtUtc, Name, SchemaVersion (+13 more)

### Community 7 - "BadgeRecord"
Cohesion: 0.12
Nodes (15): BadgeRecord, BatchIndex, Fields, ResolvedPhotoPath, RowNumber, Dictionary, RosterService, IEnumerable (+7 more)

### Community 8 - "TextLayoutEngine"
Cohesion: 0.07
Nodes (27): BundledFonts, Dictionary, IReadOnlyList, SKFontStyleWeight, SKTypeface, TextLayoutEngine, TextLayoutResult, Func (+19 more)

### Community 9 - "MainWindowViewModelTests"
Cohesion: 0.11
Nodes (5): Dictionary, MainWindowViewModelTests, Fact, SKColor, Task

### Community 10 - "Task"
Cohesion: 0.08
Nodes (13): ConfirmPrintWithWarningsAsync, PreviewRequest, PrintCheckResult, PrintJobInfo, CancellationToken, CancellationTokenSource, HashSet, IReadOnlyDictionary (+5 more)

### Community 11 - "BatchPrintRequest"
Cohesion: 0.11
Nodes (26): ArgumentException, IReadOnlyList, BatchPrintRequest, AutoResumeFromCheckpoint, BatchName, BatchRunId, CardCompletionTimeout, Cards (+18 more)

### Community 12 - "ImportSummaryViewModel"
Cohesion: 0.06
Nodes (38): ImportChoice, Append, Cancel, Replace, ImportColumnSummary, ImportPreviewCell, IsEmpty, ImportPreviewRow (+30 more)

### Community 13 - "SpoolerCorrelationResult"
Cohesion: 0.09
Nodes (21): ISpoolerJobCorrelator, SpoolerCorrelationResult, Elapsed, ErrorMessage, ExpectedJobName, IsCorrelated, MatchedJobName, RetriesAttempted (+13 more)

### Community 14 - "BatchPrintEngine"
Cohesion: 0.19
Nodes (12): BatchPrintEngine, CurrentCheckpoint, State, Action, CancellationToken, CancellationTokenSource, Task, TimeSpan (+4 more)

### Community 15 - "CardRenderer"
Cohesion: 0.18
Nodes (14): CardRenderer, DrawMissingPhotoPlaceholders, StrictLayerRendering, CancellationToken, IReadOnlyDictionary, IReadOnlyList, SKBitmap, SKCanvas (+6 more)

### Community 16 - "BadgeForge.Core.Templates.Layers"
Cohesion: 0.14
Nodes (5): BadgeForge.Core.Templates.Layers, BadgeForge.Core.Templates.Enums, BadgeForge.Core.Rendering.Elements, BadgeForge.Core.Templates.Tokens, BadgeForge.Core.Templates.Models

### Community 17 - "BadgeForge.Core.Printing.Models"
Cohesion: 0.17
Nodes (7): BadgeForge.Core.Printing.Drivers, BadgeForge.Core.Printing.Interfaces, BadgeForge.Core.Printing.Models, BadgeForge.Tests, BadgeForge.Core.Rendering, BadgeForge.Core.Printing.Exceptions, BadgeForge.Core.Printing.Batch

### Community 18 - "TemplateMigrationPipeline"
Cohesion: 0.12
Nodes (14): ITemplateMigrator, SourceVersion, TargetVersion, JsonNode, TemplateMigrationPipeline, ConcurrentDictionary, From, JsonNode (+6 more)

### Community 19 - "PeopleColumnHeaderUiTests"
Cohesion: 0.21
Nodes (12): PeopleFieldColumn, Harness, PeopleColumnHeaderUiTests, Border, Control, Fact, ListBox, Point (+4 more)

### Community 20 - "ImageSourceLoader"
Cohesion: 0.13
Nodes (13): ImageSourceLoader, SupportedExtensions, ImportedImage, Lease, Dictionary, Func, IDisposable, IReadOnlyList (+5 more)

### Community 21 - "BadgeForge.App"
Cohesion: 0.06
Nodes (44): BadgeForge.App, net10.0, SkiaSharp (4.152.0), SkiaSharp.NativeAssets.Linux (4.152.0), Microsoft.NET.Sdk, BadgeForge.Core.Data, net10.0, Microsoft.NET.Sdk (+36 more)

### Community 22 - ".ValidateBatch"
Cohesion: 0.21
Nodes (10): PhotoMatchingService, PreFlightValidationOptions, AllowMissingPhotoAsWarning, CheckDuplicates, EnforceRequirements, PrimaryKeyToken, PreFlightValidationService, Dictionary (+2 more)

### Community 23 - "BatchCheckpoint"
Cohesion: 0.07
Nodes (28): BatchCheckpoint, BatchName, BatchRunId, CompletedCards, FailedCards, IsComplete, LastUpdatedAtUtc, PendingCards (+20 more)

### Community 24 - ".ResumeAsync"
Cohesion: 0.22
Nodes (8): JsonBatchCheckpointStore, CancellationToken, JsonSerializerOptions, Task, BatchPrintEngineTests, Fact, List, Task

### Community 25 - "IdpSmart31CardPrinterDriver"
Cohesion: 0.13
Nodes (15): IdpSmart31CardPrinterDriver, DisplayName, DriverId, RibbonController, StatusMonitor, TargetPrinterName, CancellationToken, IReadOnlyList (+7 more)

### Community 26 - "PrinterCapabilities"
Cohesion: 0.11
Nodes (19): PrinterCapabilities, DimensionTolerancePixels, DriverName, IsSimulated, PrintableHeightMm, PrintableHeightPixels, PrintableWidthMm, PrintableWidthPixels (+11 more)

### Community 27 - "PreFlightValidationReport"
Cohesion: 0.12
Nodes (18): PreFlightErrorCodes, PreFlightValidationBlockedException, Report, PreFlightValidationReport, DuplicateIdentifiers, ErrorCount, Errors, HasErrors (+10 more)

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
Cohesion: 0.15
Nodes (13): ImageDiffResult, DiffBitmap, IsMatch, MaxChannelDelta, MismatchPercentage, MismatchPixels, TotalPixels, ImageDiffService (+5 more)

### Community 32 - "BatchCardItem"
Cohesion: 0.14
Nodes (16): BatchCardItem, BackBitmap, BackPixelBuffer, BatchIndex, Format, FrontBitmap, FrontPixelBuffer, Label (+8 more)

### Community 34 - "InlineTextEditor"
Cohesion: 0.11
Nodes (12): AvaloniaPropertyChangedEventArgs, InlineTextEditor, EffectiveFontSizePoints, Layer, StyleKeyOverride, Dictionary, KeyEventArgs, Type (+4 more)

### Community 35 - ".OpenTwoColourCard"
Cohesion: 0.29
Nodes (7): ColorDropperUiTests, AvaloniaBitmap, Color, Fact, Task, Window, Canvas

### Community 37 - "BadgeForge.Core.Data.Models"
Cohesion: 0.18
Nodes (3): BadgeForge.Core.Data.Validation, BadgeForge.Core.Data.Services, BadgeForge.Core.Data.Models

### Community 38 - ".Replace"
Cohesion: 0.08
Nodes (22): DataFieldKind, Barcode, Photo, QrCode, Text, BoundTemplate, TemplateBinder, Dictionary (+14 more)

### Community 39 - "MainWindowPrintingTests"
Cohesion: 0.35
Nodes (3): MainWindowPrintingTests, Fact, Task

### Community 40 - "IRibbonModeController"
Cohesion: 0.12
Nodes (15): DefaultRibbonModeController, ActiveMode, BlackPixelThreshold, HardwareVerificationNotes, RequiresRealHardwareVerification, ResinDensityAdjustment, IRibbonModeController, ActiveMode (+7 more)

### Community 41 - "IdpSmart31StatusMonitor"
Cohesion: 0.13
Nodes (9): IdpSmart31StatusMonitor, CardsPrintedSincePause, CurrentStatus, IsMonitoring, Func, HashSet, IReadOnlyCollection, TimeSpan (+1 more)

### Community 42 - "CardPrintJob"
Cohesion: 0.09
Nodes (24): CancellationToken, Task, CardPrintJob, BackPixelBuffer, BatchIndex, Format, FrontPixelBuffer, IsDuplex (+16 more)

### Community 43 - "CardFormat"
Cohesion: 0.11
Nodes (15): CardFormat, AspectRatio, CR79, CR80, Dpi, HeightMm, HeightPixels, IsPortrait (+7 more)

### Community 44 - "PhotoLayer"
Cohesion: 0.08
Nodes (26): PhotoCropMode, AspectFill, AspectFit, Stretch, IImageLayer, Adjustments, BorderRadius, CropMode (+18 more)

### Community 45 - "PeopleColumnOrderTests"
Cohesion: 0.16
Nodes (10): PeopleColumnLayout, IsCustomised, From, IReadOnlyList, List, To, PeopleColumnOrderTests, Dictionary (+2 more)

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

### Community 50 - "CsvDataIngestionService"
Cohesion: 0.41
Nodes (5): RosterRow, CsvDataIngestionService, IReadOnlyList, Stream, TextReader

### Community 51 - "ImageAdjustments"
Cohesion: 0.12
Nodes (14): ImageAdjustments, Brightness, Contrast, FlipHorizontal, FlipVertical, HasColorAdjustments, IsIdentityTransform, None (+6 more)

### Community 52 - "BarcodeValidator"
Cohesion: 0.25
Nodes (6): BarcodeValidator, Regex, BarcodeValidationResult, IBarcodeValidator, PreFlightPhotoAndCode39Tests, Fact

### Community 53 - "SKPaint"
Cohesion: 0.21
Nodes (8): PhotoRenderer, IReadOnlyDictionary, SKBitmap, SKCanvas, SKRect, Stream, SKEncodedOrigin, SKPaint

### Community 54 - "XlsxWorksheetReader"
Cohesion: 0.26
Nodes (7): XlsxWorksheetReader, HashSet, IReadOnlyList, List, Stream, XElement, ZipArchive

### Community 55 - "PreFlightValidationTests"
Cohesion: 0.33
Nodes (3): PreFlightValidationTests, Fact, Task

### Community 56 - "BadgeForge.App.ViewModels"
Cohesion: 0.26
Nodes (5): BadgeForge.UiTests, BadgeForge.App.ViewModels, BadgeForge.App.Views, BadgeForge.App.Controls, BadgeForge.App

### Community 57 - ".ExtractTokensFromString"
Cohesion: 0.22
Nodes (4): IEnumerable, IEnumerable, IEnumerable, IEnumerable

### Community 59 - ".ComputeContentRect"
Cohesion: 0.39
Nodes (4): ImageLayout, Height, SKRect, Width

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

### Community 68 - "TemplateSerializationTests.cs"
Cohesion: 0.27
Nodes (3): BadgeForge.Core.Templates.Migration, BadgeForge.Core.Templates.Storage, BadgeForge.Core.Templates.Registry

### Community 69 - "ValidationIssue"
Cohesion: 0.17
Nodes (11): ValidationIssue, BatchIndex, ErrorCode, Message, RecordIdentifier, RowNumber, Severity, TargetName (+3 more)

### Community 70 - "SpoolerSnapshot"
Cohesion: 0.22
Nodes (9): SpoolerJob, IsFinished, SpoolerSnapshot, SpoolerStatusMapper, IReadOnlyList, State, InlineData, Theory (+1 more)

### Community 71 - "RosterTable"
Cohesion: 0.13
Nodes (15): RosterFileFormat, Csv, LegacyXls, Xlsx, RosterTable, ColumnCount, Columns, Format (+7 more)

### Community 72 - "BarcodeLayer"
Cohesion: 0.14
Nodes (14): BarcodeSymbology, Code128, Code39, Ean13, Pdf417, QrCode, UpcA, BarcodeLayer (+6 more)

### Community 73 - "ICardPrinterStatusMonitor"
Cohesion: 0.17
Nodes (6): ICardPrinterStatusMonitor, CurrentStatus, IsMonitoring, CancellationToken, Task, TimeSpan

### Community 74 - "PrinterState"
Cohesion: 0.17
Nodes (11): PrinterState, CoverOpen, Error, Offline, OperatorPauseRequired, OutOfCards, OutOfRibbon, PaperJam (+3 more)

### Community 75 - ".Build"
Cohesion: 0.21
Nodes (7): ColumnMapping, Mappings, Dictionary, IReadOnlyDictionary, RosterTableBuilder, IReadOnlyList, List

### Community 76 - ".OnDesignerKeyDown"
Cohesion: 0.15
Nodes (5): KeyEventArgs, ListBox, TextBox, ComboBox, Slider

### Community 77 - ".GetCardBounds"
Cohesion: 0.22
Nodes (5): Color, PointerEventArgs, PointerWheelEventArgs, Size, Vector

### Community 79 - ".CustomLayer_CanBeRegisteredAndDeserialized_WithoutTouchingCoreCode"
Cohesion: 0.17
Nodes (11): SignatureCaptureLayer, LayerType, PenColorHex, SignatureToken, TemplateSerializationTests, TestV1ToV2Migrator, SourceVersion, TargetVersion (+3 more)

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

### Community 87 - "TemplateHistory"
Cohesion: 0.08
Nodes (21): LayerSnapper, SnapResult, IEnumerable, List, TemplateHistory, CanRedo, CanUndo, RedoLabel (+13 more)

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

### Community 95 - ".Draw"
Cohesion: 0.33
Nodes (4): PhotoPlaceholder, SKCanvas, SKColor, SKRect

### Community 96 - "V1NoOpMigrator"
Cohesion: 0.33
Nodes (4): V1NoOpMigrator, SourceVersion, TargetVersion, JsonNode

### Community 98 - "ErrorPromptKind"
Cohesion: 0.50
Nodes (4): ErrorPromptKind, Dismiss, OutcomeUnknown, Retry

### Community 99 - "MockCardPrinterDriver"
Cohesion: 0.13
Nodes (22): MockCardPrinterDriver, AbortedCardCount, DisplayName, DriverId, HoldCardInPrinterAtBatchIndex, MockStatus, ProbedCapabilities, RibbonController (+14 more)

### Community 100 - "ResumeDecision"
Cohesion: 0.40
Nodes (5): ResumeDecision, AbortCard, Continue, FinishCard, Reprint

### Community 101 - "Harness"
Cohesion: 0.16
Nodes (16): PersonFieldCell, Key, Label, Owner, Token, Value, Harness, PeopleListUiTests (+8 more)

### Community 102 - "DesignerEditingUiTests"
Cohesion: 0.30
Nodes (7): DesignerEditingUiTests, Harness, Fact, ListBox, PhysicalKey, RawInputModifiers, Task

### Community 103 - "Rect"
Cohesion: 0.25
Nodes (3): Point, Rect, DrawingContext

### Community 105 - ".RunOnUiThread"
Cohesion: 0.31
Nodes (8): Harness, InlineTextEditorUiTests, Action, Border, Fact, Func, Task, Window

### Community 107 - ".OnPropertyChanged"
Cohesion: 0.07
Nodes (15): ColumnMappingItem, CsvColumn, TemplateToken, AvaloniaBitmap, ObservableCollection, Task, ViewModelBase, BatchRecordStatus (+7 more)

### Community 109 - "TextLayer"
Cohesion: 0.13
Nodes (16): TextLayer, Alignment, ColorHex, FontSize, IsPureBlackKResin, IsRequired, LayerType, LineHeight (+8 more)

### Community 111 - ".AddImageLayerFromFileAsync"
Cohesion: 0.22
Nodes (6): Rect, CropBoxTests, Fact, InlineData, Task, Theory

### Community 113 - ".RenderCardAsync"
Cohesion: 0.31
Nodes (6): ICardRenderer, CancellationToken, IReadOnlyDictionary, IReadOnlyList, SKBitmap, Task

### Community 115 - ".Open"
Cohesion: 0.42
Nodes (5): Harness, PrintStopUiTests, Fact, Func, Task

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
- **631 isolated node(s):** `net10.0`, `Avalonia (12.1.2)`, `Avalonia.Desktop (12.1.2)`, `Avalonia.Themes.Fluent (12.1.2)`, `Avalonia.Fonts.Inter (12.1.2)` (+626 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 909 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **11 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `MainWindowViewModel` connect `MainWindowViewModel` to `.RenderAsync`, `InteractiveBadgeCanvas`, `TemplateLayer`, `MainWindow`, `PersonItem`, `TemplateDefinition`, `BadgeRecord`, `MainWindowViewModelTests`, `Task`, `BatchPrintEngine`, `CardRenderer`, `BadgeForge.Core.Templates.Layers`, `PeopleColumnHeaderUiTests`, `ImageSourceLoader`, `.ValidateBatch`, `.AddPerson`, `.OpenTwoColourCard`, `BadgeForge.Core.Data.Models`, `.Replace`, `MainWindowPrintingTests`, `CardFormat`, `PhotoLayer`, `PeopleColumnOrderTests`, `BatchExecutionState`, `ImageAdjustmentTests`, `ImageAdjustments`, `BatchCardRenderServiceTests.cs`, `.HitTestImageLayer`, `BarcodeLayer`, `ICardPrinterDriver`, `.ReplaceSelectedLayer`, `TemplateHistory`, `RosterImportService`, `.CreateDefaultTemplate`, `ErrorPromptKind`, `Harness`, `DesignerEditingUiTests`, `Rect`, `.RunOnUiThread`, `.OnPropertyChanged`, `TextLayer`, `.AddImageLayerFromFileAsync`, `.Open`, `TextAlignment`?**
  _High betweenness centrality (0.583) - this node is a cross-community bridge._
- **Why does `MainWindow` connect `MainWindow` to `.ShowDialogAsync`, `.OnColumnHeaderPointerPressed`, `App`, `MainWindowViewModel`, `.RunSafelyAsync`, `Harness`, `DesignerEditingUiTests`, `.OnDesignerKeyDown`, `.PromptAndApplyNewTemplate`, `PeopleColumnHeaderUiTests`, `.Open`, `BadgeForge.App.ViewModels`, `.EndColumnDrag`, `.SelectingALayer_BringsTheLayerTabBackFromTheDataTab`, `.FindDropTarget`?**
  _High betweenness centrality (0.112) - this node is a cross-community bridge._
- **Why does `BatchPrintEngine` connect `BatchPrintEngine` to `MainWindowViewModel`, `MockCardPrinterDriver`, `ResumeDecision`, `BatchPrintRequest`, `SpoolerCorrelationResult`, `BatchExecutionState`, `BadgeForge.Core.Printing.Models`, `IBatchPrintEngine`, `CardOutcome`, `BatchCheckpoint`, `.ResumeAsync`, `WaitOutcome`, `BatchExecutionEventArgs`?**
  _High betweenness centrality (0.104) - this node is a cross-community bridge._
- **Are the 9 inferred relationships involving `MainWindowViewModel` (e.g. with `.CropBoxDrag_ChangesTheFrame_ButNotWhereThePictureSitsOnTheCard()` and `.CropBoxDrag_IsOneUndoStep_AndUndoRestoresTheFrameAndThePicture()`) actually correct?**
  _`MainWindowViewModel` has 9 INFERRED edges - model-reasoned connections that need verification._
- **Are the 2 inferred relationships involving `MainWindow` (e.g. with `.OnFrameworkInitializationCompleted()` and `.SelectingALayer_BringsTheLayerTabBackFromTheDataTab()`) actually correct?**
  _`MainWindow` has 2 INFERRED edges - model-reasoned connections that need verification._
- **Are the 20 inferred relationships involving `TemplateDefinition` (e.g. with `.BarcodeTooLongForItsLayer_StaysInsideTheLayerInPreviews_AndIsRejectedForPrinting()` and `.CardSizeThePrinterDoesNotTake_StillFailsLoudlyWhenPrinting()`) actually correct?**
  _`TemplateDefinition` has 20 INFERRED edges - model-reasoned connections that need verification._
- **Are the 14 inferred relationships involving `TextLayer` (e.g. with `.CreateTemplate()` and `.PureBlackText_RendersStrictlyNonAntiAliased_ForKResinPanel()`) actually correct?**
  _`TextLayer` has 14 INFERRED edges - model-reasoned connections that need verification._