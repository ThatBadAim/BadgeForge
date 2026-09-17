using BadgeForge.Core.Data.Models;
using BadgeForge.Core.Data.Services;
using Xunit;

namespace BadgeForge.Tests;

public class PhotoMatchingServiceTests
{
    [Fact]
    public void ResolvePhotoPath_MatchesCaseInsensitively_WithPrebuiltIndex()
    {
        string dir = Directory.CreateTempSubdirectory("badgeforge-photo-tests-").FullName;
        try
        {
            // On disk: uppercase extension and mixed-case filename
            string filePath = Path.Combine(dir, "Alice_Smith.JPG");
            File.WriteAllBytes(filePath, new byte[] { 1, 2, 3 });

            var service = new PhotoMatchingService();
            var index = service.BuildDirectoryIndex(dir);

            Assert.Single(index);
            Assert.True(index.ContainsKey("alice_smith.jpg"));

            var record = new BadgeRecord
            {
                RowNumber = 1,
                Fields = new Dictionary<string, string> { ["EmpId"] = "alice_smith" }
            };

            var options = new PhotoMatchingOptions
            {
                PhotoDirectory = dir,
                FilenamePattern = "{EmpId}.jpg"
            };

            var resolvedWithIndex = service.ResolvePhotoPath(record, options, index);
            Assert.NotNull(resolvedWithIndex);
            Assert.Equal(Path.GetFullPath(filePath), resolvedWithIndex);

            var resolvedWithoutIndex = service.ResolvePhotoPath(record, options);
            Assert.NotNull(resolvedWithoutIndex);
            Assert.Equal(Path.GetFullPath(filePath), resolvedWithoutIndex);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void ResolvePhotoPath_MatchesWildcardExtension()
    {
        string dir = Directory.CreateTempSubdirectory("badgeforge-photo-wildcard-").FullName;
        try
        {
            string filePath = Path.Combine(dir, "1001.png");
            File.WriteAllBytes(filePath, new byte[] { 1, 2, 3 });

            var service = new PhotoMatchingService();
            var index = service.BuildDirectoryIndex(dir);

            var record = new BadgeRecord
            {
                RowNumber = 1,
                Fields = new Dictionary<string, string> { ["Id"] = "1001" }
            };

            var options = new PhotoMatchingOptions
            {
                PhotoDirectory = dir,
                FilenamePattern = "{Id}.*",
                AllowedExtensions = new List<string> { ".jpg", ".jpeg", ".png" }
            };

            var resolved = service.ResolvePhotoPath(record, options, index);
            Assert.Equal(Path.GetFullPath(filePath), resolved);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void ResolvePhotoPath_ReturnsFallback_WhenNotFound()
    {
        string dir = Directory.CreateTempSubdirectory("badgeforge-photo-fallback-").FullName;
        try
        {
            var service = new PhotoMatchingService();
            var index = service.BuildDirectoryIndex(dir);

            var record = new BadgeRecord
            {
                RowNumber = 1,
                Fields = new Dictionary<string, string> { ["Id"] = "9999" }
            };

            var options = new PhotoMatchingOptions
            {
                PhotoDirectory = dir,
                FilenamePattern = "{Id}.jpg",
                FallbackImagePath = "/fallback.png"
            };

            var resolved = service.ResolvePhotoPath(record, options, index);
            Assert.Equal("/fallback.png", resolved);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
