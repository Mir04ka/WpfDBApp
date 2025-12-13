using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using WpfDBApp.Models;

namespace WpfDBApp.Services;

public class CsvImporter
{
    private string _connectionString; // Database connection string for bulk insert
    public CsvImporter(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task ImportAsync(string csvFilePath)
    {
        if (!File.Exists(csvFilePath)) throw new FileNotFoundException("CSV file not found", csvFilePath);
        
        const int batchSize = 1000;
        var table = CreateDataTable();
        long processed = 0;

        using (var sr = new StreamReader(csvFilePath))
        {
            string line;
            while ((line = await sr.ReadLineAsync()) != null)
            {
                processed++;
                if (string.IsNullOrWhiteSpace(line)) continue;
                
                var values = line.Split(';');
                if (values.Length < 6) continue;

                if (!DateTime.TryParse(values[0], out var date))
                {
                    DateTime.TryParseExact(values[0], new[] { "dd.MM.yyyy", "yyyy-MM-dd", "MM/dd/yyyy" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
                }
                
                var row = table.NewRow();
                row["Date"] = date == default ? (object)DBNull.Value : date;
                row["FirstName"] = values[1];
                row["LastName"] = values[2];
                row["SurName"] = values[3];
                row["City"] = values[4];
                row["Country"] = values[5];
                table.Rows.Add(row);

                // Bulk insert in batches to reduce mem usage
                if (table.Rows.Count >= batchSize)
                {
                    await BulkInsertAsync(table);
                    table.Clear();
                }
            }
            
            // Insert remaining rows
            if (table.Rows.Count >= 0)
            {
                await BulkInsertAsync(table);
                table.Clear();
            }
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

    // Fast bulk insert into SQL server
    private Task BulkInsertAsync(DataTable table)
    {
        return Task.Run(() =>
        {
            using (var sqlConn = new SqlConnection(_connectionString))
            {
                sqlConn.Open();
                using (var bulk = new SqlBulkCopy(sqlConn))
                {
                    bulk.DestinationTableName = "Persons";
                    bulk.ColumnMappings.Add("Date", "Date");
                    bulk.ColumnMappings.Add("FirstName", "FirstName");
                    bulk.ColumnMappings.Add("LastName", "LastName");
                    bulk.ColumnMappings.Add("SurName", "SurName");
                    bulk.ColumnMappings.Add("City", "City");
                    bulk.ColumnMappings.Add("Country", "Country");

                    bulk.WriteToServer(table);
                }
            }
        });
    }
}