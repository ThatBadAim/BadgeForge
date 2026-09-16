using BadgeForge.Core.Data.Models;
using BadgeForge.Core.Data.Validation;
using BadgeForge.Core.Templates.Enums;
using BadgeForge.Core.Templates.Layers;
using BadgeForge.Core.Templates.Models;
using Xunit;

namespace BadgeForge.Tests;

public class PreFlightPhotoAndCode39Tests
{
    [Fact]
    public void Validation_UsesTheRecordsChosenPhoto_AndNeverChangesIt()
    {
        string dir = Directory.CreateTempSubdirectory("badgeforge-preflight-").FullName;
        try
        {
            string photo = Path.Combine(dir, "chosen.png");
            File.WriteAllBytes(photo, new byte[] { 1 });

            var template = new TemplateDefinition { Layers = { new PhotoLayer { Name = "Photo", SourceToken = "{Photo}" } } };
            var record = new BadgeRecord
            {
                RowNumber = 2,
                Fields = new Dictionary<string, string> { ["Photo"] = photo },
                ResolvedPhotoPath = photo
            };

            // No photo folder configured: previously this wiped the record's photo path
            var report = new PreFlightValidationService().ValidateBatch(template, new List<BadgeRecord> { record });

            Assert.False(report.HasErrors);
            Assert.Equal(photo, record.ResolvedPhotoPath);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Code39_LowerCaseLettersAreInvalid()
    {
        var validator = new BarcodeValidator();

        Assert.True(validator.Validate("EMP-42", BarcodeSymbology.Code39).IsValid);

        var lowerCase = validator.Validate("emp-42", BarcodeSymbology.Code39);
        Assert.False(lowerCase.IsValid);
        Assert.Contains("upper-case", lowerCase.ErrorMessage);
    }
}
