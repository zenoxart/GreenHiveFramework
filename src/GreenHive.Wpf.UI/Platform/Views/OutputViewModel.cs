using System.Collections.ObjectModel;
using GreenHive.Wpf.Framework.Mvvm;

namespace GreenHive.Wpf.UI.Platform.Views;

/// <summary>ViewModel of the Output tool window (the application log).</summary>
public sealed class OutputViewModel : ViewModelAppObject
{
    private bool _autoScroll = true;

    public ObservableCollection<LogEntry> Entries => AppContext.Log.Entries;

    public bool AutoScroll
    {
        get => _autoScroll;
        set => SetProperty(ref _autoScroll, value);
    }

    public void Clear() => AppContext.Log.Clear();
}
