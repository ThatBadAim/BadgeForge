using BadgeForge.Core.Data.Models;
using BadgeForge.Core.Data.Services;
using BadgeForge.Core.Data.Validation;
using BadgeForge.Core.Printing.Drivers;
using BadgeForge.Core.Printing.Models;
using BadgeForge.Core.Templates.Enums;
using BadgeForge.Core.Templates.Layers;
using BadgeForge.Core.Templates.Models;

namespace BadgeForge.Tests;

public class PreFlightValidationTests
{
    private readonly CsvDataIngestionService _csvIngestionService = new();
    private readonly PreFlightValidationService _validationService = new();

    [Fact]
    public void CsvIngestion_WithColumnMapping_MapsHeadersToTemplateTokens()
    {
        // Arrange
        var csvData = """
        Emp ID,Full Name,Department,Photo File
        1001,Jane Doe,Engineering,jane_doe.jpg
        1002,John Smith,Design,john_smith.jpg
        """;

        var mapping = new ColumnMapping()
            .Map("Emp ID", "EmployeeId")
            .Map("Full Name", "FullName")
            .Map("Photo File", "Photo");

        // Act
        var records = _csvIngestionService.IngestFromText(csvData, mapping);

        // Assert
        Assert.Equal(2, records.Count);

        var first = records[0];
        Assert.Equal("1001", first.GetValue("EmployeeId"));
        Assert.Equal("Jane Doe", first.GetValue("FullName"));
        Assert.Equal("jane_doe.jpg", first.GetValue("Photo"));
        Assert.Equal("Engineering", first.GetValue("Department")); // unmapped column preserved

        var second = records[1];
        Assert.Equal("1002", second.GetValue("EmployeeId"));
        Assert.Equal("John Smith", second.GetValue("FullName"));
    }

    [Fact]
    public void PreFlight_CatchesMissingOrBlankRequiredFields()
    {
        // Arrange
        var template = new TemplateDefinition
        {
            Name = "ID Card",
            TargetFormat = CardFormat.CR80,
            Layers = new List<TemplateLayer>
            {
                new TextLayer
                {
                    Name = "First Name",
                    Text = "{FirstName}",
                    IsRequired = true
                },
                new TextLayer
                {
                    Name = "Last Name",
                    Text = "{LastName}",
                    IsRequired = true
                }
            }
        };

        var records = new List<BadgeRecord>
        {
            new()
            {
                RowNumber = 2,
                Fields = new Dictionary<string, string> { { "FirstName", "Alice" }, { "LastName", "Smith" } }
            },
            new()
            {
                RowNumber = 3,
                Fields = new Dictionary<string, string> { { "FirstName", "Bob" }, { "LastName", "  " } } // Blank LastName
            },
            new()
            {
                RowNumber = 4,
                Fields = new Dictionary<string, string> { { "LastName", "Johnson" } } // Missing FirstName
            }
        };

        // Act
        var report = _validationService.ValidateBatch(template, records);

        // Assert
        Assert.True(report.HasErrors);
        Assert.Equal(2, report.ErrorCount);

        var missingFieldIssues = report.MissingFields;
        Assert.Equal(2, missingFieldIssues.Count);

        Assert.Contains(missingFieldIssues, i => i.RowNumber == 3 && i.Message.Contains("LastName"));
        Assert.Contains(missingFieldIssues, i => i.RowNumber == 4 && i.Message.Contains("FirstName"));
    }

    [Fact]
    public void PreFlight_CatchesMissingPhotoFiles()
    {
        // Arrange
        var tempPhotoDir = Path.Combine(Path.GetTempPath(), $"badgeforge_photos_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempPhotoDir);

        try
        {
            // Create a valid photo for record 1001 only
            var validPhotoPath = Path.Combine(tempPhotoDir, "1001.jpg");
            File.WriteAllText(validPhotoPath, "fake image bytes");

            var template = new TemplateDefinition
            {
                Name = "Photo Badge",
                TargetFormat = CardFormat.CR80,
                Layers = new List<TemplateLayer>
                {
                    new PhotoLayer
                    {
                        Name = "Portrait",
                        SourceToken = "{EmployeeId}",
                        IsRequired = true
                    }
                }
            };

            var records = new List<BadgeRecord>
            {
                new()
                {
                    RowNumber = 2,
                    Fields = new Dictionary<string, string> { { "EmployeeId", "1001" } }
                },
                new()
                {
                    RowNumber = 3,
                    Fields = new Dictionary<string, string> { { "EmployeeId", "1002" } } // Missing file
                }
            };

            var photoOptions = new PhotoMatchingOptions
            {
                PhotoDirectory = tempPhotoDir,
                FilenamePattern = "{EmployeeId}.jpg"
            };

            // Act
            var report = _validationService.ValidateBatch(template, records, photoOptions);

            // Assert
            Assert.True(report.HasErrors);
            var missingPhotos = report.MissingPhotos;
            Assert.Single(missingPhotos);
            Assert.Equal(3, missingPhotos[0].RowNumber);
            Assert.Equal(ValidationSeverity.Error, missingPhotos[0].Severity);
            Assert.Equal(PreFlightErrorCodes.MissingPhotoFile, missingPhotos[0].ErrorCode);
        }
        finally
        {
            if (Directory.Exists(tempPhotoDir))
                Directory.Delete(tempPhotoDir, true);
        }
    }

    [Fact]
    public void PreFlight_CatchesBarcodeValidity_PerSymbology()
    {
        // Arrange
        var template = new TemplateDefinition
        {
            Name = "Barcode Badge",
            TargetFormat = CardFormat.CR80,
            Layers = new List<TemplateLayer>
            {
                new BarcodeLayer
                {
                    Name = "Code39 Badge",
                    ContentToken = "{BarcodeVal}",
                    Symbology = BarcodeSymbology.Code39,
                    IsRequired = true
                }
            }
        };

        var records = new List<BadgeRecord>
        {
            new()
            {
                RowNumber = 2,
                Fields = new Dictionary<string, string> { { "BarcodeVal", "VALID-123" } } // Valid Code 39
            },
            new()
            {
                RowNumber = 3,
                Fields = new Dictionary<string, string> { { "BarcodeVal", "INVALID#SYMBOL" } } // '#' is invalid in Code 39
            }
        };

        // Act
        var report = _validationService.ValidateBatch(template, records);

        // Assert
        Assert.True(report.HasErrors);
        var barcodeIssues = report.InvalidBarcodes;
        Assert.Single(barcodeIssues);
        Assert.Equal(3, barcodeIssues[0].RowNumber);
        Assert.Equal(PreFlightErrorCodes.InvalidBarcode, barcodeIssues[0].ErrorCode);
        Assert.Contains("Code 39", barcodeIssues[0].Message);
    }

    [Fact]
    public void PreFlight_CatchesEan13CheckDigitMismatch()
    {
        // Arrange
        var template = new TemplateDefinition
        {
            Name = "EAN13 Badge",
            TargetFormat = CardFormat.CR80,
            Layers = new List<TemplateLayer>
            {
                new BarcodeLayer
                {
                    Name = "Ean13 Layer",
                    ContentToken = "{EanCode}",
                    Symbology = BarcodeSymbology.Ean13,
                    IsRequired = true
                }
            }
        };

        // Standard EAN-13 valid: "4006381333931"
        // Corrupted check digit: "4006381333939"
        var records = new List<BadgeRecord>
        {
            new()
            {
                RowNumber = 2,
                Fields = new Dictionary<string, string> { { "EanCode", "4006381333931" } } // Valid check digit
            },
            new()
            {
                RowNumber = 3,
                Fields = new Dictionary<string, string> { { "EanCode", "4006381333939" } } // Invalid check digit
            }
        };

        // Act
        var report = _validationService.ValidateBatch(template, records);

        // Assert
        Assert.True(report.HasErrors);
        var barcodeIssues = report.InvalidBarcodes;
        Assert.Single(barcodeIssues);
        Assert.Equal(3, barcodeIssues[0].RowNumber);
        Assert.Contains("check digit mismatch", barcodeIssues[0].Message);
    }

    [Fact]
    public void PreFlight_CatchesDuplicateBarcodesAndIdentifiers_AcrossBatch()
    {
        // Arrange
        var template = new TemplateDefinition
        {
            Name = "ID Badge",
            TargetFormat = CardFormat.CR80,
            Layers = new List<TemplateLayer>
            {
                new BarcodeLayer
                {
                    Name = "Barcode",
                    ContentToken = "{BadgeNumber}",
                    Symbology = BarcodeSymbology.Code128,
                    IsRequired = true
                }
            }
        };

        var records = new List<BadgeRecord>
        {
            new()
            {
                RowNumber = 2,
                Fields = new Dictionary<string, string> { { "BadgeNumber", "BADGE-100" } }
            },
            new()
            {
                RowNumber = 3,
                Fields = new Dictionary<string, string> { { "BadgeNumber", "BADGE-200" } }
            },
            new()
            {
                RowNumber = 4,
                Fields = new Dictionary<string, string> { { "BadgeNumber", "BADGE-100" } } // Duplicate of Row 2!
            }
        };

        // Act
        var report = _validationService.ValidateBatch(template, records);

        // Assert
        Assert.True(report.HasErrors);
        var duplicates = report.DuplicateIdentifiers;
        Assert.NotEmpty(duplicates);
        Assert.Contains(duplicates, d => d.RowNumber == 4 && d.Message.Contains("Row 2"));
    }

    [Fact]
    public void ExecutionGate_BlocksOnCriticalErrors_EvenWithBypassToggle()
    {
        // Arrange - Report with critical errors
        var reportWithErrors = new PreFlightValidationReport
        {
            TotalRecords = 2,
            Issues = new List<ValidationIssue>
            {
                new()
                {
                    Severity = ValidationSeverity.Error,
                    ErrorCode = PreFlightErrorCodes.MissingRequiredField,
                    Message = "Required field missing."
                }
            }
        };

        // Act & Assert
        Assert.False(reportWithErrors.CanProceed(bypassWarnings: false));
        // Critical error CANNOT be bypassed
        Assert.False(reportWithErrors.CanProceed(bypassWarnings: true));

        var ex = Assert.Throws<PreFlightValidationBlockedException>(() =>
            reportWithErrors.AssertCanProceed(bypassWarnings: true));
        Assert.Contains("cannot be bypassed", ex.Message);
    }

    [Fact]
    public void ExecutionGate_AllowsWarningsOnly_WhenExplicitBypassToggled()
    {
        // Arrange - Report with only non-critical warnings
        var reportWithWarningsOnly = new PreFlightValidationReport
        {
            TotalRecords = 2,
            Issues = new List<ValidationIssue>
            {
                new()
                {
                    Severity = ValidationSeverity.Warning,
                    ErrorCode = PreFlightErrorCodes.MissingPhotoFile,
                    Message = "Optional photo missing, fallback placeholder used."
                }
            }
        };

        // Act & Assert
        Assert.False(reportWithWarningsOnly.CanProceed(bypassWarnings: false)); // Blocks without explicit bypass
        Assert.True(reportWithWarningsOnly.CanProceed(bypassWarnings: true));  // Proceeds with explicit bypass

        // Verify AssertCanProceed behaviour
        Assert.Throws<PreFlightValidationBlockedException>(() =>
            reportWithWarningsOnly.AssertCanProceed(bypassWarnings: false));

        // When bypassed, AssertCanProceed passes cleanly without throwing
        reportWithWarningsOnly.AssertCanProceed(bypassWarnings: true);
    }

    [Fact]
    public async Task FullWorkflow_UsingMockPrinterDriverAndCR80Format_ValidatesBatchBeforeSubmission()
    {
        // Arrange
        using var mockDriver = new MockCardPrinterDriver();
        var cr80Format = CardFormat.CR80;

        var template = new TemplateDefinition
        {
            Name = "Conference Pass",
            TargetFormat = cr80Format,
            Layers = new List<TemplateLayer>
            {
                new TextLayer
                {
                    Name = "Attendee Name",
                    Text = "{FullName}",
                    IsRequired = true
                },
                new BarcodeLayer
                {
                    Name = "Badge Barcode",
                    ContentToken = "{BadgeId}",
                    Symbology = BarcodeSymbology.Code128,
                    IsRequired = true
                }
            }
        };

        var validRecords = new List<BadgeRecord>
        {
            new()
            {
                RowNumber = 2,
                Fields = new Dictionary<string, string> { { "FullName", "Dr. Jane Smith" }, { "BadgeId", "CONF-001" } }
            },
            new()
            {
                RowNumber = 3,
                Fields = new Dictionary<string, string> { { "FullName", "Alex Taylor" }, { "BadgeId", "CONF-002" } }
            }
        };

        // Act
        var report = _validationService.ValidateBatch(template, validRecords);

        // Assert
        Assert.False(report.HasErrors);
        Assert.False(report.HasWarnings);
        Assert.True(report.CanProceed());

        // Driver capability compatibility check with target format
        var capabilities = await mockDriver.ProbeCapabilitiesAsync("Mock SMART-31");
        bool isCompatible = capabilities.ValidateCompatibility(template.TargetFormat, out var mismatchReason);
        Assert.True(isCompatible, mismatchReason);
    }
}
