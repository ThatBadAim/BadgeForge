using System.IO;
using BadgeForge.Core.Data.Models;
using BadgeForge.Core.Data.Services;
using Xunit;

namespace BadgeForge.Tests;

public class RosterServiceTests
{
    [Theory]
    [InlineData("/photos/jane_smith.jpg", "Jane", "Smith")]
    [InlineData("JOHN-PAUL JONES.png", "John Paul", "Jones")]
    [InlineData("McDonald_ronald.jpeg", "McDonald", "Ronald")]
    [InlineData("madonna.webp", "Madonna", "")]
    public void GuessNameFromFileName_TidiesFileNames(string path, string firstName, string lastName)
    {
        var (first, last) = RosterService.GuessNameFromFileName(path);

        Assert.Equal(firstName, first);
        Assert.Equal(lastName, last);
    }

    [Fact]
    public void SaveToText_ThenIngest_KeepsOrderFieldsAndPhotoPaths()
    {
        var records = new[]
        {
            new BadgeRecord { Fields = { ["FirstName"] = "Cy", ["LastName"] = "Moss, Jr." }, ResolvedPhotoPath = "/photos/cy.png" },
            new BadgeRecord { Fields = { ["FirstName"] = "Ann", ["LastName"] = "Able" } }
        };

        string csv = new RosterService().SaveToText(records, new[] { "FirstName", "LastName" });
        var loaded = new CsvDataIngestionService().IngestFromText(csv);

        Assert.Equal(2, loaded.Count);
        Assert.Equal("Cy", loaded[0].GetValue("FirstName"));
        Assert.Equal("Moss, Jr.", loaded[0].GetValue("LastName"));
        Assert.Equal("/photos/cy.png", loaded[0].GetValue(RosterService.PhotoColumn));
        Assert.Equal("Ann", loaded[1].GetValue("FirstName"));
        Assert.Equal(string.Empty, loaded[1].GetValue(RosterService.PhotoColumn));
    }

    [Fact]
    public void ResolvePhotoColumn_FindsAbsoluteAndRelativeFiles_AndIgnoresMissingOnes()
    {
        string dir = Directory.CreateTempSubdirectory("badgeforge-roster-").FullName;
        try
        {
            string relative = Path.Combine(dir, "a.png");
            string absolute = Path.Combine(dir, "b.png");
            File.WriteAllBytes(relative, new byte[] { 1 });
            File.WriteAllBytes(absolute, new byte[] { 1 });

            var records = new[]
            {
                new BadgeRecord { Fields = { ["Photo"] = "a.png" } },
                new BadgeRecord { Fields = { ["Photo"] = absolute } },
                new BadgeRecord { Fields = { ["Photo"] = "missing.png" } },
                new BadgeRecord()
            };

            int found = new RosterService().ResolvePhotoColumn(records, dir);

            Assert.Equal(2, found);
            Assert.Equal(relative, records[0].ResolvedPhotoPath);
            Assert.Equal(absolute, records[1].ResolvedPhotoPath);
            Assert.Null(records[2].ResolvedPhotoPath);
            Assert.Null(records[3].ResolvedPhotoPath);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
