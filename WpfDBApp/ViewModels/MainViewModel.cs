using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;
using WpfDBApp.Data;
using WpfDBApp.Data.Repositories;
using WpfDBApp.Models;
using WpfDBApp.Services;
using WpfDBApp.Helpers;

namespace WpfDBApp.ViewModels;

// Main ViewModel implementing MVVM 
public class MainViewModel : ObservableObject
{
    private readonly string _connectionString;
    private readonly SemaphoreSlim _operationLock = new SemaphoreSlim(1, 1);

    public ObservableCollection<Person> Persons { get; } = new ObservableCollection<Person>();

    // Filters
    public DateTime? DateFrom { get => Get<DateTime?>(nameof(DateFrom)); set => Set(nameof(DateFrom), value); }
    public DateTime? DateTo { get => Get<DateTime?>(nameof(DateTo)); set => Set(nameof(DateTo), value); }
    public string FirstNameFilter { get => Get<string>(nameof(FirstNameFilter)); set => Set(nameof(FirstNameFilter), value); }
    public string LastNameFilter { get => Get<string>(nameof(LastNameFilter)); set => Set(nameof(LastNameFilter), value); }
    public string SurNameFilter { get => Get<string>(nameof(SurNameFilter)); set => Set(nameof(SurNameFilter), value); }
    public string CityFilter { get => Get<string>(nameof(CityFilter)); set => Set(nameof(CityFilter), value); }
    public string CountryFilter { get => Get<string>(nameof(CountryFilter)); set => Set(nameof(CountryFilter), value); }

    // Progress & status
    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (_isBusy == value) return;
            _isBusy = value;
            OnPropertyChanged();
            CommandManager.InvalidateRequerySuggested();
        }
    }


    private double _importProgress;
    public double ImportProgress
    {
        get => _importProgress;
        set
        {
            if (Math.Abs(_importProgress - value) < 0.001)
                return;

            _importProgress = value;
            OnPropertyChanged();
        }
    }

    // Export fields
    public ExportFieldsModel ExportFields { get; } = new ExportFieldsModel();

    // Commands
    public ICommand ApplyFiltersCommand { get; }
    public ICommand ImportCommand { get; }
    public ICommand ClearDatabaseCommand { get; }
    public ICommand ExportExcelCommand { get; }
    public ICommand ExportXmlCommand { get; }

    // Dialog providers 
    public Func<string, string, string?> ShowOpenFileDialog { get; set; }
    public Func<string, string, string, string?> ShowSaveFileDialog { get; set; }

    public MainViewModel(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));

        ImportCommand = new RelayCommand(
            async _ => await ImportAsync(),
            _ => !IsBusy
        );

        ExportExcelCommand = new RelayCommand(
            async _ => await ExportExcelAsync(),
            _ => !IsBusy
        );

        ExportXmlCommand = new RelayCommand(
            async _ => await ExportXmlAsync(),
            _ => !IsBusy
        );

        ClearDatabaseCommand = new RelayCommand(
            async _ => await ClearDatabaseAsync(),
            _ => !IsBusy
        );

        ApplyFiltersCommand = new RelayCommand(
            async _ => await ApplyFiltersAsync(),
            _ => !IsBusy
        );


        // initial preview load
        _ = ApplyFiltersAsync();
    }

    private CsvImporter CreateImporter() => new CsvImporter(_connectionString);
    private ExportService CreateExportService() => new ExportService(_connectionString);

    // Builds a query factory based on filters.
    private Func<IQueryable<Person>, IQueryable<Person>> BuildQuery()
    {
        var df = DateFrom;
        var dt = DateTo;
        var fn = FirstNameFilter;
        var ln = LastNameFilter;
        var sn = SurNameFilter;
        var city = CityFilter;
        var country = CountryFilter;

        return q =>
        {
            if (df.HasValue) q = q.Where(p => p.Date >= df.Value);
            if (dt.HasValue) q = q.Where(p => p.Date <= dt.Value);
            if (!string.IsNullOrWhiteSpace(fn)) q = q.Where(p => p.FirstName == fn);
            if (!string.IsNullOrWhiteSpace(ln)) q = q.Where(p => p.LastName == ln);
            if (!string.IsNullOrWhiteSpace(sn)) q = q.Where(p => p.SurName == sn);
            if (!string.IsNullOrWhiteSpace(city)) q = q.Where(p => p.City == city);
            if (!string.IsNullOrWhiteSpace(country)) q = q.Where(p => p.Country == country);
            
            return q.OrderBy(p => p.Id);
        };
    }

    /// <summary>
    /// Stream data to ObservableCollection with incremental UI updates.
    /// </summary>
    public async Task ApplyFiltersAsync()
    {
        if (IsBusy) return;
        
        if (!await _operationLock.WaitAsync(0))
            return;

        IsBusy = true;

        try
        {
            await ReloadPersonsInternalAsync();
        }
        finally
        {
            IsBusy = false;
            _operationLock.Release();
        }
    }


    public async Task ImportAsync()
    {
        if (IsBusy) return;
        
        var path = ShowOpenFileDialog.Invoke("CSV files (*.csv)|*.csv", "Select CSV");
        if (string.IsNullOrWhiteSpace(path)) return;

        if (!await _operationLock.WaitAsync(0))
        {
            return;
        }

        IsBusy = true;
        ImportProgress = 0;

        try
        {
            var importer = CreateImporter();
            var progress = new Progress<(long processed, long total, Person? person)>(p =>
            {
                ImportProgress = Math.Max(
                    1.0,
                    p.processed * 100.0 / p.total
                );

                if (p.person != null && p.processed < 1000)
                {
                    p.person.Id = (int)p.processed;
                    Persons.Add(p.person);
                }
            });

            await importer.ImportAsync(path, progress, CancellationToken.None);

            await ReloadPersonsInternalAsync(); 
        }
        catch (Exception ex)
        {
            Console.Write($"Import error: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
            _operationLock.Release();
        }
    }
    
    private async Task ReloadPersonsInternalAsync()
    {
        Persons.Clear();

        await using var repo = new RepositoryFactory(_connectionString);
        
        var query = BuildQuery();

        await foreach (var item in query(repo.Persons.Query())
                           .Take(1000)
                           .AsAsyncEnumerable())
        {
            await Application.Current.Dispatcher.InvokeAsync(() => Persons.Add(item));
        }
    }

    public async Task ClearDatabaseAsync()
    {
        if (IsBusy) return;
        
        if (MessageBox.Show(
                "Are you sure you want to clear the database?",
                "Confirm",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

        if (!await _operationLock.WaitAsync(0))
        {
            return;
        }

        IsBusy = true;

        try
        {
            await using var repo = new RepositoryFactory(_connectionString);
            await repo.Persons.ClearAsync();
            Persons.Clear();
        }
        catch (Exception ex)
        {
            Console.Write($"Clear error: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
            _operationLock.Release();
        }
    }

    public async Task ExportExcelAsync()
    {
        if (IsBusy) return;
        
        var fName = ShowSaveFileDialog.Invoke("Excel (*.xlsx)|*.xlsx", "Save Excel", "export.xlsx");
        if (string.IsNullOrWhiteSpace(fName)) return;

        if (!await _operationLock.WaitAsync(0))
        {
            return;
        }

        IsBusy = true;

        try
        {
            var query = BuildQuery();
            var exportService = CreateExportService();
            var fields = GetSelectedFields();
            
            var progress = new Progress<(long processed, long total)>(p =>
            {
                ImportProgress = p.total > 0
                    ? p.processed * 100.0 /  p.total
                    : 0;
            });

            await exportService.ExportExcelAsync(fName, query, fields, progress);
        }
        catch (Exception ex)
        {
            Console.Write($"Export error: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
            _operationLock.Release();
        }
    }

    public async Task ExportXmlAsync()
    {
        if (IsBusy) return;
        
        var fName = ShowSaveFileDialog.Invoke("XML (*.xml)|*.xml", "Save XML", "export.xml");
        if (string.IsNullOrWhiteSpace(fName)) return;

        if (!await _operationLock.WaitAsync(0))
        {
            return;
        }

        IsBusy = true;

        try
        {
            var query = BuildQuery();
            var exportService = CreateExportService();
            
            var progress = new Progress<(long processed, long total)>(p =>
            {
                ImportProgress = p.total > 0
                    ? p.processed * 100.0 /  p.total
                    : 0;
            });
            
            await exportService.ExportXmlAsync(fName, query, progress);
        }
        catch (Exception ex)
        { 
            Console.Write($"Export error: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
            _operationLock.Release();
        }
    }

    private string[] GetSelectedFields()
    {
        var list = new List<string>();
        if (ExportFields.Date) list.Add("Date");
        if (ExportFields.FirstName) list.Add("FirstName");
        if (ExportFields.LastName) list.Add("LastName");
        if (ExportFields.SurName) list.Add("SurName");
        if (ExportFields.City) list.Add("City");
        if (ExportFields.Country) list.Add("Country");
        return list.ToArray();
    }
}

// Export checkboxes
public class ExportFieldsModel : ObservableObject
{
    public bool Date { get => Get<bool>(nameof(Date)); set => Set(nameof(Date), value); }
    public bool FirstName { get => Get<bool>(nameof(FirstName)); set => Set(nameof(FirstName), value); }
    public bool LastName { get => Get<bool>(nameof(LastName)); set => Set(nameof(LastName), value); }
    public bool SurName { get => Get<bool>(nameof(SurName)); set => Set(nameof(SurName), value); }
    public bool City { get => Get<bool>(nameof(City)); set => Set(nameof(City), value); }
    public bool Country { get => Get<bool>(nameof(Country)); set => Set(nameof(Country), value); }

    public ExportFieldsModel()
    {
        Date = true;
        FirstName = true;
        LastName = true;
        SurName = true;
        City = true;
        Country = true;
    }
}
