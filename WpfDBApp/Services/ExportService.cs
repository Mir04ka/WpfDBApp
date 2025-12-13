using System.IO;
using System.Xml.Serialization;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using WpfDBApp.Data;
using WpfDBApp.Models;

namespace WpfDBApp.Services;

public class ExportService
{
    private const int ExcelRowLimit = 1_000_000; // Excel DOESN'T support more than ~1M rows

    public async Task ExportExcelAsync(
        string basePath,
        IQueryable<Person> query,
        List<string> fields)
    {
        var total = await query.CountAsync();
        int fileIndex = 1;

        for (int skip = 0; skip < total; skip += ExcelRowLimit)
        {
            var data = await query
                .AsNoTracking()
                .Skip(skip)
                .Take(ExcelRowLimit)
                .ToListAsync();

            var path = total > ExcelRowLimit
                ? Path.Combine(
                    Path.GetDirectoryName(basePath)!,
                    $"{Path.GetFileNameWithoutExtension(basePath)}_{fileIndex}.xlsx")
                : basePath;

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Export");

            for (int c = 0; c < fields.Count; c++)
                ws.Cell(1, c + 1).Value = fields[c];

            for (int r = 0; r < data.Count; r++)
            {
                for (int c = 0; c < fields.Count; c++)
                {
                    ws.Cell(r + 2, c + 1).Value =
                        typeof(Person)
                            .GetProperty(fields[c])?
                            .GetValue(data[r])?
                            .ToString();
                }
            }

            wb.SaveAs(path);
            fileIndex++;
        }
    }

    public async Task ExportXmlAsync(string path, IQueryable<Person> query)
    {
        var list = await query.AsNoTracking().ToListAsync();

        var serializer = new XmlSerializer(typeof(TestProgram));
        var data = new TestProgram
        {
            Records = list.Select((p, i) => new Record
            {
                Id = i + 1,
                Date = p.Date,
                FirstName = p.FirstName,
                LastName = p.LastName,
                SurName = p.SurName,
                City = p.City,
                Country = p.Country
            }).ToList()
        };

        using var fs = new FileStream(path, FileMode.Create);
        serializer.Serialize(fs, data);
    }

    // Building a root for XML export
    [XmlRoot("TestProgram")]
    public class TestProgram
    {
        [XmlElement("Record")]
        public required List<Record> Records { get; set; }
    }

    // XML row record
    public class Record
    {
        [XmlAttribute("id")]
        public required int Id { get; set; }
        public required DateTime Date { get; set; }
        public required string FirstName { get; set; }
        public required string LastName { get; set; }
        public required string SurName { get; set; }
        public required string City { get; set; }
        public required string Country { get; set; }
    }
}
