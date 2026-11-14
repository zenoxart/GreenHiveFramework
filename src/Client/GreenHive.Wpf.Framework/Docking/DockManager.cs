using System.ComponentModel;
using GreenHive.Wpf.Framework.Commands;
using GreenHive.NET.ModuleContracts;
using GreenHive.Wpf.Framework.Mvvm;
using GreenHive.Wpf.Framework.Windowing;
using GreenHive.Wpf.Framework.XamlMetadata;
using GreenHive.Wpf.Framework.Theming;

namespace GreenHive.Wpf.Framework.Docking;

/// <summary>
/// Central service of the dock layout (Visual Studio style): opens views as documents or tool windows,
/// floats and docks them back, tracks the active item and saves/restores the layout in AppData.
/// </summary>
public sealed class DockManager : AppObject, IDockService
{
    private readonly Dictionary<string, DockItem> _items = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<DockItem> _activationOrder = [];
    private readonly List<DockedItemState> _placeholders = [];
    private DockLayoutStore? _store;
    private bool _restoring;
    private bool _shuttingDown;

    /// <summary>Raised after items were opened, closed, moved or resized.</summary>
    public event EventHandler? LayoutChanged;

    /// <summary>Raised when another item became active.</summary>
    public event EventHandler? ActiveItemChanged;

    public DockHost? Host { get; private set; }

    /// <summary>Creates dock items from saved keys (set by the navigation service).</summary>
    public Func<string, string?, DockItem?>? ItemResolver { get; set; }

    public DockItem? ActiveItem { get; private set; }

    public DockItem? ActiveDocument => Host?.ActiveDocument;

    /// <summary>The floating item that was active last (target of "dock back").</summary>
    public DockItem? LastActiveFloating { get; private set; }

    public IReadOnlyCollection<DockItem> OpenItems => _items.Values;

    /// <summary>Open items, most recently activated first.</summary>
    public IReadOnlyList<DockItem> ActivationOrder => _activationOrder;

    internal IconCatalog? Icons => (AppContext as IIconContext)?.Icons;

    private DockLayoutStore Store => _store ??= AppContext.Produce<DockLayoutStore>();

    /// <summary>Creates the dock host for the main window.</summary>
    public DockHost CreateHost()
    {
        Host = new DockHost(this);
        return Host;
    }

    public DockItem? Find(string key) => _items.TryGetValue(key, out var item) ? item : null;

    /// <summary>Opens an item at its default (or the given) location; an open item is activated instead.</summary>
    public void Open(DockItem item, DockSide? location = null, bool activate = true)
    {
        if (_items.TryGetValue(item.Key, out var existing))
        {
            if (activate)
            {
                Activate(existing);
            }

            return;
        }

        _items[item.Key] = item;
        item.ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        var side = location ?? DefaultLocation(item);
        item.LastDockedSide = side == DockSide.Floating ? DefaultDockedSide(item) : side;
        Place(item, side, activate);
        if (activate)
        {
            Activate(item);
        }

        OnLayoutChanged();
    }

    public void Activate(DockItem item)
    {
        if (item.Location == DockSide.Floating)
        {
            (item.FloatingWindow as FloatingDockWindow)?.ActivateWindow();
        }
        else
        {
            Host?.Select(item);
        }

        OnItemActivated(item);
    }

    /// <summary>Closes an item; returns false if its ViewModel refused.</summary>
    public bool Close(DockItem? item)
    {
        if (item is null || !_items.ContainsKey(item.Key) || !item.ViewModel.CanClose())
        {
            return false;
        }

        if (item.Location == DockSide.Floating)
        {
            var window = item.FloatingWindow as FloatingDockWindow;
            item.FloatingWindow = null;
            window?.CloseSilently();
            item.View = null;
        }
        else
        {
            Host?.Remove(item);
        }

        _items.Remove(item.Key);
        _activationOrder.Remove(item);
        item.ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        item.ViewModel.OnClosed();
        if (ActiveItem == item)
        {
            ActiveItem = _activationOrder.FirstOrDefault();
            ActiveItemChanged?.Invoke(this, EventArgs.Empty);
        }

        if (LastActiveFloating == item)
        {
            LastActiveFloating = null;
        }

        OnLayoutChanged();
        return true;
    }

    public void CloseOtherDocuments(DockItem keep)
    {
        foreach (var item in _items.Values.Where(i => i != keep && i.Location == DockSide.DocumentWell).ToList())
        {
            Close(item);
        }
    }

    /// <summary>Moves an item into its own floating window (detach).</summary>
    public void Float(DockItem? item)
    {
        if (item is null || item.Location == DockSide.Floating || !item.Descriptor.CanFloat)
        {
            return;
        }

        Host?.Remove(item);
        ShowFloating(item);
        Activate(item);
        OnLayoutChanged();
    }

    /// <summary>Docks a floating item back to its last docked position (attach).</summary>
    public void Dock(DockItem? item)
    {
        if (item is null || item.Location != DockSide.Floating)
        {
            return;
        }

        var window = item.FloatingWindow as FloatingDockWindow;
        item.FloatingWindow = null;
        window?.CloseSilently();
        if (LastActiveFloating == item)
        {
            LastActiveFloating = null;
        }

        Place(item, item.LastDockedSide, true);
        Activate(item);
        OnLayoutChanged();
    }

    public void NextDocument() => Host?.CycleDocument(+1);

    public void PreviousDocument() => Host?.CycleDocument(-1);

    /// <summary>Pins or unpins (auto-hide) the pane of a side.</summary>
    public void TogglePin(DockSide side)
    {
        Host?.TogglePin(side);
        OnLayoutChanged();
    }

    public void TogglePane(DockSide side)
    {
        Host?.TogglePane(side);
        OnLayoutChanged();
    }

    public bool IsPaneVisible(DockSide side) => Host?.IsPaneVisible(side) ?? false;

    public void ApplySizeClass(SizeClass sizeClass) => Host?.ApplySizeClass(sizeClass);

    /// <summary>Restores the saved layout, or opens the default views when there is none.</summary>
    public void RestoreLayout(IReadOnlyList<string> defaultNavigationKeys)
    {
        _restoring = true;
        try
        {
            var state = Store.Load();
            if (state is null || state.Items.Count == 0)
            {
                foreach (var key in defaultNavigationKeys)
                {
                    if (ItemResolver?.Invoke(key, null) is { } item)
                    {
                        Open(item, activate: false);
                    }
                }
            }
            else
            {
                Host?.ApplyState(state);
                foreach (var saved in state.Items)
                {
                    var item = ItemResolver?.Invoke(saved.NavigationKey, saved.Parameter);
                    if (item is null)
                    {
                        // Module not available right now: keep its place for the next start.
                        _placeholders.Add(saved);
                        continue;
                    }

                    var location = Enum.TryParse<DockSide>(saved.Location, out var parsed) ? parsed : DefaultLocation(item);
                    Open(item, location, activate: false);
                    if (Enum.TryParse<DockSide>(saved.LastDockedSide, out var lastDocked) && lastDocked != DockSide.Floating)
                    {
                        item.LastDockedSide = lastDocked;
                    }
                }

                if (state.ActiveDocument is { } active && Find(active) is { } activeItem)
                {
                    Activate(activeItem);
                }
            }

            if (ActiveDocument is { } document)
            {
                OnItemActivated(document);
            }
        }
        finally
        {
            _restoring = false;
        }
    }

    /// <summary>Puts every tool window back to its default side and resets the pane sizes.</summary>
    public void ResetLayout()
    {
        Host?.ResetSizes();
        foreach (var item in _items.Values.ToList())
        {
            var target = DefaultLocation(item);
            if (item.Location == target || target == DockSide.Floating)
            {
                continue;
            }

            if (item.Location == DockSide.Floating)
            {
                item.LastDockedSide = target;
                Dock(item);
            }
            else
            {
                Host?.Remove(item);
                Place(item, target, false);
            }
        }

        OnLayoutChanged();
    }

    /// <summary>Saves the layout and closes floating windows without closing their views (app exit).</summary>
    public void Shutdown()
    {
        SaveLayout();
        _shuttingDown = true;
        foreach (var item in _items.Values.Where(i => i.FloatingWindow is not null).ToList())
        {
            (item.FloatingWindow as FloatingDockWindow)?.CloseSilently();
        }
    }

    public void SaveLayout()
    {
        if (_restoring || _shuttingDown || Host is null)
        {
            return;
        }

        var state = new DockLayoutState
        {
            LeftWidth = Host.LeftWidth,
            RightWidth = Host.RightWidth,
            BottomHeight = Host.BottomHeight,
            LeftShown = Host.Left.IsShown,
            RightShown = Host.Right.IsShown,
            BottomShown = Host.Bottom.IsShown,
            LeftPinned = Host.Left.IsPinned,
            RightPinned = Host.Right.IsPinned,
            BottomPinned = Host.Bottom.IsPinned,
            ActiveDocument = ActiveDocument?.Key,
        };

        state.Items.AddRange(Host.ItemsIn(DockSide.DocumentWell).Select(ToState));
        state.Items.AddRange(Host.ItemsIn(DockSide.Left).Select(ToState));
        state.Items.AddRange(Host.ItemsIn(DockSide.Right).Select(ToState));
        state.Items.AddRange(Host.ItemsIn(DockSide.Bottom).Select(ToState));
        state.Items.AddRange(_items.Values.Where(i => i.Location == DockSide.Floating).Select(ToState));
        state.Items.AddRange(_placeholders);
        Store.Save(state);
    }

    internal void OnItemActivated(DockItem item)
    {
        if (!_items.ContainsKey(item.Key))
        {
            return;
        }

        _activationOrder.Remove(item);
        _activationOrder.Insert(0, item);
        if (item.Location == DockSide.Floating)
        {
            LastActiveFloating = item;
        }

        if (ActiveItem != item)
        {
            ActiveItem = item;
            ActiveItemChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    internal void OnFloatingClosed(FloatingDockWindow window)
    {
        if (window.IsClosingSilently || _shuttingDown)
        {
            return;
        }

        // The user closed the floating window: close its view.
        var item = window.Item;
        item.FloatingWindow = null;
        item.View = null;
        _items.Remove(item.Key);
        _activationOrder.Remove(item);
        item.ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        item.ViewModel.OnClosed();
        if (LastActiveFloating == item)
        {
            LastActiveFloating = null;
        }

        OnLayoutChanged();
    }

    internal void OnLayoutChanged()
    {
        if (_restoring)
        {
            return;
        }

        SaveLayout();
        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnCreated() =>
        AppContext.Languages.LanguageChanged += (_, _) =>
        {
            foreach (var item in _items.Values.Where(i => i.TitleKey is not null))
            {
                item.ViewModel.Title = AppContext.Languages.GetText(item.TitleKey!);
            }
        };

    private static DockSide DefaultLocation(DockItem item) =>
        item.Descriptor.Role == ViewRole.ToolWindow
            ? item.Descriptor.DefaultDock == DockSide.DocumentWell ? DockSide.Right : item.Descriptor.DefaultDock
            : item.Descriptor.DefaultDock == DockSide.Floating ? DockSide.Floating : DockSide.DocumentWell;

    private static DockSide DefaultDockedSide(DockItem item) =>
        DefaultLocation(item) is var side && side == DockSide.Floating
            ? item.Descriptor.Role == ViewRole.ToolWindow ? DockSide.Right : DockSide.DocumentWell
            : side;

    private static DockedItemState ToState(DockItem item) => new()
    {
        NavigationKey = item.NavigationKey,
        Parameter = item.Parameter,
        Location = item.Location.ToString(),
        LastDockedSide = item.LastDockedSide.ToString(),
    };

    private void Place(DockItem item, DockSide side, bool select)
    {
        if (side == DockSide.Floating)
        {
            ShowFloating(item);
            return;
        }

        if (Host is null)
        {
            throw new InvalidOperationException("CreateHost must be called before views are opened.");
        }

        item.View = item.CreateView();
        item.Location = side;
        item.LastDockedSide = side;
        Host.Add(item, side, select);
    }

    private void ShowFloating(DockItem item, System.Windows.Point? screenPosition = null)
    {
        item.View = item.CreateView();
        item.Location = DockSide.Floating;
        var window = new FloatingDockWindow(this, item);
        item.FloatingWindow = window;
        if (System.Windows.Application.Current?.MainWindow is { IsLoaded: true } main && !ReferenceEquals(main, window))
        {
            window.Owner = main;
        }

        (AppContext as IWindowContext)?.Windows.Track(window, "float:" + item.Key, item.Descriptor.MinWidth, item.Descriptor.MinHeight);
        if (screenPosition is { } position)
        {
            window.Left = position.X - 40;
            window.Top = position.Y - 12;
        }

        window.ActivateWindow();
        LastActiveFloating = item;
    }

    /// <summary>Moves an item to another side (dock guides, tab context menu).</summary>
    public void MoveTo(DockItem item, DockSide side, System.Windows.Point? screenPosition = null)
    {
        if (!_items.ContainsKey(item.Key) || item.Location == side)
        {
            return;
        }

        if (side == DockSide.Floating)
        {
            if (!item.Descriptor.CanFloat)
            {
                return;
            }

            Host?.Remove(item);
            ShowFloating(item, screenPosition);
        }
        else if (item.Location == DockSide.Floating)
        {
            item.LastDockedSide = side;
            Dock(item);
            return;
        }
        else
        {
            Host?.Remove(item);
            Place(item, side, true);
        }

        Activate(item);
        OnLayoutChanged();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ViewModelAppObject.Title))
        {
            return;
        }

        foreach (var item in _items.Values.Where(i => ReferenceEquals(i.ViewModel, sender)))
        {
            Host?.UpdateTitle(item);
            (item.FloatingWindow as FloatingDockWindow)?.UpdateTitle();
        }
    }
}
