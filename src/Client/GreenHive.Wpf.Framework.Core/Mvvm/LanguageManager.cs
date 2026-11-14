using System.Globalization;

namespace GreenHive.Wpf.Framework.Mvvm;

/// <summary>A language the application offers.</summary>
public sealed record LanguageInfo(string Code, string NativeName);

/// <summary>
/// Manages the application languages and localized texts (WIFI: SprachenManager).
/// Texts are addressed by TextKey "scope:key" (scope = "shell" or a module ID).
/// Lookup: current language -> parent language -> default language -> "[scope:key]".
/// Changing the language updates every <see cref="LocalizedText"/> immediately.
/// </summary>
public sealed class LanguageManager : AppObject
{
    private readonly object _sync = new();
    private readonly Dictionary<string, Dictionary<string, Dictionary<string, string>>> _texts = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<WeakReference<LocalizedText>> _liveTexts = [];
    private readonly HashSet<string> _reportedMissing = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Raised after the current language changed.</summary>
    public event EventHandler? LanguageChanged;

    /// <summary>Languages offered to the user.</summary>
    public IReadOnlyList<LanguageInfo> Supported { get; private set; } = new LanguageInfo[] { new("en", "English"), new("de", "Deutsch") };

    /// <summary>Fallback language when a text is missing in the current language.</summary>
    public string DefaultLanguage { get; set; } = "en";

    /// <summary>Code of the current language.</summary>
    public string Current { get; private set; } = "en";

    /// <summary>The current language as <see cref="LanguageInfo"/>.</summary>
    public LanguageInfo CurrentInfo => Supported.FirstOrDefault(l => l.Code.Equals(Current, StringComparison.OrdinalIgnoreCase)) ?? Supported[0];

    /// <summary>Sets the offered languages.</summary>
    public void SetSupported(IEnumerable<LanguageInfo> languages)
    {
        var list = languages.ToList();
        if (list.Count > 0)
        {
            Supported = list;
        }
    }

    /// <summary>Sets the current language; unknown codes fall back to the first supported language.</summary>
    public void Set(string languageCode)
    {
        var code = Resolve(languageCode);
        Current = code;

        var culture = CultureInfo.GetCultureInfo(code);
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;

        RefreshLiveTexts();
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Registers texts of a scope for one language (replaces existing keys).</summary>
    public void Register(string scope, string language, IReadOnlyDictionary<string, string> texts)
    {
        lock (_sync)
        {
            if (!_texts.TryGetValue(scope, out var languages))
            {
                languages = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
                _texts[scope] = languages;
            }

            if (!languages.TryGetValue(language, out var keys))
            {
                keys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                languages[language] = keys;
            }

            foreach (var (key, value) in texts)
            {
                keys[key] = value;
            }
        }

        RefreshLiveTexts();
    }

    /// <summary>Removes all texts of a scope (e.g. when a module unloads).</summary>
    public void Unregister(string scope)
    {
        lock (_sync)
        {
            _texts.Remove(scope);
        }
    }

    /// <summary>Returns the text for a key, or "[scope:key]" if it is missing everywhere.</summary>
    public string GetText(string textKey, IReadOnlyDictionary<string, object?>? parameters = null)
    {
        if (TryGetText(textKey, out var text, parameters))
        {
            return text;
        }

        bool firstReport;
        lock (_sync)
        {
            firstReport = _reportedMissing.Add($"{Current}|{textKey}");
        }

        if (firstReport && HasAppContext)
        {
            AppContext.Log.Write($"Missing text '{textKey}' for language '{Current}'", LogEntryType.Warning, nameof(LanguageManager));
        }

        return $"[{textKey}]";
    }

    /// <summary>Tries to find a text without reporting it as missing.</summary>
    public bool TryGetText(string textKey, out string text, IReadOnlyDictionary<string, object?>? parameters = null)
    {
        var separator = textKey.IndexOf(':');
        var scope = separator > 0 ? textKey[..separator] : "shell";
        var key = separator > 0 ? textKey[(separator + 1)..] : textKey;

        lock (_sync)
        {
            if (_texts.TryGetValue(scope, out var languages))
            {
                foreach (var language in LookupChain())
                {
                    if (languages.TryGetValue(language, out var keys) && keys.TryGetValue(key, out var value))
                    {
                        text = Format(value, parameters);
                        return true;
                    }
                }
            }
        }

        text = string.Empty;
        return false;
    }

    /// <summary>Creates a live-updating text for binding.</summary>
    public LocalizedText Text(string textKey, IReadOnlyDictionary<string, object?>? parameters = null)
    {
        var text = new LocalizedText(this, textKey, parameters);
        lock (_sync)
        {
            _liveTexts.RemoveAll(w => !w.TryGetTarget(out _));
            _liveTexts.Add(new WeakReference<LocalizedText>(text));
        }

        return text;
    }

    private static string Format(string value, IReadOnlyDictionary<string, object?>? parameters)
    {
        if (parameters is null || parameters.Count == 0)
        {
            return value;
        }

        foreach (var (name, parameter) in parameters)
        {
            value = value.Replace("{" + name + "}", Convert.ToString(parameter, CultureInfo.CurrentCulture) ?? string.Empty, StringComparison.Ordinal);
        }

        return value;
    }

    private IEnumerable<string> LookupChain()
    {
        yield return Current;
        var dash = Current.IndexOf('-');
        if (dash > 0)
        {
            yield return Current[..dash];
        }

        yield return DefaultLanguage;
    }

    private string Resolve(string code)
    {
        var exact = Supported.FirstOrDefault(l => l.Code.Equals(code, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
        {
            return exact.Code;
        }

        var dash = code.IndexOf('-');
        var neutral = dash > 0 ? code[..dash] : code;
        return Supported.FirstOrDefault(l => l.Code.Equals(neutral, StringComparison.OrdinalIgnoreCase))?.Code ?? Supported[0].Code;
    }

    private void RefreshLiveTexts()
    {
        List<LocalizedText> alive = [];
        lock (_sync)
        {
            foreach (var reference in _liveTexts)
            {
                if (reference.TryGetTarget(out var text))
                {
                    alive.Add(text);
                }
            }
        }

        foreach (var text in alive)
        {
            text.Refresh();
        }
    }
}
