using Microsoft.Win32;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using WpfDBApp.Data;
using WpfDBApp.Services;

namespace WpfDBApp;

public partial class MainWindow : Window
{
    private readonly AppDbContext _context;
    private readonly ExportService _exportService;

    public MainWindow()
    {
        InitializeComponent();
        _context = new AppDbContext(App.ConnectionString);
        _context.Database.EnsureCreated();
        _exportService = new ExportService();

        LoadGrid(); // Update UI table
    }

    // Update UI table
    private async void LoadGrid()
    {
        PersonsGrid.ItemsSource = await _context.Persons
            .AsNoTracking()
            .Take(1000)
            .ToListAsync();
    }

    // Building LINQ query based on selected filters
    private IQueryable<Models.Person> BuildQuery()
    {
        var q = _context.Persons.AsQueryable();

        if (DateFrom.SelectedDate.HasValue)
            q = q.Where(p => p.Date >= DateFrom.SelectedDate.Value);

        if (DateTo.SelectedDate.HasValue)
            q = q.Where(p => p.Date <= DateTo.SelectedDate.Value);

        if (!string.IsNullOrWhiteSpace(FirstNameBox.Text))
            q = q.Where(p => p.FirstName == FirstNameBox.Text);

        if (!string.IsNullOrWhiteSpace(LastNameBox.Text))
            q = q.Where(p => p.LastName == LastNameBox.Text);

        if (!string.IsNullOrWhiteSpace(SurNameBox.Text))
            q = q.Where(p => p.SurName == SurNameBox.Text);

        if (!string.IsNullOrWhiteSpace(CityBox.Text))
            q = q.Where(p => p.City == CityBox.Text);

        if (!string.IsNullOrWhiteSpace(CountryBox.Text))
            q = q.Where(p => p.Country == CountryBox.Text);

        return q;
    }

    // Applies filters and refreshes the table
    private async void ApplyFilters_Click(object sender, RoutedEventArgs e)
    {
        PersonsGrid.ItemsSource = await BuildQuery()
            .AsNoTracking()
            .Take(10000)
            .ToListAsync();
    }
    
    private async void ClearDatabase_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(
                "Are you sure you want to clear the database?",
                "Are you sure?",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning)
            != MessageBoxResult.Yes)
            return;

        try
        {
            await _context.Database.ExecuteSqlRawAsync(
                "TRUNCATE TABLE Persons");

            PersonsGrid.ItemsSource = null;

            MessageBox.Show(
                "Database has been cleared",
                "Success",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Error during clearing the database:\n{ex.Message}",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
    
    private async void ExportExcel_Click(object sender, RoutedEventArgs e)
    {
        var sfd = new SaveFileDialog { Filter = "Excel (*.xlsx)|*.xlsx" };
        if (sfd.ShowDialog() != true) return;

        var fields = FieldsPanel.Children
            .OfType<System.Windows.Controls.CheckBox>()
            .Where(c => c.IsChecked == true)
            .Select(c => c.Content!.ToString()!)
            .ToList();

        if (!fields.Any())
        {
            MessageBox.Show("You must to choose at least 1 field");
            return;
        }

        await _exportService.ExportExcelAsync(
            sfd.FileName,
            BuildQuery(),
            fields);

        MessageBox.Show("Excel export completed");
    }

    private async void ExportXml_Click(object sender, RoutedEventArgs e)
    {
        var sfd = new SaveFileDialog { Filter = "XML (*.xml)|*.xml" };
        if (sfd.ShowDialog() != true) return;

        await _exportService.ExportXmlAsync(
            sfd.FileName,
            BuildQuery());

        MessageBox.Show("XML export completed");
    }
    
    private async void ImportCsv_Click(object sender, RoutedEventArgs e)
    {
        var ofd = new OpenFileDialog
        {
            Filter = "CSV files (*.csv)|*.csv",
            Title = "Choose CSV file",
            Multiselect = false
        };

        if (ofd.ShowDialog() != true)
            return;

        try
        {
            ImportProgress.IsIndeterminate = true;

            var importer = new CsvImporter(App.ConnectionString);
            await importer.ImportAsync(ofd.FileName);

            MessageBox.Show(
                "Import completed",
                "Success",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            // Update the table after import
            PersonsGrid.ItemsSource = await _context.Persons
                .AsNoTracking()
                .Take(10000)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Import error:\n{ex.Message}",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            ImportProgress.IsIndeterminate = false;
        }
    }

}
