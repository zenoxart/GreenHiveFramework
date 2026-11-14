using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using GreenHive.NET.Contracts.Api;
using GreenHive.NET.Monads;
using GreenHive.Wpf.Framework.Mvvm;
using GreenHive.Wpf.Framework.Security;
using GreenHive.NET.WorkflowContracts;
using System.Windows;
using System.Windows.Controls;
using GreenHive.Wpf.Framework.Theming;

namespace GreenHive.Wpf.Framework.ModuleLoader.Sdk;

/// <summary>
/// A form generated from an InputSchema (workflow input steps, report parameters, module dialogs).
/// Validation uses the shared InputValidator - the same rules as the workflow engine and the AI guardrails.
/// Half-width fields are placed side by side in the wider size classes.
/// </summary>
public sealed partial class InputForm : UserControl
{
    private readonly InputSchema _schema;
    private readonly LanguageManager _languages;
    private readonly Dictionary<string, (FrameworkElement Control, TextBlock Error)> _fields = new(StringComparer.OrdinalIgnoreCase);

    public InputForm(InputSchema schema, LanguageManager languages, Func<string, Task<Result<IReadOnlyList<LookupItem>>>>? lookup = null, JsonObject? values = null)
    {
        _schema = schema;
        _languages = languages;
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var row = 0;
        var column = 0;
        foreach (var field in schema.Fields)
        {
            var control = CreateControl(field, lookup);
            var error = new TextBlock { Foreground = Theme.Error, FontSize = 12, Visibility = Visibility.Collapsed, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) };
            var label = Text(field.Label is { Length: > 0 } l ? l : field.Name) + (field.Required ? " *" : string.Empty);
            var cell = UiKit.Field(label, control);
            cell.Children.Add(error);
            if (field.Help is { } help)
            {
                control.ToolTip = Text(help);
            }

            var half = field.Span == FieldSpan.Half;
            if (!half && column == 1)
            {
                row++;
                column = 0;
            }

            while (grid.RowDefinitions.Count <= row)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }

            cell.Margin = new Thickness(column == 1 ? 6 : 0, row == 0 ? 0 : 12, half && column == 0 ? 6 : 0, 0);
            Grid.SetRow(cell, row);
            Grid.SetColumn(cell, column);
            Grid.SetColumnSpan(cell, half ? 1 : 2);
            grid.Children.Add(cell);
            if (half && column == 0)
            {
                column = 1;
            }
            else
            {
                row++;
                column = 0;
            }

            _fields[field.Name] = (control, error);
        }

        Content = grid;
        if (values is not null)
        {
            SetValues(values);
        }
    }

    public bool IsReadOnly
    {
        set
        {
            foreach (var (control, _) in _fields.Values)
            {
                if (control is Control c)
                {
                    c.IsEnabled = !value;
                }
            }
        }
    }

    /// <summary>The current values (raw; not yet validated).</summary>
    public JsonObject Values
    {
        get
        {
            var values = new JsonObject();
            foreach (var input in _schema.Fields)
            {
                values[input.Name] = Read(input, _fields[input.Name].Control);
            }

            return values;
        }
    }

    public void SetValues(JsonObject values)
    {
        foreach (var field in _schema.Fields)
        {
            var value = values.FirstOrDefault(kv => kv.Key.Equals(field.Name, StringComparison.OrdinalIgnoreCase)).Value;
            if (value is not null)
            {
                Write(_fields[field.Name].Control, value);
            }
        }
    }

    /// <summary>Validates with the shared rules and shows the errors next to the fields.</summary>
    public Validation<JsonObject> Validate()
    {
        var result = InputValidator.Validate(_schema, Values);
        ShowErrors(result.IsValid ? null : Error.Many(result.Errors));
        return result;
    }

    /// <summary>Shows field errors of a (server) error next to the fields; null clears them.</summary>
    public void ShowErrors(Error? error)
    {
        var messages = error is null ? new Dictionary<string, string>() : ErrorMessages.Fields(_languages, error);
        foreach (var (name, (_, text)) in _fields)
        {
            var has = messages.TryGetValue(name, out var message);
            text.Text = message ?? string.Empty;
            text.Visibility = has ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private string Text(string keyOrText) => ErrorMessages.Text(_languages, keyOrText);

    private FrameworkElement CreateControl(InputField field, Func<string, Task<Result<IReadOnlyList<LookupItem>>>>? lookup)
    {
        switch (field.Type)
        {
            case InputFieldType.Number:
                return new NumberField(field.Min, field.Max)
                {
                    Text = decimal.TryParse(field.Default, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number.ToString(CultureInfo.CurrentCulture) : string.Empty,
                };
            case InputFieldType.Boolean:
                return new CheckBox { IsChecked = field.Default is "true" };
            case InputFieldType.Date:
                return new DatePicker
                {
                    SelectedDate = DateTime.TryParse(field.Default, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var date) ? date : null,
                };
            case InputFieldType.Choice:
            {
                var combo = new ComboBox();
                foreach (var option in field.Options)
                {
                    combo.Items.Add(new ComboBoxItem { Content = Text(option.Label is { Length: > 0 } l ? l : option.Value), Tag = option.Value });
                }

                SelectByTag(combo, field.Default);
                return combo;
            }

            case InputFieldType.EntityRef:
            {
                var combo = new ComboBox();
                if (lookup is not null && field.Lookup is { } url)
                {
                    _ = LoadLookupAsync(combo, url, lookup, field.Default);
                }

                return combo;
            }

            case InputFieldType.MultiLine:
                return new TextBox { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 80, MaxLength = field.MaxLength ?? 0, Text = field.Default ?? string.Empty };
            default:
                return new TextBox { MaxLength = field.MaxLength ?? 0, Text = field.Default ?? string.Empty };
        }
    }

    private static async Task LoadLookupAsync(ComboBox combo, string url, Func<string, Task<Result<IReadOnlyList<LookupItem>>>> lookup, string? selected)
    {
        var items = await lookup(url);
        var current = (combo.SelectedItem as ComboBoxItem)?.Tag as string ?? combo.Tag as string ?? selected;
        combo.Items.Clear();
        foreach (var item in items.GetValueOrDefault([]))
        {
            combo.Items.Add(new ComboBoxItem { Content = item.Label, Tag = item.Value });
        }

        SelectByTag(combo, current);
    }

    private static void SelectByTag(ComboBox combo, string? value)
    {
        combo.Tag = value;
        combo.SelectedItem = combo.Items.OfType<ComboBoxItem>().FirstOrDefault(i => string.Equals(i.Tag as string, value, StringComparison.OrdinalIgnoreCase));
    }

    private static JsonNode? Read(InputField field, FrameworkElement control) => control switch
    {
        NumberField n => n.Value is { } number ? JsonValue.Create(Math.Round(number, 4)) : string.IsNullOrWhiteSpace(n.Text) ? null : JsonValue.Create(n.Text),
        CheckBox c => JsonValue.Create(c.IsChecked == true),
        DatePicker d => d.SelectedDate is { } date ? JsonValue.Create(date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)) : null,
        ComboBox c => (c.SelectedItem as ComboBoxItem)?.Tag is string tag ? JsonValue.Create(tag) : c.Tag is string pending ? JsonValue.Create(pending) : null,
        TextBox t => string.IsNullOrWhiteSpace(t.Text) ? null : JsonValue.Create(t.Text),
        _ => null,
    };

    private static void Write(FrameworkElement control, JsonNode value)
    {
        var text = value is JsonValue v && v.GetValueKind() == JsonValueKind.String ? v.GetValue<string>() : value.ToJsonString();
        switch (control)
        {
            case NumberField n:
                n.Text = decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number.ToString(CultureInfo.CurrentCulture) : text;
                break;
            case CheckBox c:
                c.IsChecked = text is "true" or "True";
                break;
            case DatePicker d:
                d.SelectedDate = DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var date) ? date : null;
                break;
            case ComboBox c:
                SelectByTag(c, text);
                break;
            case TextBox t:
                t.Text = text;
                break;
        }
    }
}

/// <summary>A text box for numbers (current culture); the value is checked by the shared InputValidator.</summary>
public sealed class NumberField : TextBox
{
    public NumberField(decimal? min, decimal? max)
    {
        Minimum = min;
        Maximum = max;
        HorizontalContentAlignment = HorizontalAlignment.Right;
    }

    public decimal? Minimum { get; }

    public decimal? Maximum { get; }

    /// <summary>The parsed value, or null when the text is empty or not a number.</summary>
    public decimal? Value => decimal.TryParse(Text, NumberStyles.Number, CultureInfo.CurrentCulture, out var value) ? value : null;
}
