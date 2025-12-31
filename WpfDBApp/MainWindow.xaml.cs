using MahApps.Metro.Controls;
using WpfDBApp.ViewModels;

namespace WpfDBApp;

public partial class MainWindow : MetroWindow
{
    public MainWindow()
    {
        InitializeComponent();

        var vm = new MainViewModel(App.ConnectionString);

        // Dialog providers
        vm.ShowOpenFileDialog = (filter, title) =>
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = filter,
                Title = title
            };
            return dlg.ShowDialog() == true ? dlg.FileName : null;
        };

        vm.ShowSaveFileDialog = (filter, title, defaultName) =>
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = filter,
                Title = title,
                FileName = defaultName
            };
            return dlg.ShowDialog() == true ? dlg.FileName : null;
        };

        DataContext = vm;
    }
}