using System.Data;
using System.Globalization;
using System.IO;
using Microsoft.Data.SqlClient;
using WpfDBApp.Models;

namespace WpfDBApp.Services;

public class CsvImporter
{
    private readonly string _connectionString;

    public CsvImporter(string connectionString)
    {
        _connectionString = connectionString;
    }
    
    public async Task ImportAsync(string csvFilePath, IProgress<(long processed, long total, Person? lastPerson)> progress, CancellationToken cancellation = default)
    {
        if (!File.Exists(csvFilePath)) throw new FileNotFoundException("CSV file not found", csvFilePath);

        const int batchSize = 1000;
        var table = CreateDataTable();
        long processed = 0;
        long total = File.ReadLines(csvFilePath).LongCount();
        
        progress.Report((0, total, null));
        
        using var sr = new StreamReader(csvFilePath);

        string? line;
        while ((line = await sr.ReadLineAsync()) != null)
        {
            cancellation.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(line)) continue;

            var values = line.Split(';');
            if (values.Length < 6) continue;

            // Try parse date using common formats
            var date = DateTime.MinValue;
            if (!DateTime.TryParse(values[0], CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
            {
                DateTime.TryParseExact(values[0], new[] { "dd.MM.yyyy", "yyyy-MM-dd", "MM/dd/yyyy" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
            }

            var row = table.NewRow();
            row["Date"] = date == DateTime.MinValue ? DBNull.Value : date;
            row["FirstName"] = values[1];
            row["LastName"] = values[2];
            row["SurName"] = values[3];
            row["City"] = values[4];
            row["Country"] = values[5];
            table.Rows.Add(row);

            var newPerson = new Person
            {
                Date = date,
                FirstName = values[1],
                LastName = values[2],
                SurName = values[3],
                City = values[4],
                Country = values[5],
            };

            processed++;
            
            progress?.Report((processed, total, newPerson)); // Report current progress

            if (table.Rows.Count >= batchSize)
            {
                await BulkInsertAsync(table, cancellation);
                table.Clear();
            }
        }

        if (table.Rows.Count > 0)
        {
            await BulkInsertAsync(table, cancellation);
            progress?.Report((processed, total, null));
            table.Clear();
        }
    }

    private DataTable CreateDataTable()
    {
        var table = new DataTable();
        table.Columns.Add("Date", typeof(DateTime));
        table.Columns.Add("FirstName", typeof(string));
        table.Columns.Add("LastName", typeof(string));
        table.Columns.Add("SurName", typeof(string));
        table.Columns.Add("City", typeof(string));
        table.Columns.Add("Country", typeof(string));
        return table;
    }

    private Task BulkInsertAsync(DataTable table, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var sqlConn = new SqlConnection(_connectionString);
            sqlConn.Open();

            using var bulk = new SqlBulkCopy(sqlConn)
            {
                DestinationTableName = "Persons",
                BatchSize = table.Rows.Count
            };

            bulk.ColumnMappings.Add("Date", "Date");
            bulk.ColumnMappings.Add("FirstName", "FirstName");
            bulk.ColumnMappings.Add("LastName", "LastName");
            bulk.ColumnMappings.Add("SurName", "SurName");
            bulk.ColumnMappings.Add("City", "City");
            bulk.ColumnMappings.Add("Country", "Country");

            bulk.WriteToServer(table);
        }, cancellationToken);
    }
}
