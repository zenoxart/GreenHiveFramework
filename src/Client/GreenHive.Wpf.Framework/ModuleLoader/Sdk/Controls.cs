using System.IO;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using GreenHive.Wpf.Framework.Theming;

namespace GreenHive.Wpf.Framework.ModuleLoader.Sdk;

/// <summary>A text box that shows <see cref="PlaceholderText"/> while it is empty.</summary>
public class HintTextBox : TextBox
{
    private readonly TextBlock _hint = new() { Opacity = 0.5, Margin = new Thickness(4, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
    private Brush? _background;
    private bool _updating;

    public HintTextBox()
    {
        TextChanged += (_, _) => UpdateHint();
        Loaded += (_, _) => UpdateHint();
    }

    public string? PlaceholderText
    {
        get => _hint.Text;
        set
        {
            _hint.Text = value ?? string.Empty;
            UpdateHint();
        }
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == BackgroundProperty && !_updating)
        {
            _background = e.NewValue as Brush;
        }
    }

    private void UpdateHint()
    {
        _updating = true;
        try
        {
            if (string.IsNullOrEmpty(Text) && !string.IsNullOrEmpty(_hint.Text))
            {
                _background ??= Background;
                _hint.FontSize = FontSize;
                Background = new VisualBrush(_hint) { Stretch = Stretch.None, AlignmentX = AlignmentX.Left, AlignmentY = AlignmentY.Center };
            }
            else if (_background is not null)
            {
                Background = _background;
            }
        }
        finally
        {
            _updating = false;
        }
    }
}

/// <summary>Event data of <see cref="SearchBox.QuerySubmitted"/>.</summary>
public sealed class SearchQueryEventArgs(string queryText) : EventArgs
{
    public string QueryText { get; } = queryText;
}

/// <summary>A search field: Enter submits the query (also when it is empty, to clear a filter).</summary>
public sealed class SearchBox : HintTextBox
{
    public SearchBox()
    {
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                QuerySubmitted?.Invoke(this, new SearchQueryEventArgs(Text));
                e.Handled = true;
            }
        };
        ToolTip = "Enter";
    }

    public event EventHandler<SearchQueryEventArgs>? QuerySubmitted;
}

/// <summary>A numeric input (current culture). <see cref="Value"/> is NaN while the text is empty or not a number.</summary>
public sealed class NumberBox : HintTextBox
{
    public NumberBox()
    {
        HorizontalContentAlignment = HorizontalAlignment.Right;
        LostFocus += (_, _) =>
        {
            if (!double.IsNaN(Value))
            {
                Value = Math.Clamp(Value, Minimum, Maximum);
            }
        };
    }

    public double Minimum { get; set; } = double.MinValue;

    public double Maximum { get; set; } = double.MaxValue;

    /// <summary>Kept for API compatibility (no spin buttons).</summary>
    public double SmallChange { get; set; } = 1;

    /// <summary>Kept for API compatibility (no spin buttons).</summary>
    public double LargeChange { get; set; } = 10;

    public double Value
    {
        get => double.TryParse(Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var value) ? value : double.NaN;
        set => Text = double.IsNaN(value) ? string.Empty : value.ToString(CultureInfo.CurrentCulture);
    }
}

/// <summary>A date field with a nullable <see cref="Date"/> (DateTimeOffset, like the Fluent CalendarDatePicker).</summary>
public sealed class CalendarDatePicker : DatePicker
{
    public DateTimeOffset? Date
    {
        get => SelectedDate is { } date ? new DateTimeOffset(date) : null;
        set => SelectedDate = value?.LocalDateTime.Date;
    }
}

/// <summary>An on/off switch (check box with <see cref="IsOn"/> and an optional header text).</summary>
public sealed class ToggleSwitch : CheckBox
{
    public bool IsOn
    {
        get => IsChecked == true;
        set => IsChecked = value;
    }

    public object? Header
    {
        get => Content;
        set => Content = value;
    }
}

/// <summary>A link-style button.</summary>
public sealed class HyperlinkButton : Button
{
    public HyperlinkButton()
    {
        Background = Brushes.Transparent;
        BorderThickness = new Thickness(0);
        Padding = new Thickness(0);
        HorizontalAlignment = HorizontalAlignment.Left;
        Cursor = Cursors.Hand;
        this.Brush(ForegroundProperty, Theme.Accent);
    }
}

/// <summary>A grid with gaps between rows and columns (applied as margins of the cells).</summary>
public sealed class SpacedGrid : Grid
{
    private readonly Dictionary<UIElement, Thickness> _applied = [];

    public double ColumnSpacing { get; set; }

    public double RowSpacing { get; set; }

    protected override Size MeasureOverride(Size constraint)
    {
        foreach (UIElement child in InternalChildren)
        {
            if (child is not FrameworkElement element)
            {
                continue;
            }

            var gap = new Thickness(GetColumn(element) > 0 ? ColumnSpacing : 0, GetRow(element) > 0 ? RowSpacing : 0, 0, 0);
            var current = element.Margin;
            var ours = _applied.TryGetValue(element, out var previous) && previous == current;
            if ((current == default || ours) && current != gap)
            {
                element.Margin = gap;
            }

            if (current == default || ours)
            {
                _applied[element] = gap;
            }
        }

        return base.MeasureOverride(constraint);
    }
}

/// <summary>A round person picture: the profile image when set, otherwise the initials on the accent colour.</summary>
public sealed class PersonPicture : Border
{
    private readonly TextBlock _initials = new()
    {
        FontWeight = FontWeights.SemiBold,
        Foreground = Brushes.White,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
    };

    private ImageSource? _picture;

    public PersonPicture()
    {
        Background = Theme.BrandBrush;
        Child = _initials;
        SizeChanged += (_, e) =>
        {
            CornerRadius = new CornerRadius(Math.Min(e.NewSize.Width, e.NewSize.Height) / 2);
            _initials.FontSize = Math.Max(10, e.NewSize.Height * 0.38);
        };
    }

    public string? Initials
    {
        get => _initials.Text;
        set => _initials.Text = value ?? string.Empty;
    }

    public string? DisplayName
    {
        get => ToolTip as string;
        set => ToolTip = value;
    }

    public ImageSource? ProfilePicture
    {
        get => _picture;
        set
        {
            _picture = value;
            Background = value is null ? Theme.BrandBrush : new ImageBrush(value) { Stretch = Stretch.UniformToFill };
            _initials.Visibility = value is null ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    /// <summary>Decodes an image (PNG/JPEG bytes) and shows it; unreadable data keeps the initials.</summary>
    public bool SetPicture(byte[] bytes, int decodePixels = 112)
    {
        try
        {
            var image = new System.Windows.Media.Imaging.BitmapImage();
            image.BeginInit();
            image.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = decodePixels;
            image.StreamSource = new MemoryStream(bytes);
            image.EndInit();
            image.Freeze();
            ProfilePicture = image;
            return true;
        }
        catch (Exception ex) when (ex is NotSupportedException or IOException or ArgumentException or InvalidOperationException)
        {
            return false;
        }
    }
}
