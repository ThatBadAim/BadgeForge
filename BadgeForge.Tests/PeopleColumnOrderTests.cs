using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BadgeForge.App.ViewModels;
using BadgeForge.Core.Data.Models;
using BadgeForge.Core.Data.Services;
using BadgeForge.Core.Rendering.Batch;
using BadgeForge.Core.Templates.Layers;
using Xunit;

namespace BadgeForge.Tests;

/// <summary>
/// Column order is display state: moving a column must never rename a field, move a value or change what a badge
/// prints.
/// </summary>
public class PeopleColumnOrderTests
{
    // --- The layout itself ---

    [Fact]
    public void Arrange_WithoutAMove_KeepsTheTemplateOrder()
    {
        var layout = new PeopleColumnLayout();
        var tokens = new[] { "FirstName", "LastName", "Department" };

        Assert.Equal(tokens, layout.Arrange(tokens));
        Assert.False(layout.IsCustomised);
    }

    [Fact]
    public void Move_ReordersAndIsRemembered()
    {
        var layout = new PeopleColumnLayout();
        var tokens = new[] { "FirstName", "LastName", "Department" };

        var move = layout.Move(tokens, "Department", 0);

        Assert.Equal((2, 0), move);
        Assert.True(layout.IsCustomised);
        Assert.Equal(new[] { "Department", "FirstName", "LastName" }, layout.Arrange(tokens));
    }

    [Fact]
    public void Move_OffTheEnds_ClampsAndReportsNoMoveWhenNothingChanges()
    {
        var layout = new PeopleColumnLayout();
        var tokens = new[] { "A", "B", "C" };

        Assert.Equal((0, 2), layout.Move(tokens, "A", 99));
        Assert.Equal(new[] { "B", "C", "A" }, layout.Arrange(tokens));
        Assert.Null(layout.Move(tokens, "A", 99));
        Assert.Null(layout.Move(tokens, "Nope", 0));
    }

    [Fact]
    public void Arrange_NewColumnsGoToTheEndAndTheSetIsNeverChanged()
    {
        var layout = new PeopleColumnLayout();
        layout.Move(new[] { "A", "B", "C" }, "C", 0);

        var arranged = layout.Arrange(new[] { "A", "B", "C", "D" });

        Assert.Equal(new[] { "C", "A", "B", "D" }, arranged);
        Assert.Equal(new[] { "A", "B", "C", "D" }.OrderBy(t => t), arranged.OrderBy(t => t));
    }

    [Fact]
    public void Arrange_AColumnThatComesBackReturnsToWhereTheUserLeftIt()
    {
        // Editing a layer's text makes its token vanish and reappear; that shouldn't shuffle the grid
        var layout = new PeopleColumnLayout();
        layout.Move(new[] { "A", "B", "C" }, "C", 0);

        Assert.Equal(new[] { "C", "A" }, layout.Arrange(new[] { "A", "C" }));
        Assert.Equal(new[] { "C", "A", "B" }, layout.Arrange(new[] { "A", "B", "C" }));
    }

    [Fact]
    public void Reset_GoesBackToTheTemplateOrder()
    {
        var layout = new PeopleColumnLayout();
        layout.Move(new[] { "A", "B", "C" }, "C", 0);

        layout.Reset();

        Assert.False(layout.IsCustomised);
        Assert.Equal(new[] { "A", "B", "C" }, layout.Arrange(new[] { "A", "B", "C" }));
    }

    // --- The People grid ---

    [Fact]
    public void MovePeopleColumn_MovesTheHeadingAndEveryRowTogether()
    {
        var vm = new MainWindowViewModel();
        var person = AddPerson(vm, ("FirstName", "Ada"), ("LastName", "Lovelace"), ("Department", "Analytics"));
        var last = vm.PeopleFieldColumns.Single(c => c.Token == "LastName");

        Assert.True(vm.MovePeopleColumn(last, 0));

        Assert.Equal("LastName", vm.PeopleFieldColumns[0].Token);
        Assert.True(vm.IsPeopleColumnOrderCustomised);

        // Headings and cells stay lined up, and each cell keeps the value it always had
        Assert.Equal(vm.PeopleFieldColumns.Select(c => c.Token), person.Cells.Select(c => c.Token));
        Assert.Equal("Lovelace", person.Cells[0].Value);
        Assert.Equal("Ada", person.Cells.Single(c => c.Token == "FirstName").Value);
    }

    [Fact]
    public void MovePeopleColumn_LeavesTheRecordSchemaAlone()
    {
        var vm = new MainWindowViewModel();
        var person = AddPerson(vm, ("FirstName", "Ada"), ("LastName", "Lovelace"), ("Department", "Analytics"));
        var fieldsBefore = new Dictionary<string, string>(person.Record.Fields, StringComparer.OrdinalIgnoreCase);
        var keysBefore = person.Cells.ToDictionary(c => c.Token, c => c.Key, StringComparer.OrdinalIgnoreCase);

        vm.MovePeopleColumn(vm.PeopleFieldColumns[^1], 0);
        vm.MovePeopleColumn(vm.PeopleFieldColumns[^1], 1);

        // No field renamed, added, dropped or re-valued, and every cell still reads from the field it did before
        Assert.Equal(fieldsBefore.OrderBy(f => f.Key), person.Record.Fields.OrderBy(f => f.Key));
        Assert.Equal(keysBefore, person.Cells.ToDictionary(c => c.Token, c => c.Key, StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public void MovePeopleColumn_LeavesTemplateBindingsAndTheRenderedBadgeUnchanged()
    {
        var vm = new MainWindowViewModel();
        var person = AddPerson(vm, ("FirstName", "Ada"), ("LastName", "Lovelace"), ("Department", "Analytics"), ("EmployeeId", "1001"));

        var tokensBefore = vm.Template.GetAllReferencedTokens().OrderBy(t => t).ToList();
        var boundBefore = BoundText(vm, person);

        vm.MovePeopleColumn(vm.PeopleFieldColumns.Single(c => c.Token == "EmployeeId"), 0);
        vm.MovePeopleColumn(vm.PeopleFieldColumns.Single(c => c.Token == "FirstName"), vm.PeopleFieldColumns.Count - 1);

        Assert.Equal(tokensBefore, vm.Template.GetAllReferencedTokens().OrderBy(t => t));
        Assert.Equal(boundBefore, BoundText(vm, person));
        Assert.Contains("Ada Lovelace", boundBefore.Values);
    }

    [Fact]
    public void MovePeopleColumn_KeepsTheFieldMappingPointingAtTheSameColumns()
    {
        var vm = new MainWindowViewModel();
        AddPerson(vm, ("FirstName", "Ada"));
        var mappingBefore = vm.ColumnMappings.ToDictionary(m => m.TemplateToken, m => m.CsvColumn);

        vm.MovePeopleColumn(vm.PeopleFieldColumns[^1], 0);

        Assert.Equal(mappingBefore, vm.ColumnMappings.ToDictionary(m => m.TemplateToken, m => m.CsvColumn));
    }

    [Fact]
    public void ResetPeopleColumnOrder_PutsTheGridBackWithoutDisturbingValues()
    {
        var vm = new MainWindowViewModel();
        var person = AddPerson(vm, ("FirstName", "Ada"), ("LastName", "Lovelace"), ("Department", "Analytics"));
        var orderBefore = vm.PeopleFieldColumns.Select(c => c.Token).ToList();

        vm.MovePeopleColumn(vm.PeopleFieldColumns[^1], 0);
        vm.MovePeopleColumn(vm.PeopleFieldColumns[^1], 0);
        Assert.NotEqual(orderBefore, vm.PeopleFieldColumns.Select(c => c.Token));

        Assert.True(vm.ResetPeopleColumnOrder());

        Assert.Equal(orderBefore, vm.PeopleFieldColumns.Select(c => c.Token));
        Assert.Equal(orderBefore, person.Cells.Select(c => c.Token));
        Assert.False(vm.IsPeopleColumnOrderCustomised);
        Assert.Equal("Ada", person.Cells.Single(c => c.Token == "FirstName").Value);
        Assert.False(vm.ResetPeopleColumnOrder());
    }

    [Fact]
    public void MovePeopleColumnBy_StepsOnePlaceAndStopsAtTheEnds()
    {
        var vm = new MainWindowViewModel();
        AddPerson(vm, ("FirstName", "Ada"));
        var first = vm.PeopleFieldColumns[0];

        Assert.False(vm.MovePeopleColumnBy(first, -1));
        Assert.True(vm.MovePeopleColumnBy(first, 1));
        Assert.Equal(1, vm.PeopleFieldColumns.IndexOf(first));
        Assert.False(vm.MovePeopleColumn(null, 0));
    }

    [Fact]
    public void MovePeopleColumn_MovesEachRowsCellsRatherThanRebuildingThem()
    {
        // With hundreds of rows a reorder has to be one move per row. Rebuilding would throw away every cell — and
        // with it each row's editor and anything half-typed into it — and re-bind the whole grid.
        var vm = new MainWindowViewModel();
        for (int i = 0; i < 400; i++)
        {
            AddPerson(vm, ("FirstName", "Person" + i));
        }

        var cellsBefore = vm.People.Select(p => p.Cells.ToList()).ToList();

        vm.MovePeopleColumn(vm.PeopleFieldColumns[^1], 0);

        for (int i = 0; i < vm.People.Count; i++)
        {
            var cells = vm.People[i].Cells;
            Assert.Equal(cellsBefore[i].Count, cells.Count);
            Assert.All(cellsBefore[i], cell => Assert.Contains(cell, cells));
            Assert.Same(cellsBefore[i][^1], cells[0]);
        }
    }

    [Fact]
    public async Task PeopleImportedAfterAReorder_LineUpWithTheHeadings()
    {
        string dir = Directory.CreateTempSubdirectory("badgeforge-columns").FullName;
        try
        {
            var vm = new MainWindowViewModel();
            AddPerson(vm, ("FirstName", "Ada"));
            vm.MovePeopleColumn(vm.PeopleFieldColumns.Single(c => c.Token == "LastName"), 0);

            string csv = Path.Combine(dir, "roster.csv");
            File.WriteAllText(csv, "FirstName,LastName,Department,EmployeeId\nGrace,Hopper,Navy,1002\n");
            await vm.ImportRosterAsync(csv, append: true);

            var imported = vm.People[^1];
            Assert.Equal(vm.PeopleFieldColumns.Select(c => c.Token), imported.Cells.Select(c => c.Token));
            Assert.Equal("Hopper", imported.Cells[0].Value);
            Assert.Equal("Grace", imported.Cells.Single(c => c.Token == "FirstName").Value);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task SavePeopleList_WritesTheColumnsInTheOrderTheGridShowsThem()
    {
        string dir = Directory.CreateTempSubdirectory("badgeforge-columns").FullName;
        try
        {
            var vm = new MainWindowViewModel();
            AddPerson(vm, ("FirstName", "Ada"), ("LastName", "Lovelace"));
            vm.MovePeopleColumn(vm.PeopleFieldColumns.Single(c => c.Token == "LastName"), 0);

            string csv = Path.Combine(dir, "people.csv");
            await vm.SavePeopleListAsync(csv);

            string header = File.ReadLines(csv).First();
            Assert.StartsWith("LastName,FirstName", header);

            // Reading it back gives the same people, whatever order the columns came out in
            var reopened = new MainWindowViewModel();
            await reopened.ImportRosterAsync(csv);
            Assert.Equal("Ada", reopened.People[0].Cells.Single(c => c.Token == "FirstName").Value);
            Assert.Equal("Lovelace", reopened.People[0].Cells.Single(c => c.Token == "LastName").Value);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>
    /// The text every bound layer ends up with for a person, keyed by layer name — what the printed badge says.
    /// </summary>
    private static Dictionary<string, string> BoundText(MainWindowViewModel vm, PersonItem person)
    {
        var bound = TemplateBinder.Bind(vm.Template, vm.GetRecordFieldData(person.Record));
        return bound.Template.Layers.ToDictionary(
            l => l.Name,
            l => l switch
            {
                TextLayer text => text.Text,
                BarcodeLayer barcode => barcode.ContentToken,
                _ => string.Empty
            });
    }

    private static PersonItem AddPerson(MainWindowViewModel vm, params (string Token, string Value)[] values)
    {
        var person = vm.AddPerson();
        foreach (var (token, value) in values)
        {
            person.Cells.Single(c => c.Token == token).Value = value;
        }

        return person;
    }
}
