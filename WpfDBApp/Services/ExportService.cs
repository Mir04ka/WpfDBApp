using System.IO;
using System.Xml;
using System.Xml.Serialization;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using WpfDBApp.Data;
using WpfDBApp.Models;

namespace WpfDBApp.Services;

public class ExportService
{
    private const int ExcelRowLimit = 1_000_000; // Excel format limit
    private readonly string _connectionString;   // DB connecting string

    public ExportService(string connectionString)
    {
        _connectionString = connectionString;
    }

    public Task ExportExcelAsync(
        string basePath,
        Func<AppDbContext, IQueryable<Person>> queryFactory,
        string[] fields,
        IProgress<(long processed, long total)> progress)
    {
        return Task.Run(async () =>
        {
            if (fields == null || fields.Length == 0)
                throw new ArgumentException("Fields required", nameof(fields));

            await using var ctx = new AppDbContext(_connectionString);
            var query = queryFactory(ctx);

            var total = await query.CountAsync();
            long processed = 0;
            int fileIndex = 1;

            progress.Report((0, total));

            for (int skip = 0; skip < total; skip += ExcelRowLimit)
            {
                var chunk = await query
                    .Skip(skip)
                    .Take(ExcelRowLimit)
                    .AsNoTracking()
                    .ToListAsync();

                string path = total > ExcelRowLimit
                    ? Path.Combine(
                        Path.GetDirectoryName(basePath) ?? ".",
                        $"{Path.GetFileNameWithoutExtension(basePath)}_{fileIndex}.xlsx")
                    : basePath;

                using var wb = new XLWorkbook();
                var ws = wb.Worksheets.Add("Export");

                // header
                for (int c = 0; c < fields.Length; c++)
                    ws.Cell(1, c + 1).Value = fields[c];

                for (int r = 0; r < chunk.Count; r++)
                {
                    var item = chunk[r];

                    for (int c = 0; c < fields.Length; c++)
                    {
                        var prop = typeof(Person).GetProperty(fields[c]);
                        ws.Cell(r + 2, c + 1)
                            .SetValue(XLCellValue.FromObject(prop?.GetValue(item)));
                    }

                    processed++;

                    if (processed % 1000 == 0)
                        progress?.Report((processed, total));
                }

                wb.SaveAs(path);
                fileIndex++;
            }

            progress?.Report((total, total));
        });
    }
    
    public Task ExportXmlAsync(
        string path,
        Func<AppDbContext, IQueryable<Person>> queryFactory,
        IProgress<(long processed, long total)> progress)
    {
        return Task.Run(async () =>
        {
            await using var ctx = new AppDbContext(_connectionString);
            var query = queryFactory(ctx).AsNoTracking();

            var total = await query.CountAsync();
            long processed = 0;

            progress.Report((0, total));

            await using var fs = new FileStream(path, FileMode.Create);
            using var writer = XmlWriter.Create(fs, new XmlWriterSettings
            {
                Indent = true,
                Async = true
            });

            await writer.WriteStartDocumentAsync();
            await writer.WriteStartElementAsync(null, "TestProgram", null);

            await foreach (var p in query.AsAsyncEnumerable())
            {
                await writer.WriteStartElementAsync(null, "Record", null);
                await writer.WriteAttributeStringAsync(null, "id", null, (++processed).ToString());

                await writer.WriteElementStringAsync(null, "Date", null, p.Date.ToString("O"));
                await writer.WriteElementStringAsync(null, "FirstName", null, p.FirstName);
                await writer.WriteElementStringAsync(null, "LastName", null, p.LastName);
                await writer.WriteElementStringAsync(null, "SurName", null, p.SurName);
                await writer.WriteElementStringAsync(null, "City", null, p.City);
                await writer.WriteElementStringAsync(null, "Country", null, p.Country);

                await writer.WriteEndElementAsync();

                if (processed % 500 == 0)
                    progress?.Report((processed, total));
            }

            await writer.WriteEndElementAsync();
            await writer.WriteEndDocumentAsync();

            progress?.Report((total, total));
        });
    }

    [XmlRoot("TestProgram")]
    public class TestProgram
    {
        [XmlElement("Record")]
        public List<Record> Records { get; set; }
    }

    public class Record
    {
        [XmlAttribute("id")]
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string SurName { get; set; }
        public string City { get; set; }
        public string Country { get; set; }
    }
}
