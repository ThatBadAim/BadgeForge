using System.Text.Json;
using System.Text.Json.Nodes;
using BadgeForge.Core.Templates.Migration;
using BadgeForge.Core.Templates.Models;
using BadgeForge.Core.Templates.Storage;
using Xunit;

namespace BadgeForge.Tests;

public class TemplateStorageRobustnessTests
{
    [Theory]
    [InlineData("1")]
    [InlineData("\"1\"")]
    [InlineData("1.0")]
    public void ExtractSchemaVersion_AcceptsAWholeNumberWrittenAnyWay(string versionJson)
    {
        var node = JsonNode.Parse($"{{\"SchemaVersion\": {versionJson}}}")!;

        Assert.Equal(1, TemplateMigrationPipeline.ExtractSchemaVersion(node));
    }

    [Fact]
    public void ExtractSchemaVersion_RejectsText_WithAClearError()
    {
        var node = JsonNode.Parse("{\"SchemaVersion\": \"one\"}")!;

        var ex = Assert.Throws<JsonException>(() => TemplateMigrationPipeline.ExtractSchemaVersion(node));
        Assert.Contains("SchemaVersion", ex.Message);
    }

    [Fact]
    public async Task Save_ReplacesTheWholeFile_AndLeavesNoTemporaryFiles()
    {
        string dir = Directory.CreateTempSubdirectory("badgeforge-templates-").FullName;
        try
        {
            string path = Path.Combine(dir, "badge.json");
            await File.WriteAllTextAsync(path, new string('x', 200_000)); // a larger, older file

            var storage = new JsonTemplateStorage();
            await storage.SaveAsync(new TemplateDefinition { Name = "Visitor Pass" }, path);

            Assert.Equal("Visitor Pass", storage.Load(path).Name);
            Assert.Equal(new[] { path }, Directory.GetFiles(dir));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
