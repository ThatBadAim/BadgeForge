using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BadgeForge.App.ViewModels;
using BadgeForge.Core.Printing.Batch;
using BadgeForge.Core.Printing.Drivers;
using BadgeForge.Core.Printing.Models;
using SkiaSharp;
using Xunit;

namespace BadgeForge.Tests;

public class MainWindowPrintingTests
{
    [Fact]
    public async Task Print_Proceeds_WhenFieldsOrPhotosAreMissing()
    {
        var vm = new MainWindowViewModel();
        var ann = AddPerson(vm, "Ann", "Able", "E-1"); // no photo, and optional fields are empty

        await vm.StartBatchRunAsync();

        var driver = Assert.IsType<MockCardPrinterDriver>(vm.ActiveDriver);
        Assert.Single(driver.SubmittedJobs);
        Assert.False(vm.ErrorPromptVisible);
        Assert.Equal(BatchExecutionState.Completed, vm.BatchState);
    }

    [Fact]
    public async Task Print_Proceeds_WhenTwoPeopleShareAnId()
    {
        string dir = Directory.CreateTempSubdirectory("badgeforge-print-").FullName;
        try
        {
            string photo = CreatePng(dir, "face.png");
            var vm = new MainWindowViewModel();
            foreach (var person in new[] { AddPerson(vm, "Ann", "Able", "E-1"), AddPerson(vm, "Bo", "Adams", "E-1") })
            {
                Assert.True(await vm.SetPersonPhotoAsync(person, photo));
            }

            await vm.StartBatchRunAsync();

            var driver = Assert.IsType<MockCardPrinterDriver>(vm.ActiveDriver);
            Assert.Equal(2, driver.SubmittedJobs.Count);
            Assert.False(vm.ErrorPromptVisible);
            Assert.Equal(BatchExecutionState.Completed, vm.BatchState);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task Print_Proceeds_WhenBarcodeTokenIsMissing()
    {
        var vm = new MainWindowViewModel();
        var person = vm.AddPerson();
        person.Cells.Single(c => c.Token == "FirstName").Value = "Josh";
        person.Cells.Single(c => c.Token == "LastName").Value = "Name";
        // EmployeeId left completely blank

        await vm.StartBatchRunAsync();

        var driver = Assert.IsType<MockCardPrinterDriver>(vm.ActiveDriver);
        Assert.Single(driver.SubmittedJobs);
        Assert.False(vm.ErrorPromptVisible);
        Assert.Equal(BatchExecutionState.Completed, vm.BatchState);
    }

    [Fact]
    public async Task Print_AsksBeforePrintingWithWarnings_AndSendsNothingWhenDeclined()
    {
        string dir = Directory.CreateTempSubdirectory("badgeforge-print-").FullName;
        try
        {
            string photo = CreatePng(dir, "face.png");
            var vm = new MainWindowViewModel();
            var ann = AddPerson(vm, "Ann", "Able", "E-1");
            Assert.True(await vm.SetPersonPhotoAsync(ann, photo));

            // Department is left empty, which is only a warning
            string? warningsShown = null;
            vm.ConfirmPrintWithWarningsAsync = warnings =>
            {
                warningsShown = warnings;
                return Task.FromResult(false);
            };

            await vm.StartBatchRunAsync();

            Assert.Contains("Department", warningsShown);
            Assert.Empty(Assert.IsType<MockCardPrinterDriver>(vm.ActiveDriver).SubmittedJobs);
            Assert.True(vm.CanStartBatch);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task Print_PortraitBadge_IsPrintedOnAPortraitCanvas()
    {
        string dir = Directory.CreateTempSubdirectory("badgeforge-print-").FullName;
        try
        {
            string photo = CreatePng(dir, "face.png");
            var vm = new MainWindowViewModel { IsPortrait = true };
            var ann = AddPerson(vm, "Ann", "Able", "E-1");
            Assert.True(await vm.SetPersonPhotoAsync(ann, photo));

            await vm.StartBatchRunAsync();

            Assert.Equal(BatchExecutionState.Completed, vm.BatchState);
            var job = Assert.Single(Assert.IsType<MockCardPrinterDriver>(vm.ActiveDriver).SubmittedJobs);
            using var bitmap = SKBitmap.Decode(job.FrontPixelBuffer);
            Assert.Equal(638, bitmap.Width);
            Assert.Equal(1011, bitmap.Height);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void CardSizeAndOrientation_AreChosenSeparately()
    {
        var vm = new MainWindowViewModel();

        vm.IsPortrait = true;
        Assert.Equal(CardFormat.CR80, vm.SelectedCardSize);

        vm.SelectedCardSize = CardFormat.CR79;
        Assert.Equal(CardFormat.CR79.WithOrientation(true), vm.ActiveFormat);
        Assert.True(vm.IsPortrait);

        // A combo box clearing its selection must not wipe the card format
        vm.SelectedCardSize = null;
        Assert.Equal(CardFormat.CR79.WithOrientation(true), vm.ActiveFormat);
    }

    [Fact]
    public void NewTemplate_KeepsThePeopleList_AndClearsUnsavedChanges()
    {
        var vm = new MainWindowViewModel();
        AddPerson(vm, "Ann", "Able", "E-1");
        vm.AddTextLayer();
        Assert.True(vm.HasUnsavedTemplateChanges);

        vm.NewTemplate();

        Assert.False(vm.HasUnsavedTemplateChanges);
        Assert.Single(vm.People);
    }

    [Fact]
    public void NewBlankTemplate_CreatesEmptyLayers_AndSetsFormat()
    {
        var vm = new MainWindowViewModel();
        AddPerson(vm, "Ann", "Able", "E-1");
        Assert.NotEmpty(vm.Layers);

        vm.NewBlankTemplate(CardFormat.CR79);

        Assert.Empty(vm.Layers);
        Assert.Equal(CardFormat.CR79, vm.ActiveFormat);
        Assert.False(vm.HasUnsavedTemplateChanges);
        Assert.Single(vm.People);
    }

    [Fact]
    public void SelectedOrientation_DropdownTogglesOrientation()
    {
        var vm = new MainWindowViewModel();
        Assert.Equal("Landscape", vm.SelectedOrientation);
        Assert.False(vm.IsPortrait);

        vm.SelectedOrientation = "Portrait";
        Assert.True(vm.IsPortrait);
        Assert.Equal("Portrait", vm.SelectedOrientation);

        vm.SelectedOrientation = "Landscape";
        Assert.False(vm.IsPortrait);
        Assert.Equal("Landscape", vm.SelectedOrientation);
    }

    [Fact]
    public void ApplyCropAspectRatio_UpdatesLayerDimensionsAndAspectFill()
    {
        var vm = new MainWindowViewModel();
        var photo = vm.Layers.OfType<BadgeForge.Core.Templates.Layers.PhotoLayer>().First();
        vm.SelectedLayer = photo;

        // Apply 1:1 Square
        vm.ApplyCropAspectRatio(1.0);

        Assert.NotNull(vm.SelectedLayer);
        Assert.Equal(vm.SelectedLayer.Width, vm.SelectedLayer.Height);
        Assert.Equal(BadgeForge.Core.Templates.Enums.PhotoCropMode.AspectFill, ((BadgeForge.Core.Templates.Layers.PhotoLayer)vm.SelectedLayer).CropMode);

        // Apply crop anchor
        vm.SetCropAnchor("top");
        Assert.Equal(-1.0, vm.ImageOffsetY);
        vm.SetCropAnchor("bottom");
        Assert.Equal(1.0, vm.ImageOffsetY);
        vm.SetCropAnchor("center");
        Assert.Equal(0.0, vm.ImageOffsetY);
    }

    private static PersonItem AddPerson(MainWindowViewModel vm, string firstName, string lastName, string employeeId)
    {
        var person = vm.AddPerson();
        person.Cells.Single(c => c.Token == "FirstName").Value = firstName;
        person.Cells.Single(c => c.Token == "LastName").Value = lastName;
        person.Cells.Single(c => c.Token == "EmployeeId").Value = employeeId;
        return person;
    }

    private static string CreatePng(string directory, string fileName)
    {
        using var bitmap = new SKBitmap(40, 50);
        bitmap.Erase(SKColors.Teal);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        string path = Path.Combine(directory, fileName);
        File.WriteAllBytes(path, data.ToArray());
        return path;
    }
}
