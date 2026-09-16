using BadgeForge.Core.Printing.Models;
using BadgeForge.Core.Templates.Enums;
using BadgeForge.Core.Templates.Layers;
using BadgeForge.Core.Templates.Migration;
using BadgeForge.Core.Templates.Models;
using BadgeForge.Core.Templates.Registry;
using BadgeForge.Core.Templates.Storage;
using System.Text.Json.Nodes;

namespace BadgeForge.Tests;

public class TemplateSerializationTests
{
    [Fact]
    public void Template_WithAllBuiltInLayers_RoundTripsThroughSaveAndLoad()
    {
        // Arrange
        var storage = new JsonTemplateStorage();
        var tempFile = Path.Combine(Path.GetTempPath(), $"badgeforge_test_{Guid.NewGuid():N}.json");

        try
        {
            var original = new TemplateDefinition
            {
                Name = "Employee Security Pass 2026",
                TargetFormat = CardFormat.CR80,
                Layers = new List<TemplateLayer>
                {
                    new TextLayer
                    {
                        Name = "Full Name",
                        Text = "{FirstName} {LastName}",
                        FontFamily = "Inter",
                        FontSize = 14.0,
                        FontWeight = "Bold",
                        ColorHex = "#000000",
                        Alignment = TextAlignment.Center,
                        IsPureBlackKResin = true,
                        IsRequired = true,
                        X = 10,
                        Y = 20,
                        Width = 65,
                        Height = 8,
                        ZIndex = 2
                    },
                    new PhotoLayer
                    {
                        Name = "Portrait Photo",
                        SourceToken = "{Photo}",
                        BorderRadius = 4.0,
                        CropMode = PhotoCropMode.AspectFill,
                        IsRequired = true,
                        X = 10,
                        Y = 5,
                        Width = 25,
                        Height = 35,
                        ZIndex = 1
                    },
                    new StaticImageLayer
                    {
                        Name = "Company Logo",
                        ImagePath = "assets/logo.png",
                        MaintainAspectRatio = true,
                        Opacity = 0.95,
                        X = 60,
                        Y = 5,
                        Width = 20,
                        Height = 15,
                        ZIndex = 3
                    },
                    new BarcodeLayer
                    {
                        Name = "Access Barcode",
                        ContentToken = "EMP-{EmployeeId}",
                        Symbology = BarcodeSymbology.Code128,
                        IncludeText = true,
                        IsPureBlackKResin = true,
                        IsRequired = true,
                        X = 10,
                        Y = 40,
                        Width = 65,
                        Height = 10,
                        ZIndex = 4
                    }
                }
            };

            // Act - Save to disk and re-load
            storage.Save(original, tempFile);
            var loaded = storage.Load(tempFile);

            // Assert
            Assert.NotNull(loaded);
            Assert.Equal(original.Name, loaded.Name);
            Assert.Equal(TemplateDefinition.CurrentSchemaVersion, loaded.SchemaVersion);

            // Assert Target CardFormat preservation
            Assert.NotNull(loaded.TargetFormat);
            Assert.Equal(CardFormat.CR80.Name, loaded.TargetFormat.Name);
            Assert.Equal(CardFormat.CR80.WidthMm, loaded.TargetFormat.WidthMm);
            Assert.Equal(CardFormat.CR80.HeightMm, loaded.TargetFormat.HeightMm);
            Assert.Equal(CardFormat.CR80.Dpi, loaded.TargetFormat.Dpi);
            Assert.Equal(CardFormat.CR80.WidthPixels, loaded.TargetFormat.WidthPixels);
            Assert.Equal(CardFormat.CR80.HeightPixels, loaded.TargetFormat.HeightPixels);

            // Assert Layers
            Assert.Equal(4, loaded.Layers.Count);

            var textLayer = Assert.IsType<TextLayer>(loaded.Layers[0]);
            Assert.Equal("{FirstName} {LastName}", textLayer.Text);
            Assert.True(textLayer.IsPureBlackKResin);
            Assert.Equal(TextAlignment.Center, textLayer.Alignment);

            var photoLayer = Assert.IsType<PhotoLayer>(loaded.Layers[1]);
            Assert.Equal("{Photo}", photoLayer.SourceToken);
            Assert.Equal(PhotoCropMode.AspectFill, photoLayer.CropMode);
            Assert.Equal(4.0, photoLayer.BorderRadius);

            var staticImageLayer = Assert.IsType<StaticImageLayer>(loaded.Layers[2]);
            Assert.Equal("assets/logo.png", staticImageLayer.ImagePath);
            Assert.Equal(0.95, staticImageLayer.Opacity);

            var barcodeLayer = Assert.IsType<BarcodeLayer>(loaded.Layers[3]);
            Assert.Equal("EMP-{EmployeeId}", barcodeLayer.ContentToken);
            Assert.Equal(BarcodeSymbology.Code128, barcodeLayer.Symbology);
            Assert.True(barcodeLayer.IsPureBlackKResin);

            // Assert Token extraction
            var allTokens = loaded.GetAllReferencedTokens();
            Assert.Contains("FirstName", allTokens);
            Assert.Contains("LastName", allTokens);
            Assert.Contains("Photo", allTokens);
            Assert.Contains("EmployeeId", allTokens);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public void CustomLayer_CanBeRegisteredAndDeserialized_WithoutTouchingCoreCode()
    {
        // Arrange - Define a new layer type in user code
        var customRegistry = new LayerRegistry();
        customRegistry.RegisterDefaultBuiltInLayers();
        customRegistry.Register<SignatureCaptureLayer>(SignatureCaptureLayer.TypeDiscriminator);

        var storage = new JsonTemplateStorage(layerRegistry: customRegistry);

        var original = new TemplateDefinition
        {
            Name = "Contractor Badge with Signature",
            TargetFormat = CardFormat.CR80,
            Layers = new List<TemplateLayer>
            {
                new TextLayer { Name = "Title", Text = "Contractor Pass" },
                new SignatureCaptureLayer
                {
                    Name = "Signee",
                    PenColorHex = "#000080",
                    SignatureToken = "{SignData}",
                    Width = 40,
                    Height = 15
                }
            }
        };

        // Act
        var json = storage.Serialize(original);
        var deserialized = storage.Deserialize(json);

        // Assert
        Assert.Equal(2, deserialized.Layers.Count);
        Assert.IsType<TextLayer>(deserialized.Layers[0]);
        var signatureLayer = Assert.IsType<SignatureCaptureLayer>(deserialized.Layers[1]);
        Assert.Equal("#000080", signatureLayer.PenColorHex);
        Assert.Equal("{SignData}", signatureLayer.SignatureToken);
    }

    [Fact]
    public void MigrationPipeline_UpgradesLegacySchema_ToCurrentVersion()
    {
        // Arrange
        var pipeline = new TemplateMigrationPipeline();

        // Register a migration from v0 (or v1) to v2
        pipeline.Register(new TestV1ToV2Migrator());

        var json = """
        {
            "Name": "Legacy v1 Template",
            "SchemaVersion": 1,
            "TargetFormat": { "Name": "CR80", "WidthMm": 85.6, "HeightMm": 53.98, "Dpi": 300 },
            "Layers": []
        }
        """;

        var root = JsonNode.Parse(json)!;

        // Act
        var migrated = pipeline.MigrateTo(root, targetVersion: 2);

        // Assert
        Assert.Equal(2, TemplateMigrationPipeline.ExtractSchemaVersion(migrated));
        Assert.True(migrated.AsObject().ContainsKey("MigratedByV2"));
    }

    // Custom Layer for extensibility testing
    private record SignatureCaptureLayer : TemplateLayer
    {
        public const string TypeDiscriminator = "SignatureCapture";
        public override string LayerType => TypeDiscriminator;

        public string PenColorHex { get; init; } = "#000000";
        public string SignatureToken { get; init; } = "{Signature}";

        public override IEnumerable<string> GetReferencedTokens()
        {
            return ExtractTokensFromString(SignatureToken);
        }
    }

    // Test migrator simulating schema evolution
    private class TestV1ToV2Migrator : ITemplateMigrator
    {
        public int SourceVersion => 1;
        public int TargetVersion => 2;

        public JsonNode Migrate(JsonNode rootNode)
        {
            if (rootNode is JsonObject obj)
            {
                obj["MigratedByV2"] = true;
                obj["SchemaVersion"] = 2;
            }
            return rootNode;
        }
    }
}
