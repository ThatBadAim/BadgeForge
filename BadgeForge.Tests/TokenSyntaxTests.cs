using BadgeForge.Core.Templates.Layers;
using BadgeForge.Core.Templates.Tokens;
using Xunit;

namespace BadgeForge.Tests;

public class TokenSyntaxTests
{
    [Theory]
    [InlineData("{{FullName}}", new[] { "FullName" })]
    [InlineData("{{ FullName }}", new[] { "FullName" })]
    [InlineData("{FirstName} {LastName}", new[] { "FirstName", "LastName" })]
    [InlineData("ID: {{EmployeeId}} / {Department}", new[] { "EmployeeId", "Department" })]
    [InlineData("No tokens here", new string[0])]
    public void Extract_ReadsDoubleAndLegacySingleBraces(string text, string[] expected)
    {
        Assert.Equal(expected, TokenSyntax.Extract(text).ToArray());
    }

    [Fact]
    public void Replace_DoubleBraces_LeavesNoStrayBraces()
    {
        var fields = new Dictionary<string, string> { ["FullName"] = "Ada Lovelace" };

        Assert.Equal("Name: Ada Lovelace", TokenSyntax.Replace("Name: {{FullName}}", fields));
    }

    [Fact]
    public void Replace_UnknownToken_KeepsRawText()
    {
        Assert.Equal("{{JobTitle}}", TokenSyntax.Replace("{{JobTitle}}", new Dictionary<string, string>()));
    }

    [Fact]
    public void Replace_IsSinglePass_BracesInValuesArePrintedLiterally()
    {
        var fields = new Dictionary<string, string>
        {
            ["FullName"] = "{{Secret}}",
            ["Secret"] = "leaked"
        };

        Assert.Equal("{{Secret}}", TokenSyntax.Replace("{{FullName}}", fields));
    }

    [Fact]
    public void SanitizeValue_StripsControlCharactersCollapsesWhitespaceAndCapsLength()
    {
        Assert.Equal("Ada Lovelace", TokenSyntax.SanitizeValue("  Ada\t\r\n\0 Lovelace  "));
        Assert.Equal(TokenSyntax.MaxValueLength, TokenSyntax.SanitizeValue(new string('x', 5000)).Length);
    }

    [Fact]
    public void SanitizeValue_NeverSplitsASurrogatePairAtTheLengthCap()
    {
        string value = new string('x', TokenSyntax.MaxValueLength - 1) + "😀";

        string sanitized = TokenSyntax.SanitizeValue(value);

        Assert.False(char.IsHighSurrogate(sanitized[^1]));
    }

    [Fact]
    public void Layers_ReportDynamicTokens()
    {
        Assert.True(new TextLayer { Text = "{{FullName}}" }.HasDynamicTokens);
        Assert.False(new TextLayer { Text = "VISITOR" }.HasDynamicTokens);
        Assert.Equal(new[] { "Photo" }, new PhotoLayer { SourceToken = "{{Photo}}" }.GetReferencedTokens());
    }

    [Theory]
    [InlineData("{{ FullName }}", "FullName")]
    [InlineData("{Photo}", "Photo")]
    [InlineData("Department", "Department")]
    public void NormalizeName_StripsBracesAndWhitespace(string input, string expected)
    {
        Assert.Equal(expected, TokenSyntax.NormalizeName(input));
        Assert.Equal("{{" + expected + "}}", TokenSyntax.Format(input));
    }
}
