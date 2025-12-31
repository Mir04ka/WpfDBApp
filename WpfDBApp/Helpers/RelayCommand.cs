using System.Windows.Input;

namespace WpfDBApp.Helpers;

// Relay between UI and ViewModel
public class RelayCommand : ICommand
{
    private readonly Func<object, bool> _canExecute;
    private readonly Func<object, Task> _executeAsync;
    private readonly Action<object> _executeSync;

    public RelayCommand(Func<object, Task> executeAsync, Func<object, bool>? canExecute = null)
    {
        _executeAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
        _canExecute = canExecute ?? (_ => true);
    }

    public RelayCommand(Action<object> executeSync, Func<object, bool>? canExecute = null)
    {
        _executeSync = executeSync ?? throw new ArgumentNullException(nameof(executeSync));
        _canExecute = canExecute ?? (_ => true);
    }

    public bool CanExecute(object parameter) => _canExecute(parameter);

    public event EventHandler CanExecuteChanged
    {
        add { CommandManager.RequerySuggested += value; }
        remove { CommandManager.RequerySuggested -= value; }
    }

    public async void Execute(object parameter)
    {
        if (_executeAsync != null)
            await _executeAsync(parameter);
        else
            _executeSync.Invoke(parameter);
    }
}