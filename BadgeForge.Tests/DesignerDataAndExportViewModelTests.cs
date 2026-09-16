using BadgeForge.App.ViewModels;
using BadgeForge.Core.Templates.Enums;
using BadgeForge.Core.Templates.Layers;
using Xunit;

namespace BadgeForge.Tests;

public class DesignerDataAndExportViewModelTests
{
    private static (MainWindowViewModel Vm, TextLayer Layer) CreateWithText(string text)
    {
        var vm = new MainWindowViewModel();
        vm.NewBlankTemplate();
        vm.AddTextLayer(text: text);
        return (vm, Assert.IsType<TextLayer>(vm.SelectedLayer));
    }

    [Fact]
    public void InlineEdit_Commit_WritesTheTypedTextThroughAReplacedLayer()
    {
        var (vm, layer) = CreateWithText("Visitor");

        Assert.True(vm.BeginInlineTextEdit(layer));
        Assert.Same(layer, vm.InlineEditingLayer);
        Assert.Equal("Visitor", vm.InlineEditingText);

        vm.InlineEditingText = "Contractor";
        Assert.True(vm.CommitInlineTextEdit());

        Assert.False(vm.IsInlineTextEditing);
        var updated = Assert.IsType<TextLayer>(Assert.Single(vm.Template.Layers));
        Assert.Equal("Contractor", updated.Text);
        Assert.Equal("Visitor", layer.Text); // the original record was never mutated
        Assert.Same(updated, vm.SelectedLayer);
        Assert.Equal("Contractor", vm.TextContent);
    }

    [Fact]
    public void InlineEdit_Cancel_LeavesTheLayerUnchanged()
    {
        var (vm, layer) = CreateWithText("Visitor");
        vm.BeginInlineTextEdit(layer);
        vm.InlineEditingText = "Something else";

        vm.CancelInlineTextEdit();

        Assert.False(vm.IsInlineTextEditing);
        Assert.Same(layer, Assert.Single(vm.Template.Layers));
    }

    [Fact]
    public void InlineEdit_SelectingAnotherLayer_KeepsWhatWasTyped()
    {
        var (vm, first) = CreateWithText("First");
        vm.AddTextLayer(text: "Second");
        var second = vm.SelectedLayer!;

        vm.BeginInlineTextEdit(first);
        vm.InlineEditingText = "First, edited";
        vm.SelectedLayer = second;

        Assert.False(vm.IsInlineTextEditing);
        Assert.Contains(vm.Template.Layers, l => l is TextLayer { Text: "First, edited" });
        Assert.Same(second, vm.SelectedLayer);
    }

    [Fact]
    public void InlineEdit_RestylingMidEdit_KeepsTheSessionOnTheNewCopy()
    {
        var (vm, layer) = CreateWithText("Visitor");
        vm.BeginInlineTextEdit(layer);
        vm.InlineEditingText = "Visit";

        vm.TextFontSize = 20;

        Assert.True(vm.IsInlineTextEditing);
        Assert.Equal(20, vm.InlineEditingLayer!.FontSize);
        Assert.Equal("Visit", vm.InlineEditingText);
    }

    [Fact]
    public void InlineEdit_IsRefusedForLockedLayers_AndEndsWhenTheTemplateChanges()
    {
        var (vm, layer) = CreateWithText("Visitor");
        vm.ToggleLayerLock(layer);
        Assert.False(vm.BeginInlineTextEdit(Assert.IsType<TextLayer>(vm.Template.Layers[0])));

        vm.ToggleLayerLock(vm.Template.Layers[0]);
        Assert.True(vm.BeginInlineTextEdit(Assert.IsType<TextLayer>(vm.Template.Layers[0])));
        vm.NewTemplate();
        Assert.False(vm.IsInlineTextEditing);
    }

    [Fact]
    public void AddDataFieldLayer_InsertsTokenBoundLayers()
    {
        var vm = new MainWindowViewModel();
        vm.NewBlankTemplate();

        vm.AddDataFieldLayer(DataFieldKind.Text, "JobTitle");
        var text = Assert.IsType<TextLayer>(vm.SelectedLayer);
        Assert.Equal("{{JobTitle}}", text.Text);
        Assert.Equal("Job Title", text.Name);
        Assert.True(vm.HasSelectedLayerTokens);
        Assert.Equal("{{JobTitle}}", vm.SelectedLayerTokensText);
        Assert.Contains(vm.PeopleFieldColumns, c => c.Token == "JobTitle");

        vm.AddDataFieldLayer(DataFieldKind.Photo, "Photo");
        Assert.Equal("{{Photo}}", Assert.IsType<PhotoLayer>(vm.SelectedLayer).SourceToken);

        vm.AddDataFieldLayer(DataFieldKind.QrCode, "EmployeeId");
        var qr = Assert.IsType<BarcodeLayer>(vm.SelectedLayer);
        Assert.Equal(BarcodeSymbology.QrCode, qr.Symbology);
        Assert.Equal("{{EmployeeId}}", qr.ContentToken);
    }

    [Fact]
    public void TextOverflowIndex_ChangesHowLongTextIsHandled()
    {
        var (vm, _) = CreateWithText("A long job title");
        Assert.Equal((int)TextOverflowMode.ShrinkToFit, vm.TextOverflowIndex);

        vm.TextOverflowIndex = (int)TextOverflowMode.Wrap;
        Assert.Equal(TextOverflowMode.Wrap, Assert.IsType<TextLayer>(vm.SelectedLayer).Overflow);

        vm.TextOverflowIndex = -1; // a combo box rebuilding its selection
        Assert.Equal(TextOverflowMode.Wrap, Assert.IsType<TextLayer>(vm.SelectedLayer).Overflow);
    }

    [Fact]
    public void SampleData_ProvidesStandardCardholderTokens()
    {
        var fields = new MainWindowViewModel().GetActiveRecordFieldData();

        Assert.Equal("ALEXANDER CROSS", fields["FullName"]);
        Assert.False(string.IsNullOrEmpty(fields["JobTitle"]));
        Assert.False(string.IsNullOrEmpty(fields["ExpiryDate"]));
    }

    [Fact]
    public void BatchSelection_SelectAllInvertAndClear()
    {
        var vm = new MainWindowViewModel();
        var people = Enumerable.Range(0, 3).Select(_ => vm.AddPerson()).ToList();
        people[1].IsIncluded = false;

        vm.InvertPeopleSelection();
        Assert.Equal(new[] { false, true, false }, people.Select(p => p.IsIncluded));
        Assert.Equal("Export 1 badge to PDF…", vm.ExportPdfButtonText);

        vm.SelectAllPeople();
        Assert.All(people, p => Assert.True(p.IsIncluded));

        vm.ClearPeopleSelection();
        Assert.All(people, p => Assert.False(p.IsIncluded));
        Assert.False(vm.CanExportPdf);
    }

    [Fact]
    public async Task ImportRoster_Xlsx_FeedsStandardTokensFromAliasColumns()
    {
        string path = CardholderRosterTests.CreateWorkbook(
            sharedStrings: new[] { "Name", "Title", "Employee Id", "Grace Hopper", "Rear Admiral", "Alan Turing", "Cryptanalyst" },
            sheetRows: """
                <row r="1"><c r="A1" t="s"><v>0</v></c><c r="B1" t="s"><v>1</v></c><c r="C1" t="s"><v>2</v></c></row>
                <row r="2"><c r="A2" t="s"><v>3</v></c><c r="B2" t="s"><v>4</v></c><c r="C2"><v>1</v></c></row>
                <row r="3"><c r="A3" t="s"><v>5</v></c><c r="B3" t="s"><v>6</v></c><c r="C3"><v>2</v></c></row>
                """);
        try
        {
            var vm = new MainWindowViewModel();
            vm.NewBlankTemplate();
            vm.AddDataFieldLayer(DataFieldKind.Text, "JobTitle");

            await vm.ImportRosterAsync(path);

            Assert.Equal(2, vm.People.Count);
            var fields = vm.GetRecordFieldData(vm.People[1].Record);
            Assert.Equal("Cryptanalyst", fields["JobTitle"]);
            Assert.Equal("Alan Turing", fields["FullName"]);
            Assert.Equal("Turing", fields["LastName"]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ExportPdf_WritesOnlyTickedPeople_AndReportsTheOutcome()
    {
        string path = Path.Combine(Path.GetTempPath(), $"badgeforge-export-{Guid.NewGuid():N}.pdf");
        try
        {
            var vm = new MainWindowViewModel();
            var people = Enumerable.Range(1, 3).Select(n =>
            {
                var person = vm.AddPerson();
                person.Cells.Single(c => c.Token == "FirstName").Value = $"Person{n}";
                return person;
            }).ToList();
            people[1].IsIncluded = false;

            var result = await vm.ExportSelectedToPdfAsync(path);

            Assert.NotNull(result);
            Assert.Equal(2, result.CardCount);
            Assert.True(File.Exists(path));
            Assert.False(vm.IsExporting);
            Assert.Equal("Exported 2 badges", vm.ExportProgressText);
            Assert.Equal(100, vm.ExportProgressPercent);
            // The starter template has a photo frame and nobody has a photo: each exported badge is flagged
            Assert.Equal(2, result.Warnings.Count(w => w.Contains("photo", StringComparison.OrdinalIgnoreCase)));
            Assert.DoesNotContain(result.Warnings, w => w.Contains("Person2"));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
