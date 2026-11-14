using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using GreenHive.NET.Core.Common.Command;
using GreenHive.NET.Core.Common.MVVM.ViewModelSupport;
using GreenHive.Wpf.UI.Common.Shortcuts;

namespace GreenHive.Wpf.UI.MVVM.ViewModel;

public class ShortcutManagerViewModel : NotificationObject
{
    private ObservableCollection<ShortcutCategoryGroup> _categories = new();
    private ShortcutItemViewModel? _selectedShortcut;
    private string _searchText = string.Empty;
    private string _conflictMessage = string.Empty;

    public ObservableCollection<ShortcutCategoryGroup> Categories
    {
        get => _categories;
        set => SetProperty(ref _categories, value);
    }

    public ShortcutItemViewModel? SelectedShortcut
    {
        get => _selectedShortcut;
        set => SetProperty(ref _selectedShortcut, value);
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                FilterShortcuts();
            }
        }
    }

    public string ConflictMessage
    {
        get => _conflictMessage;
        set => SetProperty(ref _conflictMessage, value);
    }

    public ICommand SaveCommand { get; }
    public ICommand ResetAllCommand { get; }
    public ICommand ResetSelectedCommand { get; }
    public ICommand ClearKeyCommand { get; }
    public ICommand ExportCommand { get; }
    public ICommand ImportCommand { get; }

    private List<ShortcutItemViewModel> _allShortcuts = new();

    public ShortcutManagerViewModel()
    {
        SaveCommand = new RelayCommand(_ => Save());
        ResetAllCommand = new RelayCommand(_ => ResetAll());
        ResetSelectedCommand = new RelayCommand(_ => ResetSelected(), _ => SelectedShortcut != null);
        ClearKeyCommand = new RelayCommand(_ => ClearKey(), _ => SelectedShortcut != null);
        ExportCommand = new RelayCommand(_ => Export());
        ImportCommand = new RelayCommand(_ => Import());

        LoadShortcuts();

        ShortcutManager.Instance.ShortcutChanged += OnShortcutChanged;
    }

    private void LoadShortcuts()
    {
        _allShortcuts.Clear();

        foreach (var shortcut in ShortcutManager.Instance.RegisteredShortcuts)
        {
            var vm = new ShortcutItemViewModel(shortcut);
            vm.KeyGestureChanged += OnKeyGestureChanged;
            _allShortcuts.Add(vm);
        }

        GroupShortcuts();
    }

    private void GroupShortcuts()
    {
        var filtered = string.IsNullOrWhiteSpace(SearchText)
            ? _allShortcuts
            : _allShortcuts.Where(s =>
                s.DisplayName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                s.Description.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                s.Category.Contains(SearchText, StringComparison.OrdinalIgnoreCase)).ToList();

        var grouped = filtered
            .GroupBy(s => s.Category)
            .OrderBy(g => g.Key)
            .Select(g => new ShortcutCategoryGroup
            {
                CategoryName = g.Key,
                Shortcuts = new ObservableCollection<ShortcutItemViewModel>(g.OrderBy(s => s.DisplayName))
            })
            .ToList();

        Categories = new ObservableCollection<ShortcutCategoryGroup>(grouped);
    }

    private void FilterShortcuts()
    {
        GroupShortcuts();
    }

    private void OnKeyGestureChanged(object? sender, EventArgs e)
    {
        if (sender is ShortcutItemViewModel shortcutVm && shortcutVm.KeyGesture != null)
        {
            // Check for conflicts
            if (ShortcutManager.Instance.IsGestureInUse(shortcutVm.KeyGesture, out var existing) 
                && existing?.Id != shortcutVm.Id)
            {
                ConflictMessage = $"?? Konflikt: Bereits verwendet von '{existing?.DisplayName}'";
            }
            else
            {
                ConflictMessage = string.Empty;
            }
        }
        else
        {
            ConflictMessage = string.Empty;
        }
    }

    private void OnShortcutChanged(object? sender, ShortcutChangedEventArgs e)
    {
        // Reload shortcuts if changed externally
        LoadShortcuts();
    }

    private void Save()
    {
        try
        {
            foreach (var shortcutVm in _allShortcuts)
            {
                ShortcutManager.Instance.UpdateShortcutKey(shortcutVm.Id, shortcutVm.KeyGesture);
                
                var shortcut = ShortcutManager.Instance.GetShortcut(shortcutVm.Id);
                if (shortcut != null)
                {
                    shortcut.IsEnabled = shortcutVm.IsEnabled;
                }
            }

            ShortcutManager.Instance.SaveConfiguration();
            ConflictMessage = "? Shortcuts erfolgreich gespeichert!";
        }
        catch (Exception ex)
        {
            ConflictMessage = $"? Fehler beim Speichern: {ex.Message}";
        }
    }

    private void ResetAll()
    {
        ShortcutManager.Instance.ResetAllToDefaults();
        LoadShortcuts();
        ConflictMessage = "? Alle Shortcuts zurückgesetzt!";
    }

    private void ResetSelected()
    {
        if (SelectedShortcut != null)
        {
            ShortcutManager.Instance.ResetToDefault(SelectedShortcut.Id);
            var shortcut = ShortcutManager.Instance.GetShortcut(SelectedShortcut.Id);
            if (shortcut != null)
            {
                SelectedShortcut.UpdateFromShortcut(shortcut);
            }
            ConflictMessage = $"? Shortcut '{SelectedShortcut.DisplayName}' zurückgesetzt!";
        }
    }

    private void ClearKey()
    {
        if (SelectedShortcut != null)
        {
            SelectedShortcut.KeyGesture = null;
            ConflictMessage = string.Empty;
        }
    }

    private void Export()
    {
        try
        {
            ShortcutManager.Instance.SaveConfiguration();
            var configPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "GreenHive", "shortcuts.json");
            System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{configPath}\"");
        }
        catch (Exception ex)
        {
            ConflictMessage = $"? Fehler beim Export: {ex.Message}";
        }
    }

    private void Import()
    {
        try
        {
            ShortcutManager.Instance.LoadConfiguration();
            LoadShortcuts();
            ConflictMessage = "? Shortcuts erfolgreich importiert!";
        }
        catch (Exception ex)
        {
            ConflictMessage = $"? Fehler beim Import: {ex.Message}";
        }
    }
}

public class ShortcutCategoryGroup
{
    public string CategoryName { get; set; } = string.Empty;
    public ObservableCollection<ShortcutItemViewModel> Shortcuts { get; set; } = new();
}

public class ShortcutItemViewModel : NotificationObject
{
    private KeyGesture? _keyGesture;
    private bool _isEnabled;
    private bool _isRecording;

    public string Id { get; }
    public string DisplayName { get; }
    public string Description { get; }
    public string Category { get; }
    public bool IsModifiable { get; }
    public KeyGesture? DefaultKeyGesture { get; }

    public KeyGesture? KeyGesture
    {
        get => _keyGesture;
        set
        {
            if (SetProperty(ref _keyGesture, value))
            {
                OnPropertyChanged(nameof(KeyGestureText));
                KeyGestureChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public string KeyGestureText => KeyGesture?.GetDisplayStringForCulture(System.Globalization.CultureInfo.CurrentCulture) ?? "Nicht zugewiesen";

    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }

    public bool IsRecording
    {
        get => _isRecording;
        set => SetProperty(ref _isRecording, value);
    }

    public event EventHandler? KeyGestureChanged;

    public ShortcutItemViewModel(ShortcutAction shortcut)
    {
        Id = shortcut.Id;
        DisplayName = shortcut.DisplayName;
        Description = shortcut.Description;
        Category = shortcut.Category;
        IsModifiable = shortcut.IsModifiable;
        DefaultKeyGesture = shortcut.DefaultKeyGesture;
        _keyGesture = shortcut.KeyGesture;
        _isEnabled = shortcut.IsEnabled;
    }

    public void UpdateFromShortcut(ShortcutAction shortcut)
    {
        KeyGesture = shortcut.KeyGesture;
        IsEnabled = shortcut.IsEnabled;
    }
}
