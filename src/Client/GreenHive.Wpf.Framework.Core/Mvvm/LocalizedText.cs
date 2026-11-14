using System.ComponentModel;

namespace GreenHive.Wpf.Framework.Mvvm;

/// <summary>
/// A bindable, live-updating localized text. Use it in ViewModels instead of plain strings;
/// <see cref="Value"/> changes when the language or a parameter changes.
/// </summary>
public sealed class LocalizedText : INotifyPropertyChanged
{
    private readonly LanguageManager _languages;
    private IReadOnlyDictionary<string, object?>? _parameters;
    private string _value;

    internal LocalizedText(LanguageManager languages, string key, IReadOnlyDictionary<string, object?>? parameters)
    {
        _languages = languages;
        Key = key;
        _parameters = parameters;
        _value = languages.GetText(key, parameters);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Key { get; }

    public string Value => _value;

    public void SetParameters(IReadOnlyDictionary<string, object?>? parameters)
    {
        _parameters = parameters;
        Refresh();
    }

    public override string ToString() => _value;

    internal void Refresh()
    {
        var value = _languages.GetText(Key, _parameters);
        if (value != _value)
        {
            _value = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
        }
    }
}
