using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WpfDBApp.Helpers;

// Data storage for ViewModel props
public class ObservableObject : INotifyPropertyChanged
{
    private readonly ConcurrentDictionary<string, object> _store = new ConcurrentDictionary<string, object>(); // To avoid race condition

    public event PropertyChangedEventHandler? PropertyChanged;

    protected T Get<T>(string name)
    {
        if (_store.TryGetValue(name, out var val))
            return (T)val;
        return default!;
    }

    protected bool Set<T>(string name, T value, [CallerMemberName] string? caller = null)
    {
        _store[name] = value!;
        OnPropertyChanged(caller ?? name);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}