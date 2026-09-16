# Graph Report - Printer Application  (2026-09-14)

## Corpus Check
- 90 files · ~40,377 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 1277 nodes · 2525 edges · 68 communities (62 shown, 6 thin omitted)
- Extraction: 89% EXTRACTED · 11% INFERRED · 0% AMBIGUOUS · INFERRED: 269 edges (avg confidence: 0.83)
- Token cost: 0 input · 0 output

## Community Hubs (Navigation)
- BadgeForge.Core.Templates.Layers
- LayerRegistry
- MainWindowViewModel
- MainWindow
- BarcodeValidator
- SpoolerCorrelationResult
- MockCardPrinterDriver
- BatchCheckpoint
- BatchRecordDisplayItem
- InteractiveBadgeCanvas
- BatchExecutionEventArgs
- .RequestLivePreviewUpdate
- BatchPrintRequest
- BadgeForge.App
- PreFlightValidationReport
- .GoldenImage_FixedTemplateAndData_MatchesReferencePixelForPixel
- .ValidateBatch
- CardFormat
- ImageAdjustmentTests
- TemplateMigrationPipeline
- BatchPrintEngine
- IRibbonModeController
- IdpSmart31CardPrinterDriver
- ImageSourceLoader
- BatchRecordCheckpoint
- BarcodeLayer
- MainWindowViewModelTests
- PhotoLayer
- ImageAdjustments
- ColumnMapping
- PrinterStatus
- PrinterCapabilities
- .RenderCardAsync
- TextLayer
- Task
- ValidationIssue
- .SubmitCardAsync
- IdpSmart31StatusMonitor
- ICardPrinterStatusMonitor
- TemplateDefinition
- PrinterCapabilityMismatchException
- .CrashResume_SkipsAlreadyPrintedCards_AndResumesUnfinishedWork
- SKPaint
- CardPrintResult
- LayerIconConverter
- BatchExecutionSummary
- TemplateLayer
- PrinterState
- CardPrintJob
- .Draw
- .OnPropertyChanged
- BatchCardItem
- .Render
- IBatchPrintEngine
- .MigrationPipeline_UpgradesLegacySchema_ToCurrentVersion
- StaticImageLayer
- .Render
- PreFlightValidationOptions
- .LoadAsync
- IdpSmart31StatusMonitor.cs
- .ComputeContentRect
- CardRendererTests
- .Pagination_WithLoadedData_NavigatesCorrectly
- CardFormatTests
- .PollStatusAsync
- .ReplaceSelectedLayer
- TextColorSwatch
- .GetAllReferencedTokens

## God Nodes (most connected - your core abstractions)
1. `MainWindowViewModel` - 185 edges
2. `TemplateDefinition` - 45 edges
3. `TemplateLayer` - 44 edges
4. `MainWindow` - 41 edges
5. `InteractiveBadgeCanvas` - 40 edges
6. `MockCardPrinterDriver` - 35 edges
7. `ImageAdjustments` - 34 edges
8. `TextLayer` - 32 edges
9. `BatchPrintEngine` - 31 edges
10. `BadgeForge.Core.Printing.Models` - 27 edges

## Surprising Connections (you probably didn't know these)
- `InteractiveBadgeCanvas` --references--> `CardFormat`  [EXTRACTED]
  BadgeForge.App/Controls/InteractiveBadgeCanvas.cs → BadgeForge.Core.Printing/Models/CardFormat.cs
- `InteractiveBadgeCanvas` --references--> `TemplateLayer`  [EXTRACTED]
  BadgeForge.App/Controls/InteractiveBadgeCanvas.cs → BadgeForge.Core.Templates/Layers/TemplateLayer.cs
- `MainWindowViewModel` --references--> `BadgeRecord`  [EXTRACTED]
  BadgeForge.App/ViewModels/MainWindowViewModel.cs → BadgeForge.Core.Data/Models/BadgeRecord.cs
- `MainWindowViewModel` --references--> `CsvDataIngestionService`  [EXTRACTED]
  BadgeForge.App/ViewModels/MainWindowViewModel.cs → BadgeForge.Core.Data/Services/CsvDataIngestionService.cs
- `MainWindowViewModel` --references--> `PhotoMatchingService`  [EXTRACTED]
  BadgeForge.App/ViewModels/MainWindowViewModel.cs → BadgeForge.Core.Data/Services/PhotoMatchingService.cs

## Import Cycles
- None detected.

## Communities (68 total, 6 thin omitted)

### Community 0 - "BadgeForge.Core.Templates.Layers"
Cohesion: 0.06
Nodes (27): PrinterStatusChangedEventArgs, CurrentStatus, PreviousStatus, ImageDrawing, StaticImageRenderer, BadgeForge.Core.Data.Validation, BadgeForge.Core.Printing.Drivers, BadgeForge.App.Converters (+19 more)

### Community 1 - "LayerRegistry"
Cohesion: 0.15
Nodes (10): ILayerRegistry, IReadOnlyDictionary, Type, LayerRegistry, Default, ConcurrentDictionary, IReadOnlyDictionary, Type (+2 more)

### Community 2 - "MainWindowViewModel"
Cohesion: 0.03
Nodes (72): MainWindowViewModel, ActiveFormat, AvailableFormats, AvailablePrinters, BatchItems, BatchProgressPercent, BatchState, CanGoNext (+64 more)

### Community 3 - "MainWindow"
Cohesion: 0.06
Nodes (13): AppBuilder, Application, App, MainWindow, ViewModel, EventArgs, KeyEventArgs, Task (+5 more)

### Community 4 - "BarcodeValidator"
Cohesion: 0.15
Nodes (15): BarcodeValidator, Regex, BarcodeValidationResult, IBarcodeValidator, BarcodeSymbology, Code128, Code39, Ean13 (+7 more)

### Community 5 - "SpoolerCorrelationResult"
Cohesion: 0.09
Nodes (21): ISpoolerJobCorrelator, SpoolerCorrelationResult, Elapsed, ErrorMessage, ExpectedJobName, IsCorrelated, MatchedJobName, RetriesAttempted (+13 more)

### Community 6 - "MockCardPrinterDriver"
Cohesion: 0.14
Nodes (13): MockCardPrinterDriver, DisplayName, DriverId, MockStatus, ProbedCapabilities, RibbonController, SimulatedPrintDelayMs, SimulatePaperJamAtBatchIndex (+5 more)

### Community 7 - "BatchCheckpoint"
Cohesion: 0.11
Nodes (20): BatchCheckpoint, BatchName, BatchRunId, CompletedCards, FailedCards, IsComplete, LastUpdatedAtUtc, PendingCards (+12 more)

### Community 8 - "BatchRecordDisplayItem"
Cohesion: 0.13
Nodes (14): BatchRecordDisplayItem, BatchIndex, ErrorMessage, Name, PhotoStatus, PrintStatus, RecordId, StatusBadgeColor (+6 more)

### Community 9 - "InteractiveBadgeCanvas"
Cohesion: 0.07
Nodes (29): InteractiveBadgeCanvas, Bitmap, Format, ViewModel, Zoom, EventArgs, IBrush, KeyEventArgs (+21 more)

### Community 10 - "BatchExecutionEventArgs"
Cohesion: 0.09
Nodes (22): BatchExecutionEventArgs, CurrentCardIndex, CurrentRecord, CurrentState, Message, PauseReason, PreviousState, TotalCards (+14 more)

### Community 12 - "BatchPrintRequest"
Cohesion: 0.11
Nodes (18): BatchPrintRequest, AutoResumeFromCheckpoint, BatchName, BatchRunId, CardCompletionTimeout, Cards, CheckpointFilePath, Driver (+10 more)

### Community 13 - "BadgeForge.App"
Cohesion: 0.08
Nodes (35): BadgeForge.App, net10.0, SkiaSharp (4.152.0), SkiaSharp.NativeAssets.Linux (4.152.0), Microsoft.NET.Sdk, BadgeForge.Core.Data, net10.0, Microsoft.NET.Sdk (+27 more)

### Community 14 - "PreFlightValidationReport"
Cohesion: 0.13
Nodes (17): PreFlightErrorCodes, PreFlightValidationBlockedException, Report, PreFlightValidationReport, DuplicateIdentifiers, ErrorCount, Errors, HasErrors (+9 more)

### Community 15 - ".GoldenImage_FixedTemplateAndData_MatchesReferencePixelForPixel"
Cohesion: 0.15
Nodes (13): ImageDiffResult, DiffBitmap, IsMatch, MaxChannelDelta, MismatchPercentage, MismatchPixels, TotalPixels, ImageDiffService (+5 more)

### Community 16 - ".ValidateBatch"
Cohesion: 0.13
Nodes (19): BadgeRecord, BatchIndex, Fields, ResolvedPhotoPath, RowNumber, Dictionary, PhotoMatchingOptions, AllowedExtensions (+11 more)

### Community 17 - "CardFormat"
Cohesion: 0.13
Nodes (13): CardFormat, AspectRatio, CR79, CR80, Dpi, HeightMm, HeightPixels, IsPortrait (+5 more)

### Community 18 - "ImageAdjustmentTests"
Cohesion: 0.24
Nodes (4): ImageAdjustmentTests, Fact, SKBitmap, Task

### Community 19 - "TemplateMigrationPipeline"
Cohesion: 0.12
Nodes (13): ITemplateMigrator, SourceVersion, TargetVersion, JsonNode, TemplateMigrationPipeline, ConcurrentDictionary, V1NoOpMigrator, SourceVersion (+5 more)

### Community 20 - "BatchPrintEngine"
Cohesion: 0.31
Nodes (8): BatchPrintEngine, CurrentCheckpoint, State, Action, CancellationToken, CancellationTokenSource, Task, TaskCompletionSource

### Community 21 - "IRibbonModeController"
Cohesion: 0.12
Nodes (15): DefaultRibbonModeController, ActiveMode, BlackPixelThreshold, HardwareVerificationNotes, RequiresRealHardwareVerification, ResinDensityAdjustment, IRibbonModeController, ActiveMode (+7 more)

### Community 22 - "IdpSmart31CardPrinterDriver"
Cohesion: 0.18
Nodes (12): IdpSmart31CardPrinterDriver, DisplayName, DriverId, RibbonController, StatusMonitor, TargetPrinterName, CancellationToken, SupportedOSPlatform (+4 more)

### Community 23 - "ImageSourceLoader"
Cohesion: 0.15
Nodes (10): ImageSourceLoader, SupportedExtensions, ImportedImage, Dictionary, Func, IReadOnlyList, SKBitmap, KeyValuePair (+2 more)

### Community 24 - "BatchRecordCheckpoint"
Cohesion: 0.17
Nodes (12): BatchRecordCheckpoint, BatchIndex, CompletedAtUtc, CorrelatedJobName, ErrorMessage, Label, RecordId, RetryCount (+4 more)

### Community 25 - "BarcodeLayer"
Cohesion: 0.17
Nodes (11): BarcodeLayer, ContentToken, IncludeText, IsPureBlackKResin, IsRequired, LayerType, Symbology, IEnumerable (+3 more)

### Community 26 - "MainWindowViewModelTests"
Cohesion: 0.16
Nodes (3): MainWindowViewModelTests, Fact, Task

### Community 27 - "PhotoLayer"
Cohesion: 0.12
Nodes (16): PhotoCropMode, AspectFill, AspectFit, Stretch, IImageLayer, Adjustments, BorderRadius, CropMode (+8 more)

### Community 28 - "ImageAdjustments"
Cohesion: 0.13
Nodes (14): ImageAdjustments, Brightness, Contrast, FlipHorizontal, FlipVertical, HasColorAdjustments, IsIdentityTransform, None (+6 more)

### Community 29 - "ColumnMapping"
Cohesion: 0.24
Nodes (8): ColumnMapping, Mappings, Dictionary, IReadOnlyDictionary, CsvDataIngestionService, IReadOnlyList, Stream, TextReader

### Community 30 - "PrinterStatus"
Cohesion: 0.15
Nodes (13): PrinterStatus, ActiveJobId, CardsPrintedSincePause, InputHopperCount, IsNativeStatus, IsReadyToPrint, OutputHopperCount, RequiresOperatorAttention (+5 more)

### Community 31 - "PrinterCapabilities"
Cohesion: 0.15
Nodes (15): PrinterCapabilities, DriverName, PrintableHeightMm, PrintableHeightPixels, PrintableWidthMm, PrintableWidthPixels, PrinterName, RawDriverProperties (+7 more)

### Community 32 - ".RenderCardAsync"
Cohesion: 0.18
Nodes (12): CardRenderer, CancellationToken, IReadOnlyDictionary, SKBitmap, SKCanvas, SKRect, Task, ICardRenderer (+4 more)

### Community 33 - "TextLayer"
Cohesion: 0.18
Nodes (10): TextLayer, Alignment, ColorHex, FontFamily, FontSize, IsPureBlackKResin, IsRequired, LayerType (+2 more)

### Community 34 - "Task"
Cohesion: 0.24
Nodes (3): CancellationTokenSource, List, Task

### Community 35 - "ValidationIssue"
Cohesion: 0.17
Nodes (11): ValidationIssue, BatchIndex, ErrorCode, Message, RecordIdentifier, RowNumber, Severity, TargetName (+3 more)

### Community 36 - ".SubmitCardAsync"
Cohesion: 0.14
Nodes (8): CancellationToken, Task, MockStatusMonitor, CurrentStatus, IsMonitoring, CancellationToken, Task, TimeSpan

### Community 37 - "IdpSmart31StatusMonitor"
Cohesion: 0.22
Nodes (5): IdpSmart31StatusMonitor, CurrentStatus, IsMonitoring, TimeSpan, Timer

### Community 38 - "ICardPrinterStatusMonitor"
Cohesion: 0.17
Nodes (7): ICardPrinterStatusMonitor, CurrentStatus, IsMonitoring, CancellationToken, Task, TimeSpan, IDisposable

### Community 39 - "TemplateDefinition"
Cohesion: 0.14
Nodes (15): TemplateDefinition, CreatedAtUtc, Id, Layers, Metadata, ModifiedAtUtc, Name, SchemaVersion (+7 more)

### Community 40 - "PrinterCapabilityMismatchException"
Cohesion: 0.14
Nodes (16): OperatorPauseRequiredException, CardsPrintedInBatch, MaxCardsBeforePause, PrinterCapabilityMismatchException, DriverCanvasHeight, DriverCanvasWidth, DriverDpiX, DriverDpiY (+8 more)

### Community 41 - ".CrashResume_SkipsAlreadyPrintedCards_AndResumesUnfinishedWork"
Cohesion: 0.42
Nodes (4): BatchPrintEngineTests, Fact, List, Task

### Community 42 - "SKPaint"
Cohesion: 0.21
Nodes (8): PhotoRenderer, IReadOnlyDictionary, SKBitmap, SKCanvas, SKRect, Stream, SKEncodedOrigin, SKPaint

### Community 43 - "CardPrintResult"
Cohesion: 0.14
Nodes (11): CancellationToken, Task, CardPrintResult, CompletedAtUtc, ErrorMessage, JobId, OperatorPauseTriggered, SpoolerJobId (+3 more)

### Community 44 - "LayerIconConverter"
Cohesion: 0.38
Nodes (5): LayerIconConverter, Type, CultureInfo, Geometry, IValueConverter

### Community 45 - "BatchExecutionSummary"
Cohesion: 0.20
Nodes (10): BatchExecutionSummary, BatchRunId, Checkpoint, CompletedCards, ElapsedTime, FailedCards, FinalState, TerminalMessage (+2 more)

### Community 46 - "TemplateLayer"
Cohesion: 0.06
Nodes (35): LayerSnapper, SnapResult, IEnumerable, List, IEnumerable, TemplateLayer, Height, Id (+27 more)

### Community 47 - "PrinterState"
Cohesion: 0.14
Nodes (12): SupportedOSPlatform, PrinterState, CoverOpen, Error, Offline, OperatorPauseRequired, OutOfCards, OutOfRibbon (+4 more)

### Community 48 - "CardPrintJob"
Cohesion: 0.17
Nodes (13): CardPrintJob, BackPixelBuffer, BatchIndex, Format, FrontPixelBuffer, IsDuplex, JobId, Label (+5 more)

### Community 49 - ".Draw"
Cohesion: 0.22
Nodes (6): SKBitmap, SKCanvas, SKRect, SKCanvas, SKRect, SKColorFilter

### Community 50 - ".OnPropertyChanged"
Cohesion: 0.16
Nodes (6): AvaloniaBitmap, ColumnMappingItem, CsvColumn, TemplateToken, ViewModelBase, INotifyPropertyChanged

### Community 51 - "BatchCardItem"
Cohesion: 0.15
Nodes (12): BatchCardItem, BackBitmap, BackPixelBuffer, BatchIndex, Format, FrontBitmap, FrontPixelBuffer, Label (+4 more)

### Community 52 - ".Render"
Cohesion: 0.32
Nodes (7): BarcodeRenderer, IReadOnlyDictionary, Regex, SKCanvas, SKRect, BarcodeFormat, BitMatrix

### Community 53 - "IBatchPrintEngine"
Cohesion: 0.39
Nodes (5): IBatchPrintEngine, CurrentCheckpoint, State, CancellationToken, Task

### Community 54 - ".MigrationPipeline_UpgradesLegacySchema_ToCurrentVersion"
Cohesion: 0.21
Nodes (8): JsonNode, TemplateSerializationTests, TestV1ToV2Migrator, SourceVersion, TargetVersion, Fact, JsonNode, InvalidOperationException

### Community 55 - "StaticImageLayer"
Cohesion: 0.20
Nodes (10): StaticImageLayer, Adjustments, Base64Data, BorderRadius, CropMode, HasImage, ImagePath, LayerType (+2 more)

### Community 56 - ".Render"
Cohesion: 0.31
Nodes (6): TextRenderer, IReadOnlyDictionary, Regex, SKCanvas, SKRect, SKFontStyleWeight

### Community 57 - "PreFlightValidationOptions"
Cohesion: 0.40
Nodes (4): PreFlightValidationOptions, AllowMissingPhotoAsWarning, CheckDuplicates, PrimaryKeyToken

### Community 58 - ".LoadAsync"
Cohesion: 0.31
Nodes (4): CancellationToken, Task, CancellationToken, Task

### Community 59 - "IdpSmart31StatusMonitor.cs"
Cohesion: 0.50
Nodes (4): DefaultNativeIdpStatusProvider, IsAvailable, INativeIdpStatusProvider, IsAvailable

### Community 60 - ".ComputeContentRect"
Cohesion: 0.33
Nodes (4): ImageLayout, SKRect, Height, Width

### Community 61 - "CardRendererTests"
Cohesion: 0.39
Nodes (3): CardRendererTests, Fact, Task

### Community 66 - "TextColorSwatch"
Cohesion: 0.67
Nodes (3): TextColorSwatch, Brush, IBrush

## Knowledge Gaps
- **436 isolated node(s):** `net10.0`, `Avalonia (12.1.2)`, `Avalonia.Desktop (12.1.2)`, `Avalonia.Themes.Fluent (12.1.2)`, `Avalonia.Fonts.Inter (12.1.2)` (+431 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 586 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **6 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `MainWindowViewModel` connect `MainWindowViewModel` to `BadgeForge.Core.Templates.Layers`, `MainWindow`, `BatchRecordDisplayItem`, `InteractiveBadgeCanvas`, `BatchExecutionEventArgs`, `.RequestLivePreviewUpdate`, `BatchPrintRequest`, `.ValidateBatch`, `CardFormat`, `ImageAdjustmentTests`, `BatchPrintEngine`, `ImageSourceLoader`, `BarcodeLayer`, `MainWindowViewModelTests`, `PhotoLayer`, `ImageAdjustments`, `ColumnMapping`, `.RenderCardAsync`, `TextLayer`, `Task`, `TemplateDefinition`, `TemplateLayer`, `.OnPropertyChanged`, `StaticImageLayer`, `.LoadAsync`, `.Pagination_WithLoadedData_NavigatesCorrectly`, `.ReplaceSelectedLayer`, `TextColorSwatch`?**
  _High betweenness centrality (0.550) - this node is a cross-community bridge._
- **Why does `ICardPrinterDriver` connect `BatchPrintRequest` to `BadgeForge.Core.Templates.Layers`, `.RenderCardAsync`, `MainWindowViewModel`, `MockCardPrinterDriver`, `ICardPrinterStatusMonitor`, `CardPrintResult`, `BatchPrintEngine`, `IRibbonModeController`, `IdpSmart31CardPrinterDriver`?**
  _High betweenness centrality (0.114) - this node is a cross-community bridge._
- **Why does `CardFormat` connect `CardFormat` to `MainWindowViewModel`, `TemplateDefinition`, `InteractiveBadgeCanvas`, `CardPrintJob`, `BatchCardItem`, `IdpSmart31CardPrinterDriver`, `CardFormatTests`, `PrinterCapabilities`?**
  _High betweenness centrality (0.109) - this node is a cross-community bridge._
- **Are the 23 inferred relationships involving `MainWindowViewModel` (e.g. with `.ViewModel_AddImageFromFile_EmbedsImageAndSizesFrameToAspect()` and `.ViewModel_ImageEdits_UpdateTemplateLayer()`) actually correct?**
  _`MainWindowViewModel` has 23 INFERRED edges - model-reasoned connections that need verification._
- **Are the 11 inferred relationships involving `TemplateDefinition` (e.g. with `.RenderCardAsync_QueriesCanvasSize_FromDriverProbedCapabilities()` and `.RenderCardAsync_ThrowsLoudly_WhenDriverCanvasMismatchesTemplateFormat()`) actually correct?**
  _`TemplateDefinition` has 11 INFERRED edges - model-reasoned connections that need verification._
- **What connects `net10.0`, `Avalonia (12.1.2)`, `Avalonia.Desktop (12.1.2)` to the rest of the system?**
  _436 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `BadgeForge.Core.Templates.Layers` be split into smaller, more focused modules?**
  _Cohesion score 0.05721168322794339 - nodes in this community are weakly interconnected._