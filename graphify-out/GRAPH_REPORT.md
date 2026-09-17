# Graph Report - Printer Application  (2026-09-17)

## Corpus Check
- cluster-only mode — file stats not available

## Summary
- 2681 nodes · 5971 edges · 161 communities (122 shown, 38 thin omitted)
- Extraction: 91% EXTRACTED · 9% INFERRED · 0% AMBIGUOUS · INFERRED: 561 edges (avg confidence: 0.83)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `03eb9f45`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- MainWindowViewModel
- InteractiveBadgeCanvas
- RosterTable
- BadgeRecord
- ImportSummaryViewModel
- MainWindow
- DefaultRibbonModeController
- .RequestLivePreviewUpdate
- BatchPrintEngine
- PersonItem
- BadgeForge.Core.Templates.Layers
- IReadOnlyList
- TemplateDefinition
- PrinterCapabilities
- BatchPrintRequest
- BadgeForge.Core.Printing.Models
- TemplateLayer
- MainWindowViewModel
- .Replace
- TextLayer
- .RunPrintJobAsync
- IdpSmart31StatusMonitor
- MainWindowViewModelTests
- MockCardPrinterDriver
- IdpSmart31CardPrinterDriver
- BadgeForge.Core.Data.Models
- TemplateMigrationPipeline
- BatchCheckpoint
- ImageSourceLoader
- DesignerDataAndExportViewModelTests
- CardRenderer
- MainWindowViewModel
- CardPrintJob
- .ComputeContentRect
- DesignerEditingUiTests
- ImageAdjustmentTests
- Task
- TemplateHistory
- BatchCardItem
- PeopleColumnHeaderUiTests
- .RunAsync
- WindowsSpooler
- LayerRegistry
- CardFormat
- .RunSafelyAsync
- .ValidateBatch
- TextLayoutEngine
- .ShowDialogAsync
- Cardholder
- .RunOnUiThread
- Harness
- RenderedCard
- SpoolerCorrelationResult
- .OpenTwoColourCard
- BadgeForge.App.ViewModels
- PeopleColumnLayout
- XlsxWorksheetReader
- PreFlightValidationTests
- .Render
- .RenderAll
- PreFlightValidationReport
- BatchExecutionEventArgs
- SKPaint
- PhotoLayer
- ImageAdjustments
- .OnDesignerKeyDown
- LayerVisibilityIconConverter
- MockStatusMonitor
- ICardPrinterDriver
- .ExtractTokensFromString
- PeopleColumnOrderTests
- .CorrelateJobAsync
- TemplateLayer
- .Render
- .RenderAsync
- .FindDropTarget
- ValidationIssue
- IBatchPrintEngine
- SpoolerSnapshot
- PrinterStatus
- .ExportAsync
- TextLayoutEngineTests
- BadgeForge.sln
- .ResolvePhotoPath
- BatchRecordCheckpoint
- ICardPrinterStatusMonitor
- CardPrintResult
- PrinterState
- .GoldenImage_FixedTemplateAndData_MatchesReferencePixelForPixel
- Program
- .Snap
- BundledFonts
- .Draw
- PdfCardDocumentWriter
- LayerJsonConverter
- App
- BatchExecutionSummary
- .SubmitCardAsync
- .RenderCardAsync
- StaticImageLayer
- CardholderRosterTests
- LayerSnapper
- .EndColumnDrag
- CardholderMapper
- ImageDiffResult
- CardRendererTests
- .Open
- .FocusPersonField
- ColumnMapping
- PhotoMatchingOptions
- BarcodeLayer
- HeadlessTestApp
- LayerIconConverter
- ViewModelBase
- CardOutcome
- .Draw
- BadgeForge.Core.Templates.Migration
- V1NoOpMigrator
- ResumeDecision
- BatchRenderOptions
- TextOverflowMode
- .SelectingALayer_BringsTheLayerTabBackFromTheDataTab
- ErrorPromptKind
- PrinterStatusChangedEventArgs
- SynchronousProgress
- WaitOutcome
- .OnColumnHeaderPointerPressed
- Make EXEs
- publish-windows.sh
- Bitmap
- Color
- Point
- Action
- Button
- ListBox
- RoutedEventArgs
- TextBox
- AvaloniaBitmap
- CancellationTokenSource
- HashSet
- ImageAdjustments
- ObservableCollection
- PhotoCropMode
- From
- To
- DateTime
- Stream
- DateTimeOffset
- Regex
- SKRect
- IProgress
- InlineData
- Theory
- SKColor
- App
- AppBuilder
- Lazy
- PhotoMatchingService
- PreFlightValidationService
- ValidationIssue

## God Nodes (most connected - your core abstractions)
1. `MainWindowViewModel` - 311 edges
2. `MainWindow` - 123 edges
3. `InteractiveBadgeCanvas` - 86 edges
4. `MainWindowViewModel` - 76 edges
5. `BatchPrintEngine` - 69 edges
6. `MockCardPrinterDriver` - 67 edges
7. `TemplateDefinition` - 64 edges
8. `MainWindowViewModel` - 61 edges
9. `TextLayer` - 53 edges
10. `BatchPrintRequest` - 44 edges

## Surprising Connections (you probably didn't know these)
- `MainWindowViewModel` --references--> `PhotoMatchingService`  [EXTRACTED]
  BadgeForge.App/ViewModels/MainWindowViewModel.cs → BadgeForge.Core.Data/Services/PhotoMatchingService.cs
- `MainWindowViewModel` --references--> `PreFlightValidationService`  [EXTRACTED]
  BadgeForge.App/ViewModels/MainWindowViewModel.cs → BadgeForge.Core.Data/Validation/PreFlightValidationService.cs
- `Harness` --references--> `InteractiveBadgeCanvas`  [EXTRACTED]
  BadgeForge.UiTests/DesignerEditingUiTests.cs → BadgeForge.App/Controls/InteractiveBadgeCanvas.cs
- `Harness` --references--> `InteractiveBadgeCanvas`  [EXTRACTED]
  BadgeForge.UiTests/InlineTextEditorUiTests.cs → BadgeForge.App/Controls/InteractiveBadgeCanvas.cs
- `CardRendererTests` --references--> `CardRenderer`  [EXTRACTED]
  BadgeForge.Tests/CardRendererTests.cs → BadgeForge.Core.Rendering/CardRenderer.cs

## Import Cycles
- None detected.

## Communities (161 total, 38 thin omitted)

### Community 0 - "MainWindowViewModel"
Cohesion: 0.01
Nodes (134): AvaloniaBitmap, MainWindowViewModel, ActiveDriver, ActiveFormat, AllPeopleIncluded, AvailableFormats, AvailablePrinters, BarcodeContent (+126 more)

### Community 1 - "InteractiveBadgeCanvas"
Cohesion: 0.06
Nodes (36): InteractiveBadgeCanvas, Format, InlineEditor, IsInPointerGesture, ViewModel, Zoom, CardFormat, DragEventArgs (+28 more)

### Community 2 - "RosterTable"
Cohesion: 0.06
Nodes (36): RosterFileFormat, Csv, LegacyXls, Xlsx, RosterRow, RosterTable, ColumnCount, Columns (+28 more)

### Community 3 - "BadgeRecord"
Cohesion: 0.06
Nodes (32): BadgeRecord, BatchIndex, Fields, ResolvedPhotoPath, RowNumber, Dictionary, RosterService, IEnumerable (+24 more)

### Community 4 - "ImportSummaryViewModel"
Cohesion: 0.06
Nodes (39): ImportChoice, Append, Cancel, Replace, ImportColumnSummary, ImportPreviewCell, IsEmpty, ImportPreviewRow (+31 more)

### Community 5 - "MainWindow"
Cohesion: 0.08
Nodes (6): MainWindow, ViewModel, PeopleFieldColumn, TemplateLayer, DataFieldKind, RoutedEventArgs

### Community 6 - "DefaultRibbonModeController"
Cohesion: 0.07
Nodes (30): Bitmap, PrintDocumentHelper, Bitmap, CardFormat, SKBitmap, SupportedOSPlatform, DefaultRibbonModeController, ActiveMode (+22 more)

### Community 7 - ".RequestLivePreviewUpdate"
Cohesion: 0.10
Nodes (6): BarcodeLayer, EventArgs, PhotoLayer, LayerEditingTests, Fact, BarcodeSymbology

### Community 8 - "BatchPrintEngine"
Cohesion: 0.13
Nodes (22): BatchExecutionState, Cancelled, Completed, Idle, PausedByUser, PausedForHopper, PausedOnError, Running (+14 more)

### Community 9 - "PersonItem"
Cohesion: 0.05
Nodes (38): ColumnMappingItem, CsvColumn, TemplateToken, PersonFieldCell, Key, Label, Owner, Token (+30 more)

### Community 10 - "BadgeForge.Core.Templates.Layers"
Cohesion: 0.10
Nodes (12): TextAlignment, Center, Left, Right, BadgeForge.Core.Templates.Layers, BadgeForge.Core.Templates.Enums, BadgeForge.Core.Rendering.Elements, BadgeForge.Core.Templates.Tokens (+4 more)

### Community 11 - "IReadOnlyList"
Cohesion: 0.09
Nodes (6): BadgeRecord, IEnumerable, IReadOnlyList, PeopleFieldColumn, PersonItem, PropertyChangedEventArgs

### Community 12 - "TemplateDefinition"
Cohesion: 0.09
Nodes (23): TemplateDefinition, CreatedAtUtc, Id, Layers, Metadata, ModifiedAtUtc, Name, SchemaVersion (+15 more)

### Community 13 - "PrinterCapabilities"
Cohesion: 0.07
Nodes (31): CardPrintJob, SKBitmap, PrinterCapabilityMismatchException, DriverCanvasHeight, DriverCanvasWidth, DriverDpiX, DriverDpiY, ExpectedCanvasHeight (+23 more)

### Community 14 - "BatchPrintRequest"
Cohesion: 0.12
Nodes (24): BatchPrintRequest, AutoResumeFromCheckpoint, BatchName, BatchRunId, CardCompletionTimeout, Cards, CheckpointFilePath, Driver (+16 more)

### Community 15 - "BadgeForge.Core.Printing.Models"
Cohesion: 0.15
Nodes (7): BadgeForge.Core.Printing.Drivers, BadgeForge.Core.Printing.Interfaces, BadgeForge.Core.Printing.Models, BadgeForge.Tests, BadgeForge.Core.Rendering, BadgeForge.Core.Printing.Exceptions, BadgeForge.Core.Printing.Batch

### Community 16 - "TemplateLayer"
Cohesion: 0.10
Nodes (8): Func, Rect, TemplateLayer, Height, Width, ImageAdjustments, PhotoCropMode, TextAlignment

### Community 17 - "MainWindowViewModel"
Cohesion: 0.13
Nodes (15): MainWindowViewModel, CanExportPdf, ExportPdfButtonText, ExportProgressPercent, ExportProgressText, IsExporting, HasSelectedLayerTokens, SelectedLayerTokensText (+7 more)

### Community 18 - ".Replace"
Cohesion: 0.10
Nodes (17): BoundTemplate, TemplateBinder, Dictionary, IReadOnlyDictionary, IReadOnlyList, Layer, List, TokenSyntax (+9 more)

### Community 19 - "TextLayer"
Cohesion: 0.09
Nodes (22): AvaloniaPropertyChangedEventArgs, InlineTextEditor, EffectiveFontSizePoints, Layer, StyleKeyOverride, Dictionary, KeyEventArgs, Type (+14 more)

### Community 20 - ".RunPrintJobAsync"
Cohesion: 0.09
Nodes (16): ConfirmPrintWithWarningsAsync, PreviewRequest, PrintCheckResult, PrintJobInfo, CancellationToken, Dictionary, IReadOnlyDictionary, List (+8 more)

### Community 21 - "IdpSmart31StatusMonitor"
Cohesion: 0.11
Nodes (17): DefaultNativeIdpStatusProvider, IsAvailable, IdpSmart31StatusMonitor, CardsPrintedSincePause, CurrentStatus, IsMonitoring, INativeIdpStatusProvider, IsAvailable (+9 more)

### Community 22 - "MainWindowViewModelTests"
Cohesion: 0.14
Nodes (6): MainWindowViewModelTests, BarcodeLayer, Fact, PhotoLayer, Task, TextLayer

### Community 23 - "MockCardPrinterDriver"
Cohesion: 0.11
Nodes (22): MockCardPrinterDriver, AbortedCardCount, DisplayName, DriverId, HoldCardInPrinterAtBatchIndex, MockStatus, ProbedCapabilities, RibbonController (+14 more)

### Community 24 - "IdpSmart31CardPrinterDriver"
Cohesion: 0.12
Nodes (17): IdpSmart31CardPrinterDriver, DisplayName, DriverId, RibbonController, StatusMonitor, TargetPrinterName, CancellationToken, CardPrintResult (+9 more)

### Community 25 - "BadgeForge.Core.Data.Models"
Cohesion: 0.10
Nodes (12): TextColorSwatch, Brush, IBrush, PreFlightValidationOptions, AllowMissingPhotoAsWarning, CheckDuplicates, EnforceRequirements, PrimaryKeyToken (+4 more)

### Community 26 - "TemplateMigrationPipeline"
Cohesion: 0.10
Nodes (19): ITemplateMigrator, SourceVersion, TargetVersion, JsonNode, TemplateMigrationPipeline, ConcurrentDictionary, From, JsonNode (+11 more)

### Community 27 - "BatchCheckpoint"
Cohesion: 0.10
Nodes (20): BatchCheckpoint, BatchName, BatchRunId, CompletedCards, FailedCards, IsComplete, LastUpdatedAtUtc, PendingCards (+12 more)

### Community 28 - "ImageSourceLoader"
Cohesion: 0.12
Nodes (13): ImageSourceLoader, SupportedExtensions, ImportedImage, Lease, Dictionary, Func, IDisposable, IReadOnlyList (+5 more)

### Community 29 - "DesignerDataAndExportViewModelTests"
Cohesion: 0.14
Nodes (11): CardFormat, DataFieldKind, Barcode, Photo, QrCode, Text, DesignerDataAndExportViewModelTests, Fact (+3 more)

### Community 30 - "CardRenderer"
Cohesion: 0.18
Nodes (14): CardRenderer, DrawMissingPhotoPlaceholders, StrictLayerRendering, CancellationToken, IReadOnlyDictionary, IReadOnlyList, SKBitmap, SKCanvas (+6 more)

### Community 31 - "MainWindowViewModel"
Cohesion: 0.20
Nodes (11): MainWindowViewModel, CanRedo, CanUndo, RedoTooltip, UndoTooltip, TemplateLayer, Label, UndoRedoTests (+3 more)

### Community 32 - "CardPrintJob"
Cohesion: 0.12
Nodes (20): OperatorPauseRequiredException, CardsPrintedInBatch, MaxCardsBeforePause, PrinterException, PrinterHardwareFaultException, FaultCode, CardPrintJob, BackPixelBuffer (+12 more)

### Community 33 - ".ComputeContentRect"
Cohesion: 0.19
Nodes (10): ImageLayout, ImageAdjustments, PhotoCropMode, CropBoxTests, Fact, PhotoCropMode, Task, InlineData (+2 more)

### Community 34 - "DesignerEditingUiTests"
Cohesion: 0.28
Nodes (8): DesignerEditingUiTests, Harness, Fact, ListBox, PhysicalKey, RawInputModifiers, Task, TemplateLayer

### Community 35 - "ImageAdjustmentTests"
Cohesion: 0.20
Nodes (6): ImageAdjustmentTests, Fact, SKBitmap, SKColor, Task, StaticImageLayer

### Community 36 - "Task"
Cohesion: 0.10
Nodes (6): Task, BatchExecutionEventArgs, CancellationTokenSource, ErrorPromptKind, ImportedImage, RosterTable

### Community 37 - "TemplateHistory"
Cohesion: 0.15
Nodes (12): TemplateHistory, CanRedo, CanUndo, RedoLabel, UndoDepth, UndoLabel, TemplateSnapshot, Func (+4 more)

### Community 38 - "BatchCardItem"
Cohesion: 0.11
Nodes (19): BatchCardItem, BackBitmap, BackPixelBuffer, BatchIndex, Format, FrontBitmap, FrontPixelBuffer, Label (+11 more)

### Community 39 - "PeopleColumnHeaderUiTests"
Cohesion: 0.21
Nodes (12): PeopleFieldColumn, Harness, PeopleColumnHeaderUiTests, Border, Control, Fact, ListBox, Point (+4 more)

### Community 40 - ".RunAsync"
Cohesion: 0.29
Nodes (7): BatchStopControlTests, Fact, Func, List, Task, Events, Summary

### Community 41 - "WindowsSpooler"
Cohesion: 0.21
Nodes (11): JobInfo1, SystemTime, WindowsSpooler, Func, IReadOnlyCollection, IReadOnlyList, BufferCall, DllImport (+3 more)

### Community 42 - "LayerRegistry"
Cohesion: 0.16
Nodes (10): ArgumentException, ILayerRegistry, IReadOnlyDictionary, Type, LayerRegistry, Default, ConcurrentDictionary, IReadOnlyDictionary (+2 more)

### Community 43 - "CardFormat"
Cohesion: 0.11
Nodes (15): CardFormat, AspectRatio, CR79, CR80, Dpi, HeightMm, HeightPixels, IsPortrait (+7 more)

### Community 45 - ".ValidateBatch"
Cohesion: 0.22
Nodes (15): PreFlightValidationService, BadgeRecord, BarcodeLayer, Dictionary, IReadOnlyDictionary, IReadOnlyList, List, PhotoLayer (+7 more)

### Community 46 - "TextLayoutEngine"
Cohesion: 0.30
Nodes (6): TextLayoutEngine, TextLayoutResult, Func, IReadOnlyList, List, TextMeasurer

### Community 47 - ".ShowDialogAsync"
Cohesion: 0.15
Nodes (4): List, Task, Window, WindowClosingEventArgs

### Community 48 - "Cardholder"
Cohesion: 0.13
Nodes (13): Cardholder, AdditionalFields, Department, FullName, Id, IsSelected, JobTitle, PhotoPath (+5 more)

### Community 49 - ".RunOnUiThread"
Cohesion: 0.31
Nodes (9): Harness, InlineTextEditorUiTests, Action, Border, Fact, Func, Task, TextLayer (+1 more)

### Community 50 - "Harness"
Cohesion: 0.25
Nodes (10): Harness, PeopleListUiTests, Button, Fact, ListBox, PhysicalKey, RawInputModifiers, Task (+2 more)

### Community 51 - "RenderedCard"
Cohesion: 0.14
Nodes (15): CancellationTokenSource, Task, BatchExportResult, BatchRenderProgress, Message, Percent, CardRenderJob, RenderedCard (+7 more)

### Community 52 - "SpoolerCorrelationResult"
Cohesion: 0.14
Nodes (13): ISpoolerJobCorrelator, SpoolerCorrelationResult, Elapsed, ErrorMessage, ExpectedJobName, IsCorrelated, MatchedJobName, RetriesAttempted (+5 more)

### Community 53 - ".OpenTwoColourCard"
Cohesion: 0.27
Nodes (9): ColorDropperUiTests, AvaloniaBitmap, Color, Fact, Task, TextLayer, Window, Canvas (+1 more)

### Community 54 - "BadgeForge.App.ViewModels"
Cohesion: 0.25
Nodes (4): BadgeForge.UiTests, BadgeForge.App.ViewModels, BadgeForge.App.Controls, BadgeForge.App

### Community 55 - "PeopleColumnLayout"
Cohesion: 0.27
Nodes (6): PeopleColumnLayout, IsCustomised, IReadOnlyList, List, From, To

### Community 56 - "XlsxWorksheetReader"
Cohesion: 0.26
Nodes (7): XlsxWorksheetReader, HashSet, IReadOnlyList, List, Stream, XElement, ZipArchive

### Community 57 - "PreFlightValidationTests"
Cohesion: 0.21
Nodes (6): PreFlightValidationBlockedException, Report, PreFlightValidationTests, Fact, Task, InvalidOperationException

### Community 58 - ".Render"
Cohesion: 0.29
Nodes (7): BarcodeRenderer, IReadOnlyDictionary, SKCanvas, SKRect, BarcodeFormat, BitMatrix, SKRectI

### Community 59 - ".RenderAll"
Cohesion: 0.38
Nodes (6): BatchCardRenderServiceTests, Fact, IReadOnlyList, List, Task, OperationCanceledException

### Community 60 - "PreFlightValidationReport"
Cohesion: 0.12
Nodes (15): PreFlightErrorCodes, PreFlightValidationReport, DuplicateIdentifiers, ErrorCount, Errors, HasErrors, HasWarnings, InvalidBarcodes (+7 more)

### Community 61 - "BatchExecutionEventArgs"
Cohesion: 0.12
Nodes (16): BatchExecutionEventArgs, CurrentCardIndex, CurrentRecord, CurrentState, Message, PauseReason, PreviousState, TotalCards (+8 more)

### Community 62 - "SKPaint"
Cohesion: 0.21
Nodes (8): PhotoRenderer, IReadOnlyDictionary, SKBitmap, SKCanvas, SKRect, Stream, SKEncodedOrigin, SKPaint

### Community 63 - "PhotoLayer"
Cohesion: 0.13
Nodes (16): PhotoCropMode, AspectFill, AspectFit, Stretch, IImageLayer, Adjustments, BorderRadius, CropMode (+8 more)

### Community 64 - "ImageAdjustments"
Cohesion: 0.12
Nodes (14): ImageAdjustments, Brightness, Contrast, FlipHorizontal, FlipVertical, HasColorAdjustments, IsIdentityTransform, None (+6 more)

### Community 65 - ".OnDesignerKeyDown"
Cohesion: 0.14
Nodes (5): Action, ComboBox, Key, ListBox, Slider

### Community 66 - "LayerVisibilityIconConverter"
Cohesion: 0.18
Nodes (9): BoolToDimOpacityConverter, CultureInfo, Type, LayerVisibilityIconConverter, CultureInfo, Geometry, Type, BadgeForge.App.Converters (+1 more)

### Community 67 - "MockStatusMonitor"
Cohesion: 0.18
Nodes (5): MockStatusMonitor, CurrentStatus, IsMonitoring, Func, TimeSpan

### Community 68 - "ICardPrinterDriver"
Cohesion: 0.17
Nodes (12): ICardPrinterDriver, DisplayName, DriverId, RibbonController, StatusMonitor, TargetPrinterName, CancellationToken, CardPrintJob (+4 more)

### Community 69 - ".ExtractTokensFromString"
Cohesion: 0.13
Nodes (9): IEnumerable, IEnumerable, IEnumerable, IEnumerable, SignatureCaptureLayer, LayerType, PenColorHex, SignatureToken (+1 more)

### Community 70 - "PeopleColumnOrderTests"
Cohesion: 0.32
Nodes (4): PeopleColumnOrderTests, Dictionary, Fact, Task

### Community 71 - ".CorrelateJobAsync"
Cohesion: 0.16
Nodes (10): SpoolerJobCorrelator, Action, CancellationToken, Func, SupportedOSPlatform, Task, TimeSpan, ISpoolerJobCorrelator (+2 more)

### Community 72 - "TemplateLayer"
Cohesion: 0.15
Nodes (13): TemplateLayer, HasDynamicTokens, Height, Id, IsLocked, IsVisible, LayerType, Name (+5 more)

### Community 73 - ".Render"
Cohesion: 0.27
Nodes (7): TextRenderer, IReadOnlyDictionary, SKCanvas, SKFontStyleWeight, SKRect, SKTypeface, SKFont

### Community 74 - ".RenderAsync"
Cohesion: 0.26
Nodes (9): ArgumentOutOfRangeException, BatchCardRenderService, IBatchCardRenderService, CancellationToken, CardRenderer, IEnumerable, IProgress, IReadOnlyList (+1 more)

### Community 75 - ".FindDropTarget"
Cohesion: 0.21
Nodes (7): Control, DragEventArgs, Button, Highlight, OnPhoto, Person, Visual

### Community 76 - "ValidationIssue"
Cohesion: 0.17
Nodes (11): ValidationIssue, BatchIndex, ErrorCode, Message, RecordIdentifier, RowNumber, Severity, TargetName (+3 more)

### Community 77 - "IBatchPrintEngine"
Cohesion: 0.33
Nodes (5): IBatchPrintEngine, CurrentCheckpoint, State, CancellationToken, Task

### Community 78 - "SpoolerSnapshot"
Cohesion: 0.22
Nodes (9): SpoolerJob, IsFinished, SpoolerSnapshot, SpoolerStatusMapper, InlineData, Theory, Message, PrinterState (+1 more)

### Community 79 - "PrinterStatus"
Cohesion: 0.15
Nodes (13): PrinterStatus, ActiveJobId, CardsPrintedSincePause, InputHopperCount, IsNativeStatus, IsReadyToPrint, OutputHopperCount, RequiresOperatorAttention (+5 more)

### Community 80 - ".ExportAsync"
Cohesion: 0.15
Nodes (11): BatchPdfExporter, CancellationToken, IReadOnlyList, Task, TemplateDefinition, BatchExportResult, BatchRenderOptions, BatchRenderProgress (+3 more)

### Community 81 - "TextLayoutEngineTests"
Cohesion: 0.36
Nodes (3): TextLayoutEngineTests, Fact, TextMeasurer

### Community 82 - "BadgeForge.sln"
Cohesion: 0.26
Nodes (10): BadgeForge.App, BadgeForge.Core.Data, BadgeForge.Core.Printing, BadgeForge.Core.Rendering, BadgeForge.Core.Templates, BadgeForge.Installer, BadgeForge.Tests, BadgeForge.UiTests (+2 more)

### Community 83 - ".ResolvePhotoPath"
Cohesion: 0.36
Nodes (6): PhotoMatchingService, BadgeRecord, IReadOnlyDictionary, PhotoMatchingOptions, PhotoMatchingServiceTests, Fact

### Community 84 - "BatchRecordCheckpoint"
Cohesion: 0.17
Nodes (12): BatchRecordCheckpoint, BatchIndex, CompletedAtUtc, CorrelatedJobName, ErrorMessage, Label, RecordId, RetryCount (+4 more)

### Community 85 - "ICardPrinterStatusMonitor"
Cohesion: 0.17
Nodes (6): ICardPrinterStatusMonitor, CurrentStatus, IsMonitoring, CancellationToken, Task, TimeSpan

### Community 86 - "CardPrintResult"
Cohesion: 0.17
Nodes (9): CardPrintResult, CompletedAtUtc, ErrorMessage, JobId, OperatorPauseTriggered, SpoolerJobId, SubmittedAtUtc, Success (+1 more)

### Community 87 - "PrinterState"
Cohesion: 0.17
Nodes (11): PrinterState, CoverOpen, Error, Offline, OperatorPauseRequired, OutOfCards, OutOfRibbon, PaperJam (+3 more)

### Community 88 - ".GoldenImage_FixedTemplateAndData_MatchesReferencePixelForPixel"
Cohesion: 0.30
Nodes (5): ImageDiffService, SKBitmap, GoldenImageTests, Fact, Task

### Community 89 - "Program"
Cohesion: 0.36
Nodes (3): Program, BadgeForge.Installer, SupportedOSPlatform

### Community 90 - ".Snap"
Cohesion: 0.38
Nodes (3): IEnumerable, LayerSnapperTests, Fact

### Community 91 - "BundledFonts"
Cohesion: 0.33
Nodes (6): BundledFonts, Dictionary, IReadOnlyList, SKFontStyleWeight, SKTypeface, SKFontStyleSlant

### Community 92 - ".Draw"
Cohesion: 0.20
Nodes (8): ImageDrawing, SKBitmap, SKCanvas, SKRect, StaticImageRenderer, SKCanvas, SKRect, SKColorFilter

### Community 93 - "PdfCardDocumentWriter"
Cohesion: 0.20
Nodes (8): PdfCardDocumentWriter, PageCount, PageHeightPoints, PageWidthPoints, SKBitmap, IDisposable, SKDocument, SKManagedWStream

### Community 94 - "LayerJsonConverter"
Cohesion: 0.29
Nodes (7): LayerJsonConverter, ConcurrentDictionary, JsonSerializerOptions, Type, JsonConverter, Utf8JsonReader, Utf8JsonWriter

### Community 95 - "App"
Cohesion: 0.22
Nodes (5): Application, App, Program, AppBuilder, STAThread

### Community 96 - "BatchExecutionSummary"
Cohesion: 0.20
Nodes (10): BatchExecutionSummary, BatchRunId, Checkpoint, CompletedCards, ElapsedTime, FailedCards, FinalState, TerminalMessage (+2 more)

### Community 97 - ".SubmitCardAsync"
Cohesion: 0.24
Nodes (6): CancellationToken, CardPrintJob, CardPrintResult, Task, CancellationToken, Task

### Community 98 - ".RenderCardAsync"
Cohesion: 0.31
Nodes (6): ICardRenderer, CancellationToken, IReadOnlyDictionary, IReadOnlyList, SKBitmap, Task

### Community 99 - "StaticImageLayer"
Cohesion: 0.20
Nodes (10): StaticImageLayer, Adjustments, Base64Data, BorderRadius, CropMode, HasImage, ImagePath, LayerType (+2 more)

### Community 100 - "CardholderRosterTests"
Cohesion: 0.29
Nodes (4): CardholderRosterTests, Fact, InvalidDataException, NotSupportedException

### Community 101 - "LayerSnapper"
Cohesion: 0.28
Nodes (5): LayerSnapper, SnapResult, List, Guide, Position

### Community 102 - ".EndColumnDrag"
Cohesion: 0.22
Nodes (3): PointerCaptureLostEventArgs, PointerEventArgs, PointerReleasedEventArgs

### Community 104 - "ImageDiffResult"
Cohesion: 0.22
Nodes (8): ImageDiffResult, DiffBitmap, IsMatch, MaxChannelDelta, MismatchPercentage, MismatchPixels, TotalPixels, BadgeForge.Core.Rendering.Testing

### Community 105 - "CardRendererTests"
Cohesion: 0.39
Nodes (3): CardRendererTests, Fact, Task

### Community 106 - ".Open"
Cohesion: 0.42
Nodes (5): Harness, PrintStopUiTests, Fact, Func, Task

### Community 107 - ".FocusPersonField"
Cohesion: 0.25
Nodes (3): KeyEventArgs, PersonItem, TextBox

### Community 108 - "ColumnMapping"
Cohesion: 0.29
Nodes (4): ColumnMapping, Mappings, Dictionary, IReadOnlyDictionary

### Community 109 - "PhotoMatchingOptions"
Cohesion: 0.29
Nodes (6): PhotoMatchingOptions, AllowedExtensions, FallbackImagePath, FilenamePattern, PhotoDirectory, IReadOnlyList

### Community 110 - "BarcodeLayer"
Cohesion: 0.29
Nodes (7): BarcodeLayer, ContentToken, IncludeText, IsPureBlackKResin, IsRequired, LayerType, Symbology

### Community 111 - "HeadlessTestApp"
Cohesion: 0.33
Nodes (5): App, AppBuilder, HeadlessTestApp, HeadlessUnitTestSession, Lazy

### Community 112 - "LayerIconConverter"
Cohesion: 0.47
Nodes (4): LayerIconConverter, CultureInfo, Geometry, Type

### Community 114 - "CardOutcome"
Cohesion: 0.33
Nodes (6): CardOutcome, Printed, StoppedAfterCardFinished, StoppedBeforeCard, StoppedCardAborted, StoppedCardUnknown

### Community 115 - ".Draw"
Cohesion: 0.33
Nodes (4): PhotoPlaceholder, SKCanvas, SKColor, SKRect

### Community 117 - "V1NoOpMigrator"
Cohesion: 0.33
Nodes (4): V1NoOpMigrator, SourceVersion, TargetVersion, JsonNode

### Community 118 - "ResumeDecision"
Cohesion: 0.40
Nodes (5): ResumeDecision, AbortCard, Continue, FinishCard, Reprint

### Community 119 - "BatchRenderOptions"
Cohesion: 0.40
Nodes (5): BatchRenderOptions, Dpi, DrawMissingPhotoPlaceholders, MaxDegreeOfParallelism, StrictLayerRendering

### Community 120 - "TextOverflowMode"
Cohesion: 0.40
Nodes (5): TextOverflowMode, Ellipsis, Overflow, ShrinkToFit, Wrap

### Community 121 - ".SelectingALayer_BringsTheLayerTabBackFromTheDataTab"
Cohesion: 0.40
Nodes (4): DesignPageUiTests, Fact, Task, TabControl

### Community 122 - "ErrorPromptKind"
Cohesion: 0.50
Nodes (4): ErrorPromptKind, Dismiss, OutcomeUnknown, Retry

### Community 123 - "PrinterStatusChangedEventArgs"
Cohesion: 0.50
Nodes (4): PrinterStatusChangedEventArgs, CurrentStatus, PreviousStatus, EventArgs

### Community 124 - "SynchronousProgress"
Cohesion: 0.50
Nodes (3): SynchronousProgress, Action, IProgress

### Community 125 - "WaitOutcome"
Cohesion: 0.67
Nodes (3): WaitOutcome, Ready, StopRequested

## Knowledge Gaps
- **598 isolated node(s):** `SystemTime`, `PreFlightErrorCodes`, `ActiveDriver`, `ActiveFormat`, `AllPeopleIncluded` (+593 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 974 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **38 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `MainWindowViewModel` connect `MainWindowViewModel` to `InteractiveBadgeCanvas`, `ImageAdjustmentTests`, `Task`, `MainWindow`, `.RequestLivePreviewUpdate`, `PersonItem`, `IReadOnlyList`, `TemplateDefinition`, `.ValidateBatch`, `TemplateLayer`, `MainWindowViewModel`, `ViewModelBase`, `.ResolvePhotoPath`, `.RunPrintJobAsync`, `BadgeForge.Core.Data.Models`, `ErrorPromptKind`, `DesignerDataAndExportViewModelTests`?**
  _High betweenness centrality (0.368) - this node is a cross-community bridge._
- **Why does `MainWindow` connect `MainWindow` to `MainWindowViewModel`, `.OnDesignerKeyDown`, `DesignerEditingUiTests`, `.EndColumnDrag`, `PeopleColumnHeaderUiTests`, `.Open`, `.FindDropTarget`, `.FocusPersonField`, `.RunSafelyAsync`, `.ShowDialogAsync`, `Harness`, `BadgeForge.App.ViewModels`, `.SelectingALayer_BringsTheLayerTabBackFromTheDataTab`, `.OnColumnHeaderPointerPressed`, `App`?**
  _High betweenness centrality (0.181) - this node is a cross-community bridge._
- **Why does `MockCardPrinterDriver` connect `MockCardPrinterDriver` to `CardPrintJob`, `.SubmitCardAsync`, `MockStatusMonitor`, `ICardPrinterDriver`, `.RunAsync`, `CardRendererTests`, `.Open`, `PrinterCapabilities`, `BatchPrintRequest`, `BadgeForge.Core.Printing.Models`, `MainWindowViewModel`, `MainWindowViewModelTests`, `.GoldenImage_FixedTemplateAndData_MatchesReferencePixelForPixel`, `PreFlightValidationTests`, `CardRenderer`?**
  _High betweenness centrality (0.142) - this node is a cross-community bridge._
- **Are the 2 inferred relationships involving `MainWindow` (e.g. with `.OnFrameworkInitializationCompleted()` and `.SelectingALayer_BringsTheLayerTabBackFromTheDataTab()`) actually correct?**
  _`MainWindow` has 2 INFERRED edges - model-reasoned connections that need verification._
- **Are the 4 inferred relationships involving `MainWindowViewModel` (e.g. with `.CropBoxDrag_ChangesTheFrame_ButNotWhereThePictureSitsOnTheCard()` and `.CropBoxDrag_IsOneUndoStep_AndUndoRestoresTheFrameAndThePicture()`) actually correct?**
  _`MainWindowViewModel` has 4 INFERRED edges - model-reasoned connections that need verification._
- **What connects `SystemTime`, `PreFlightErrorCodes`, `ActiveDriver` to the rest of the system?**
  _598 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `MainWindowViewModel` be split into smaller, more focused modules?**
  _Cohesion score 0.014496644295302013 - nodes in this community are weakly interconnected._