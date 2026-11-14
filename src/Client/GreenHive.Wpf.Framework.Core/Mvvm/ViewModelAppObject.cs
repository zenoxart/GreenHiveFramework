using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace GreenHive.Wpf.Framework.Mvvm;

/// <summary>
/// Base class of all ViewModels in the host and in modules (WIFI: ViewModelAppObjekt).
/// Self-written INotifyPropertyChanged, busy counter with logging, size classes and lifecycle hooks.
/// </summary>
public abstract class ViewModelAppObject : AppObject, INotifyPropertyChanged, IResponsiveView
{
    private int _busyCounter;
    private string _title = string.Empty;
    private SizeClass _sizeClass = SizeClass.Expanded;
    private HeightClass _heightClass = HeightClass.Regular;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Title shown in tabs, window titles and the navigation wheel.</summary>
    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    /// <summary>True while at least one operation started with <see cref="BeginWork"/> is running.</summary>
    public bool IsBusy => Volatile.Read(ref _busyCounter) > 0;

    public SizeClass SizeClass
    {
        get => _sizeClass;
        set
        {
            if (SetProperty(ref _sizeClass, value))
            {
                OnSizeClassChanged();
            }
        }
    }

    public HeightClass HeightClass
    {
        get => _heightClass;
        set => SetProperty(ref _heightClass, value);
    }

    /// <summary>Called when the view was opened or navigated to.</summary>
    public virtual void OnNavigatedTo(object? parameter)
    {
    }

    /// <summary>Called when another view became active.</summary>
    public virtual void OnNavigatedFrom()
    {
    }

    /// <summary>Return false to keep the view open (e.g. unsaved changes).</summary>
    public virtual bool CanClose() => true;

    /// <summary>Called after the view was closed.</summary>
    public virtual void OnClosed()
    {
    }

    /// <summary>
    /// Raises PropertyChanged. WPF bindings are updated on the UI thread, so calls from background threads are
    /// marshalled through the context's dispatcher.
    /// </summary>
    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = "")
    {
        var handler = PropertyChanged;
        if (handler is null)
        {
            return;
        }

        var args = new PropertyChangedEventArgs(propertyName);
        var dispatcher = HasAppContext ? AppContext.Dispatcher : null;
        if (dispatcher is null || dispatcher.HasThreadAccess)
        {
            handler(this, args);
        }
        else
        {
            dispatcher.Run(() => handler(this, args));
        }
    }

    /// <summary>Assigns the value and raises PropertyChanged if it changed.</summary>
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = "")
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    /// <summary>Marks the start of an operation (WIFI: StartProtokollieren). Pair with <see cref="EndWork"/> in finally.</summary>
    protected void BeginWork([CallerMemberName] string methodName = "")
    {
        if (Interlocked.Increment(ref _busyCounter) == 1)
        {
            OnPropertyChanged(nameof(IsBusy));
        }

        if (HasAppContext)
        {
            AppContext.Log.Write($"{this}.{methodName} starts", LogEntryType.Start, ToString());
        }
    }

    /// <summary>Marks the end of an operation (WIFI: EndeProtokollieren).</summary>
    protected void EndWork([CallerMemberName] string methodName = "")
    {
        if (Interlocked.Decrement(ref _busyCounter) == 0)
        {
            OnPropertyChanged(nameof(IsBusy));
        }

        if (HasAppContext)
        {
            AppContext.Log.Write($"{this}.{methodName} ends", LogEntryType.End, ToString());
        }
    }

    protected virtual void OnSizeClassChanged()
    {
    }
}
