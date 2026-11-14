using GreenHive.Wpf.Framework.Docking;
using GreenHive.NET.ModuleContracts;
using GreenHive.Wpf.Framework.Mvvm;

namespace GreenHive.Wpf.Framework.Navigation;

/// <summary>What a wheel segment stands for.</summary>
public enum WheelSegmentKind
{
    Module,
    OpenView,
}

/// <summary>One segment of the navigation wheel.</summary>
public sealed class WheelSegment
{
    public required string Key { get; init; }

    public required string Label { get; init; }

    public string? Icon { get; init; }

    /// <summary>0 = inner ring (modules), 1 = outer ring (open documents and windows).</summary>
    public int Ring { get; init; }

    public WheelSegmentKind Kind { get; init; }

    public bool IsFloating { get; init; }
}

/// <summary>
/// State and logic of the navigation wheel (Ctrl+Tab): inner ring = modules (most recently used first),
/// outer ring = open documents and floating views. Built dynamically each time it opens.
/// </summary>
public sealed class NavigationWheelViewModel : ViewModelAppObject
{
    private IReadOnlyList<WheelSegment> _segments = [];
    private WheelSegment? _selected;
    private bool _isOpen;

    /// <summary>Raised when segments or selection changed (the control redraws).</summary>
    public event EventHandler? Changed;

    public IReadOnlyList<WheelSegment> Segments => _segments;

    public WheelSegment? Selected => _selected;

    public bool IsOpen
    {
        get => _isOpen;
        private set => SetProperty(ref _isOpen, value);
    }

    public string CenterTitle => _selected?.Label ?? string.Empty;

    public string CenterSubtitle => _selected is null
        ? string.Empty
        : AppContext.Languages.GetText(_selected.Kind == WheelSegmentKind.Module ? "shell:wheel.modules" : "shell:wheel.documents");

    private NavigationService Navigation => Context<INavigationContext>().Navigation;

    private IDockService Dock => Context<IDockContext>().Dock;

    /// <summary>Builds the segments and preselects the previous module (or the last one when reversed).</summary>
    public void Prepare(bool reverse)
    {
        var history = Navigation.History;
        var activeModule = Dock.ActiveItem?.ModuleId;
        var modules = Navigation.Registry.ModuleHomes
            .OrderBy(t => t.ModuleId.Equals(activeModule, StringComparison.OrdinalIgnoreCase) ? -1 : history.Rank(t.ModuleId))
            .ThenBy(t => t.Order)
            .Select(t => new WheelSegment
            {
                Key = t.Key,
                Label = t.TextKey is null ? t.Key : AppContext.Languages.GetText(t.TextKey + ".label"),
                Icon = t.Icon,
                Ring = 0,
                Kind = WheelSegmentKind.Module,
            })
            .ToList();

        var openViews = Dock.ActivationOrder
            .Where(i => i.Descriptor.Role == ViewRole.Document || i.Location == DockSide.Floating)
            .Select(i => new WheelSegment
            {
                Key = i.Key,
                Label = i.Title,
                Icon = i.Icon,
                Ring = 1,
                Kind = WheelSegmentKind.OpenView,
                IsFloating = i.Location == DockSide.Floating,
            })
            .ToList();

        _segments = modules.Concat(openViews).ToArray();
        var ring = modules.Count > 0 ? modules : openViews;
        _selected = ring.Count switch
        {
            0 => null,
            1 => ring[0],
            _ => reverse ? ring[^1] : ring[1],
        };

        IsOpen = true;
        RaiseChanged();
    }

    /// <summary>Next (+1) or previous (-1) segment in the current ring.</summary>
    public void Move(int step)
    {
        if (_selected is null)
        {
            return;
        }

        var ring = _segments.Where(s => s.Ring == _selected.Ring).ToList();
        var index = ring.IndexOf(_selected);
        _selected = ring[((index + step) % ring.Count + ring.Count) % ring.Count];
        RaiseChanged();
    }

    /// <summary>Switches between the inner and the outer ring.</summary>
    public void SwitchRing()
    {
        var targetRing = _selected?.Ring == 0 ? 1 : 0;
        var candidate = _segments.FirstOrDefault(s => s.Ring == targetRing);
        if (candidate is not null)
        {
            _selected = candidate;
            RaiseChanged();
        }
    }

    /// <summary>Selects the segment at an angle (degrees, clockwise from the top) in a ring.</summary>
    public void SelectAt(int ring, double angle)
    {
        var segments = _segments.Where(s => s.Ring == ring).ToList();
        if (segments.Count == 0)
        {
            return;
        }

        var sweep = 360.0 / segments.Count;
        var index = (int)Math.Floor(((angle + (sweep / 2)) % 360 + 360) % 360 / sweep) % segments.Count;
        if (!ReferenceEquals(_selected, segments[index]))
        {
            _selected = segments[index];
            RaiseChanged();
        }
    }

    /// <summary>Opens the selection and closes the wheel.</summary>
    public void Confirm()
    {
        var selected = _selected;
        Close();
        if (selected is null)
        {
            return;
        }

        if (selected.Kind == WheelSegmentKind.Module)
        {
            Navigation.NavigateTo(selected.Key);
        }
        else if (Dock.Find(selected.Key) is { } item)
        {
            Dock.Activate(item);
        }
    }

    public void Cancel() => Close();

    private void Close()
    {
        IsOpen = false;
        _selected = null;
        RaiseChanged();
    }

    private void RaiseChanged()
    {
        OnPropertyChanged(nameof(Selected));
        OnPropertyChanged(nameof(CenterTitle));
        OnPropertyChanged(nameof(CenterSubtitle));
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
