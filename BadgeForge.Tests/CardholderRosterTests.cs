using System.IO.Compression;
using System.Text;
using BadgeForge.Core.Data.Models;
using BadgeForge.Core.Data.Services;
using Xunit;

namespace BadgeForge.Tests;

public class CardholderRosterTests
{
    [Fact]
    public void FromRecord_RecognisesCommonColumnSpellings_AndKeepsTheRestAsAdditionalFields()
    {
        var record = new BadgeRecord
        {
            Fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Badge Number"] = "E-1001",
                ["Name"] = "Grace Hopper",
                ["Title"] = "Rear Admiral",
                ["Dept"] = "Navy",
                ["Expiry Date"] = "2027-12-31",
                ["ExpiryDate"] = "2027-12-31"
            },
            ResolvedPhotoPath = "/photos/grace.jpg"
        };

        var cardholder = CardholderMapper.FromRecord(record);

        Assert.Equal("E-1001", cardholder.Id);
        Assert.Equal("Grace Hopper", cardholder.FullName);
        Assert.Equal("Rear Admiral", cardholder.JobTitle);
        Assert.Equal("Navy", cardholder.Department);
        Assert.Equal("/photos/grace.jpg", cardholder.PhotoPath);
        Assert.Single(cardholder.AdditionalFields);
        Assert.Equal("2027-12-31", cardholder.AdditionalFields["ExpiryDate"]);
        Assert.True(cardholder.IsSelected);
    }

    [Fact]
    public void ToFieldData_ProvidesStandardTokensAndDerivesNames()
    {
        var cardholder = new Cardholder
        {
            Id = "42",
            FullName = "Katherine Johnson",
            JobTitle = "Mathematician",
            Department = "Flight Research",
            PhotoPath = "/p/k.png",
            AdditionalFields = { ["ExpiryDate"] = "2028-01-01" }
        };

        var data = cardholder.ToFieldData();

        Assert.Equal("Katherine", data["FirstName"]);
        Assert.Equal("Johnson", data["LastName"]);
        Assert.Equal("Mathematician", data["JobTitle"]);
        Assert.Equal("/p/k.png", data["Photo"]);
        Assert.Equal("2028-01-01", data["expirydate"]);
    }

    [Fact]
    public void AddStandardTokens_FillsOnlyMissingValues()
    {
        var data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["First Name"] = "Alan",
            ["Last Name"] = "Turing",
            ["Position"] = "Cryptanalyst",
            ["JobTitle"] = "Already set"
        };

        CardholderMapper.AddStandardTokens(data);

        Assert.Equal("Alan Turing", data["FullName"]);
        Assert.Equal("Already set", data["JobTitle"]);
    }

    [Fact]
    public void RoundTrip_CardholderToRecordAndBack_KeepsValues()
    {
        var original = new Cardholder { Id = "7", FullName = "Hedy Lamarr", JobTitle = "Inventor", Department = "R&D", PhotoPath = "/h.jpg" };

        var restored = CardholderMapper.FromRecord(CardholderMapper.ToRecord(original, rowNumber: 2));

        Assert.Equal(original.Id, restored.Id);
        Assert.Equal(original.FullName, restored.FullName);
        Assert.Equal(original.JobTitle, restored.JobTitle);
        Assert.Equal(original.Department, restored.Department);
        Assert.Equal(original.PhotoPath, restored.PhotoPath);
    }

    [Fact]
    public void Xlsx_ReadsSharedAndInlineStringsNumbersAndDates()
    {
        string path = CreateWorkbook(
            sharedStrings: new[] { "Full Name", "Job Title", "Expiry Date", "Ada Lovelace", "Analyst" },
            sheetRows: """
                <row r="1"><c r="A1" t="s"><v>0</v></c><c r="B1" t="s"><v>1</v></c><c r="C1" t="s"><v>2</v></c><c r="D1" t="inlineStr"><is><t>Employee Id</t></is></c></row>
                <row r="3"><c r="A3" t="s"><v>3</v></c><c r="B3" t="s"><v>4</v></c><c r="C3" s="1"><v>46387</v></c><c r="D3"><v>1001</v></c></row>
                """);
        try
        {
            var records = new RosterImportService().ImportRecords(path);

            var record = Assert.Single(records);
            Assert.Equal(3, record.RowNumber);
            Assert.Equal("Ada Lovelace", record.GetValue("Full Name"));
            Assert.Equal("Ada Lovelace", record.GetValue("FullName"));
            Assert.Equal("Analyst", record.GetValue("JobTitle"));
            Assert.Equal("2026-12-31", record.GetValue("ExpiryDate"));
            Assert.Equal("1001", record.GetValue("EmployeeId"));

            var cardholder = CardholderMapper.FromRecord(record);
            Assert.Equal("1001", cardholder.Id);
            Assert.Equal("Analyst", cardholder.JobTitle);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Import_LegacyXls_ExplainsHowToConvert()
    {
        string path = Path.Combine(Path.GetTempPath(), $"roster-{Guid.NewGuid():N}.xls");
        File.WriteAllBytes(path, new byte[] { 0xD0, 0xCF, 0x11, 0xE0 });
        try
        {
            var ex = Assert.Throws<NotSupportedException>(() => new RosterImportService().ImportRecords(path));
            Assert.Contains(".xlsx", ex.Message);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Import_CorruptXlsx_ThrowsInvalidData()
    {
        string path = Path.Combine(Path.GetTempPath(), $"roster-{Guid.NewGuid():N}.xlsx");
        File.WriteAllText(path, "not a zip");
        try
        {
            Assert.Throws<InvalidDataException>(() => new RosterImportService().ImportRecords(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Builds a minimal but well-formed .xlsx: workbook, relationships, shared strings, one date style and one sheet.
    /// </summary>
    internal static string CreateWorkbook(string[] sharedStrings, string sheetRows)
    {
        string path = Path.Combine(Path.GetTempPath(), $"roster-{Guid.NewGuid():N}.xlsx");
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);

        void Add(string name, string xml)
        {
            using var writer = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(false));
            writer.Write(xml);
        }

        const string main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        const string rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

        Add("xl/workbook.xml", $"""<workbook xmlns="{main}" xmlns:r="{rel}"><sheets><sheet name="People" sheetId="1" r:id="rId7"/></sheets></workbook>""");
        Add("xl/_rels/workbook.xml.rels", """<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId7" Type="worksheet" Target="worksheets/people.xml"/></Relationships>""");
        Add("xl/sharedStrings.xml", $"""<sst xmlns="{main}">{string.Concat(sharedStrings.Select(s => $"<si><t>{System.Security.SecurityElement.Escape(s)}</t></si>"))}</sst>""");
        Add("xl/styles.xml", $"""<styleSheet xmlns="{main}"><numFmts count="1"><numFmt numFmtId="164" formatCode="dd/mm/yyyy"/></numFmts><cellXfs count="2"><xf numFmtId="0"/><xf numFmtId="164"/></cellXfs></styleSheet>""");
        Add("xl/worksheets/people.xml", $"""<worksheet xmlns="{main}"><sheetData>{sheetRows}</sheetData></worksheet>""");
        return path;
    }
}
