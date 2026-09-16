using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BadgeForge.App.ViewModels;
using BadgeForge.Core.Printing.Batch;
using BadgeForge.Core.Printing.Drivers;
using BadgeForge.Core.Printing.Models;
using BadgeForge.Core.Templates.Enums;
using BadgeForge.Core.Templates.Layers;
using SkiaSharp;
using Xunit;

namespace BadgeForge.Tests;

public class MainWindowViewModelTests
{
    [Fact]
    public void InitialState_LoadsDefaultTemplateAndFormat()
    {
        var vm = new MainWindowViewModel();

        Assert.NotNull(vm.Template);
        Assert.Equal("Employee ID Badge", vm.Template.Name);
        Assert.NotEmpty(vm.Template.Layers);
        Assert.False(vm.IsPortrait);
        Assert.Equal(85.6, vm.ActiveFormat.WidthMm);
        Assert.Equal(53.98, vm.ActiveFormat.HeightMm);

        Assert.NotEmpty(vm.AvailablePrinters);
        Assert.Contains(vm.AvailablePrinters, p => p.Contains("Mock IDP SMART-31"));
        Assert.NotNull(vm.SelectedPrinter);
    }

    [Fact]
    public void LayerManagement_AddTextLayer_CreatesAndSelectsLayer()
    {
        var vm = new MainWindowViewModel();
        int initialCount = vm.Template.Layers.Count;

        vm.AddTextLayer(15.0, 20.0);

        Assert.Equal(initialCount + 1, vm.Template.Layers.Count);
        Assert.NotNull(vm.SelectedLayer);
        Assert.IsType<TextLayer>(vm.SelectedLayer);
        Assert.Equal(15.0, vm.SelectedLayer.X);
        Assert.Equal(20.0, vm.SelectedLayer.Y);
        Assert.True(vm.IsTextLayerSelected);
        Assert.False(vm.IsPhotoLayerSelected);
        Assert.False(vm.IsBarcodeLayerSelected);
    }

    [Fact]
    public void TextColor_PickingColor_UpdatesLayerAndLeavesKResin()
    {
        var vm = new MainWindowViewModel();
        vm.AddTextLayer();
        Assert.True(vm.TextPrintsAsPureBlack);

        vm.TextColorHex = " 7a918d ";

        var layer = Assert.IsType<TextLayer>(vm.SelectedLayer);
        Assert.Equal("#7A918D", layer.ColorHex);
        Assert.False(layer.IsPureBlackKResin);
        Assert.Equal("#7A918D", vm.TextColorHex);
        Assert.Contains(layer, vm.Template.Layers);
    }

    [Fact]
    public void TextColor_PickingBlack_KeepsKResin()
    {
        var vm = new MainWindowViewModel();
        vm.AddTextLayer();

        vm.TextColorHex = "#000";

        var layer = Assert.IsType<TextLayer>(vm.SelectedLayer);
        Assert.Equal("#000000", layer.ColorHex);
        Assert.True(layer.IsPureBlackKResin);
    }

    [Fact]
    public void TextColor_InvalidHex_LeavesLayerUnchanged()
    {
        var vm = new MainWindowViewModel();
        vm.AddTextLayer();
        var before = vm.SelectedLayer;

        vm.TextColorHex = "not a colour";

        Assert.Same(before, vm.SelectedLayer);
        Assert.Equal("#000000", vm.TextColorHex);
    }

    [Fact]
    public void TextColor_KResinLayer_ReportsBlackUntilKResinTurnedOff()
    {
        var vm = new MainWindowViewModel();
        vm.AddTextLayer();
        vm.TextColorHex = "#B42318";

        vm.TextPrintsAsPureBlack = true;
        Assert.Equal("#000000", vm.TextColorHex);

        vm.TextPrintsAsPureBlack = false;
        Assert.Equal("#B42318", vm.TextColorHex);
    }

    [Fact]
    public void LayerManagement_AddPhotoLayer_CreatesPhotoLayer()
    {
        var vm = new MainWindowViewModel();
        int initialCount = vm.Template.Layers.Count;

        vm.AddPhotoLayer(10.0, 10.0);

        Assert.Equal(initialCount + 1, vm.Template.Layers.Count);
        Assert.NotNull(vm.SelectedLayer);
        Assert.IsType<PhotoLayer>(vm.SelectedLayer);
        Assert.True(vm.IsPhotoLayerSelected);
        Assert.False(vm.IsTextLayerSelected);
    }

    [Fact]
    public void LayerManagement_AddBarcodeLayer_CreatesBarcodeLayer()
    {
        var vm = new MainWindowViewModel();
        int initialCount = vm.Template.Layers.Count;

        vm.AddBarcodeLayer(5.0, 40.0);

        Assert.Equal(initialCount + 1, vm.Template.Layers.Count);
        Assert.NotNull(vm.SelectedLayer);
        Assert.IsType<BarcodeLayer>(vm.SelectedLayer);
        Assert.True(vm.IsBarcodeLayerSelected);
    }

    [Fact]
    public void LayerManagement_DeleteSelectedLayer_RemovesFromTemplate()
    {
        var vm = new MainWindowViewModel();
        var firstLayer = vm.Template.Layers.First();
        vm.SelectedLayer = firstLayer;

        vm.DeleteSelectedLayer();

        Assert.DoesNotContain(firstLayer, vm.Template.Layers);
    }

    [Fact]
    public void LayerManagement_DuplicateSelectedLayer_AddsClonedLayer()
    {
        var vm = new MainWindowViewModel();
        var original = vm.Template.Layers.First();
        vm.SelectedLayer = original;
        int countBefore = vm.Template.Layers.Count;

        vm.DuplicateSelectedLayer();

        Assert.Equal(countBefore + 1, vm.Template.Layers.Count);
        Assert.NotNull(vm.SelectedLayer);
        Assert.NotEqual(original.Id, vm.SelectedLayer.Id);
        Assert.Equal(original.X + 2.0, vm.SelectedLayer.X);
        Assert.Equal(original.Y + 2.0, vm.SelectedLayer.Y);
        Assert.Contains("(Copy)", vm.SelectedLayer.Name);
    }

    [Fact]
    public void LayerManagement_MoveAndResizeSelectedLayer_ModifiesGeometry()
    {
        var vm = new MainWindowViewModel();
        var original = vm.Template.Layers.First();
        vm.SelectedLayer = original;

        double origX = original.X;
        double origY = original.Y;
        double origW = original.Width;
        double origH = original.Height;

        vm.MoveSelectedLayer(5.0, 3.0);
        Assert.Equal(Math.Round(origX + 5.0, 1), vm.SelectedLayer.X);
        Assert.Equal(Math.Round(origY + 3.0, 1), vm.SelectedLayer.Y);

        vm.ResizeSelectedLayer(4.0, 6.0);
        Assert.Equal(Math.Round(origW + 4.0, 1), vm.SelectedLayer.Width);
        Assert.Equal(Math.Round(origH + 6.0, 1), vm.SelectedLayer.Height);
    }

    [Fact]
    public void LayerManagement_MoveAndResize_WhenTemplateOrderDiffersFromZOrder_UpdatesCorrectListItem()
    {
        var vm = new MainWindowViewModel();

        // Template storage order no longer matches the Z-ordered display list
        vm.Template.Layers.Reverse();
        vm.RefreshLayersList();

        var target = vm.Layers[0];
        var bystanders = vm.Layers.Skip(1).ToList();
        vm.SelectedLayer = target;

        vm.MoveSelectedLayer(2.0, 1.0);
        vm.ResizeSelectedLayer(1.0, 1.0);

        var updated = vm.SelectedLayer!;
        Assert.Equal(target.Id, updated.Id);
        Assert.Same(updated, vm.Layers[0]);
        Assert.Equal(bystanders, vm.Layers.Skip(1).ToList());
        Assert.Equal(vm.Template.Layers.Count, vm.Layers.Select(l => l.Id).Distinct().Count());
        Assert.Contains(updated, vm.Template.Layers);
        Assert.DoesNotContain(target, vm.Template.Layers);
    }

    [Fact]
    public void CardFormat_SwitchingOrientation_UpdatesDimensions()
    {
        var vm = new MainWindowViewModel();
        Assert.False(vm.IsPortrait);
        Assert.Equal(85.6, vm.ActiveFormat.WidthMm);
        Assert.Equal(53.98, vm.ActiveFormat.HeightMm);

        vm.IsPortrait = true;
        Assert.True(vm.IsPortrait);
        Assert.Equal(53.98, vm.ActiveFormat.WidthMm);
        Assert.Equal(85.6, vm.ActiveFormat.HeightMm);

        vm.IsPortrait = false;
        Assert.False(vm.IsPortrait);
        Assert.Equal(85.6, vm.ActiveFormat.WidthMm);
        Assert.Equal(53.98, vm.ActiveFormat.HeightMm);
    }

    [Fact]
    public void CardFormat_SwitchingFormat_UpdatesToCR79()
    {
        var vm = new MainWindowViewModel();
        vm.ActiveFormat = CardFormat.CR79;

        Assert.Equal(83.9, vm.ActiveFormat.WidthMm);
        Assert.Equal(51.0, vm.ActiveFormat.HeightMm);
        Assert.Equal(CardFormat.CR79, vm.Template.TargetFormat);
    }

    [Fact]
    public async Task Pagination_WithLoadedData_NavigatesCorrectly()
    {
        var vm = new MainWindowViewModel();
        
        // Create temporary CSV file
        var tempCsv = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempCsv, "EmployeeId,FirstName,LastName,Department\nEMP001,Alice,Smith,Engineering\nEMP002,Bob,Jones,Marketing\nEMP003,Charlie,Brown,Security\n");
            await vm.ImportRosterAsync(tempCsv);

            Assert.Equal(3, vm.TotalRecords);
            Assert.Equal(0, vm.CurrentRecordIndex);
            Assert.Equal("Record 1 of 3", vm.RecordPaginationText);
            Assert.False(vm.CanGoPrevious);
            Assert.True(vm.CanGoNext);

            // Navigate Next
            vm.NextRecord();
            Assert.Equal(1, vm.CurrentRecordIndex);
            Assert.Equal("Record 2 of 3", vm.RecordPaginationText);
            Assert.True(vm.CanGoPrevious);
            Assert.True(vm.CanGoNext);

            // Navigate Next to end
            vm.NextRecord();
            Assert.Equal(2, vm.CurrentRecordIndex);
            Assert.Equal("Record 3 of 3", vm.RecordPaginationText);
            Assert.True(vm.CanGoPrevious);
            Assert.False(vm.CanGoNext);

            // Bounds protection - can't advance past end
            vm.NextRecord();
            Assert.Equal(2, vm.CurrentRecordIndex);

            // Navigate Previous
            vm.PreviousRecord();
            Assert.Equal(1, vm.CurrentRecordIndex);
            Assert.Equal("Record 2 of 3", vm.RecordPaginationText);

            // Check current active record fields
            var currentFields = vm.GetActiveRecordFieldData();
            Assert.Equal("Bob", currentFields["FirstName"]);
            Assert.Equal("Jones", currentFields["LastName"]);
            Assert.Equal("Marketing", currentFields["Department"]);
        }
        finally
        {
            if (File.Exists(tempCsv))
            {
                File.Delete(tempCsv);
            }
        }
    }

    [Fact]
    public async Task People_ReflectRecords_CorrectInitialStatus()
    {
        var vm = new MainWindowViewModel();
        var tempCsv = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempCsv, "EmployeeId,FirstName,LastName\nE101,John,Doe\nE102,Jane,Roe\n");
            await vm.ImportRosterAsync(tempCsv);

            Assert.Equal(2, vm.People.Count);
            Assert.Equal("E101", vm.People[0].RecordId);
            Assert.Equal("John Doe", vm.People[0].Name);
            Assert.Equal("Pending", vm.People[0].PrintStatus);
        }
        finally
        {
            if (File.Exists(tempCsv))
            {
                File.Delete(tempCsv);
            }
        }
    }

    [Fact]
    public void BatchControls_InitialStatus_CanStartWhenDataLoaded()
    {
        var vm = new MainWindowViewModel();
        Assert.True(vm.CanStartBatch);
        Assert.False(vm.IsBatchRunning);
        Assert.False(vm.HopperPausePromptVisible);
        Assert.False(vm.ErrorPromptVisible);
    }

    [Fact]
    public async Task HopperAlert_Resume_ClearsAlertPrompt()
    {
        var vm = new MainWindowViewModel();
        vm.HopperPausePromptVisible = true;
        vm.HopperPauseMessage = "Hopper full. Please clear output hopper.";
        Assert.True(vm.HopperPausePromptVisible);

        await vm.ResumeBatchRunAsync();
        Assert.False(vm.HopperPausePromptVisible);
    }

    [Fact]
    public void People_AddPerson_EditingFieldsFeedsTheBadge()
    {
        var vm = new MainWindowViewModel();
        var person = vm.AddPerson();

        Assert.Same(person, Assert.Single(vm.People));
        Assert.Same(person, vm.SelectedPerson);
        Assert.Equal(1, person.Position);
        Assert.Equal(new[] { "FirstName", "LastName" }, vm.PeopleFieldColumns.Take(2).Select(c => c.Token));
        Assert.DoesNotContain(vm.PeopleFieldColumns, c => c.Token == "Photo");

        person.Cells.Single(c => c.Token == "FirstName").Value = "Ada";
        person.Cells.Single(c => c.Token == "LastName").Value = "Lovelace";

        Assert.Equal("Ada Lovelace", person.Name);
        var fields = vm.GetActiveRecordFieldData();
        Assert.Equal("Ada", fields["FirstName"]);
        Assert.Equal("Lovelace", fields["LastName"]);
    }

    [Fact]
    public void People_GetOrAddPersonAfter_AddsAtTheEndButNeverAnotherBlankPerson()
    {
        var vm = new MainWindowViewModel();
        var ann = AddNamedPerson(vm, "Ann", "Able");
        var bo = AddNamedPerson(vm, "Bo", "Adams");

        Assert.Same(bo, vm.GetOrAddPersonAfter(ann));

        var added = vm.GetOrAddPersonAfter(bo);
        Assert.NotNull(added);
        Assert.Equal(new[] { ann, bo, added }, vm.People);
        Assert.True(added.IsBlank);

        Assert.Null(vm.GetOrAddPersonAfter(added));
        Assert.Equal(3, vm.People.Count);

        added.Cells.Single(c => c.Token == "FirstName").Value = "Cy";
        Assert.False(added.IsBlank);
        Assert.NotNull(vm.GetOrAddPersonAfter(added));
        Assert.Equal(4, vm.People.Count);
    }

    [Fact]
    public void People_MoveRemoveAndSort_KeepPrintOrder()
    {
        var vm = new MainWindowViewModel();
        var ann = AddNamedPerson(vm, "Ann", "Able");
        var bo = AddNamedPerson(vm, "Bo", "Adams");
        var cy = AddNamedPerson(vm, "Cy", "Moss");

        vm.MovePerson(cy, -2);
        Assert.Equal(new[] { cy, ann, bo }, vm.People);
        Assert.Equal(new[] { 1, 2, 3 }, vm.People.Select(p => p.Position));
        Assert.Same(cy, vm.SelectedPerson);
        Assert.Equal("Cy", vm.GetActiveRecordFieldData()["FirstName"]);

        vm.RemovePerson(ann);
        Assert.Equal(new[] { cy, bo }, vm.People);
        Assert.Equal(2, vm.TotalRecords);

        vm.SortPeopleByName();
        Assert.Equal(new[] { bo, cy }, vm.People);
        Assert.Equal(new[] { 1, 2 }, vm.People.Select(p => p.Position));
    }

    [Fact]
    public async Task People_PhotosFromFiles_AreUsedForThePhotoLayer()
    {
        string dir = Directory.CreateTempSubdirectory("badgeforge-people-").FullName;
        try
        {
            string jane = CreatePng(dir, "jane_smith.png", SKColors.Teal);
            string notImage = Path.Combine(dir, "notes.png");
            await File.WriteAllTextAsync(notImage, "not an image");

            var vm = new MainWindowViewModel();
            int added = await vm.AddPeopleFromPhotosAsync(new[] { jane, notImage });

            Assert.Equal(1, added);
            var person = Assert.Single(vm.People);
            Assert.Equal("Jane Smith", person.Name);
            Assert.True(person.HasPhoto);
            Assert.Equal(jane, vm.GetActiveRecordFieldData()["Photo"]);

            var other = vm.AddPerson();
            Assert.False(await vm.SetPersonPhotoAsync(other, notImage));
            Assert.False(other.HasPhoto);
            Assert.True(await vm.SetPersonPhotoAsync(other, jane));
            Assert.Equal(jane, vm.GetRecordFieldData(other.Record)["Photo"]);

            vm.ClearPersonPhoto(other);
            Assert.False(vm.GetRecordFieldData(other.Record).ContainsKey("Photo"));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task People_Print_SendsRenderedBadgesForTickedPeopleInListOrder()
    {
        string dir = Directory.CreateTempSubdirectory("badgeforge-people-").FullName;
        try
        {
            string photo = CreatePng(dir, "face.png", SKColors.Teal);
            var vm = new MainWindowViewModel();
            var ann = AddNamedPerson(vm, "Ann", "Able");
            var bo = AddNamedPerson(vm, "Bo", "Adams");
            var cy = AddNamedPerson(vm, "Cy", "Moss");

            // The default template's photo layer is required, so everyone printed needs a photo and an ID
            foreach (var person in new[] { ann, cy })
            {
                Assert.True(await vm.SetPersonPhotoAsync(person, photo));
            }
            ann.Cells.Single(c => c.Token == "EmployeeId").Value = "E-1";
            cy.Cells.Single(c => c.Token == "EmployeeId").Value = "E-2";

            vm.MovePerson(cy, -2); // Cy, Ann, Bo
            bo.IsIncluded = false;
            Assert.Equal("Print 2 badges", vm.PrintPeopleButtonText);
            Assert.Null(vm.AllPeopleIncluded);

            await vm.StartBatchRunAsync();

            Assert.Equal(BatchExecutionState.Completed, vm.BatchState);
            Assert.True(vm.CanStartBatch);
            var driver = Assert.IsType<MockCardPrinterDriver>(vm.ActiveDriver);
            Assert.Equal(new[] { "Cy Moss", "Ann Able" }, driver.SubmittedJobs.Select(j => j.Label));
            foreach (var job in driver.SubmittedJobs)
            {
                using var bitmap = SKBitmap.Decode(job.FrontPixelBuffer);
                Assert.NotNull(bitmap);
                Assert.Equal(1011, bitmap.Width);
                Assert.Equal(638, bitmap.Height);
            }
            Assert.NotEqual(driver.SubmittedJobs[0].FrontPixelBuffer, driver.SubmittedJobs[1].FrontPixelBuffer);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task People_SavedList_ReopensInOrderWithPhotos()
    {
        string dir = Directory.CreateTempSubdirectory("badgeforge-people-").FullName;
        try
        {
            string photo = CreatePng(dir, "cy.png", SKColors.Olive);
            var vm = new MainWindowViewModel();
            AddNamedPerson(vm, "Ann", "Able");
            var cy = AddNamedPerson(vm, "Cy", "Moss");
            Assert.True(await vm.SetPersonPhotoAsync(cy, photo));
            vm.MovePerson(cy, -1);

            string csv = Path.Combine(dir, "people.csv");
            await vm.SavePeopleListAsync(csv);

            var reopened = new MainWindowViewModel();
            await reopened.ImportRosterAsync(csv);
            Assert.Equal(new[] { "Cy Moss", "Ann Able" }, reopened.People.Select(p => p.Name));
            Assert.Equal(photo, reopened.People[0].PhotoPath);
            Assert.False(reopened.People[1].HasPhoto);

            await reopened.ImportRosterAsync(csv, append: true);
            Assert.Equal(new[] { "Cy Moss", "Ann Able", "Cy Moss", "Ann Able" }, reopened.People.Select(p => p.Name));
            Assert.Equal(new[] { 1, 2, 3, 4 }, reopened.People.Select(p => p.Position));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private static PersonItem AddNamedPerson(MainWindowViewModel vm, string firstName, string lastName)
    {
        var person = vm.AddPerson();
        person.Cells.Single(c => c.Token == "FirstName").Value = firstName;
        person.Cells.Single(c => c.Token == "LastName").Value = lastName;
        return person;
    }

    private static string CreatePng(string directory, string fileName, SKColor color)
    {
        using var bitmap = new SKBitmap(40, 50);
        bitmap.Erase(color);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        string path = Path.Combine(directory, fileName);
        File.WriteAllBytes(path, data.ToArray());
        return path;
    }
}
