using GreenHive.Wpf.Framework.Mvvm;

namespace GreenHive.Wpf.UI.Platform;

/// <summary>Texts of the Green Hive additions to the shell (scope "greenhive"): classic views, shortcuts, tutorial, diagnostics.</summary>
internal static class GreenHiveTexts
{
    public static void Register(LanguageManager languages)
    {
        languages.Register("greenhive", "en", English);
        languages.Register("greenhive", "de", German);
    }

    private static readonly Dictionary<string, string> English = new()
    {
        ["group.classic.label"] = "Classic views",
        ["view.dashboard.label"] = "Dashboard",
        ["view.dashboard.tooltip"] = "Classic Green Hive dashboard",
        ["view.settings.label"] = "Preferences",
        ["view.profile.label"] = "Classic profile",
        ["view.admin.label"] = "Administration",
        ["view.news.label"] = "What's new",
        ["view.glossary.label"] = "Glossary",
        ["view.about.label"] = "About Green Hive",
        ["command.shortcuts.label"] = "Keyboard shortcuts",
        ["command.shortcuts.tooltip"] = "Change, record and reset keyboard shortcuts",
        ["command.cheatsheet.label"] = "Shortcut cheat sheet",
        ["command.cheatsheet.tooltip"] = "Shows the shortcuts available right now",
        ["command.tutorial.label"] = "Start tutorial",
        ["command.heatmap.label"] = "Interaction heatmap",
        ["command.performance.label"] = "Performance overlay",
        ["cheatsheet.title"] = "Keyboard shortcuts available now",
        ["cheatsheet.hint"] = "Press Esc or Ctrl+/ to close",
        ["palette.title"] = "Command palette",
        ["palette.empty"] = "No matching command",
        ["profile.signout"] = "Sign out",
        ["profile.notSignedIn"] = "Not signed in",
    };

    private static readonly Dictionary<string, string> German = new()
    {
        ["group.classic.label"] = "Klassische Ansichten",
        ["view.dashboard.label"] = "Dashboard",
        ["view.dashboard.tooltip"] = "Klassisches Green Hive-Dashboard",
        ["view.settings.label"] = "Einstellungen (klassisch)",
        ["view.profile.label"] = "Profil (klassisch)",
        ["view.admin.label"] = "Administration",
        ["view.news.label"] = "Neuigkeiten",
        ["view.glossary.label"] = "Glossar",
        ["view.about.label"] = "Über Green Hive",
        ["command.shortcuts.label"] = "Tastenkürzel",
        ["command.shortcuts.tooltip"] = "Tastenkürzel ändern, aufnehmen und zurücksetzen",
        ["command.cheatsheet.label"] = "Tastenkürzel-Übersicht",
        ["command.cheatsheet.tooltip"] = "Zeigt die gerade verfügbaren Tastenkürzel",
        ["command.tutorial.label"] = "Tutorial starten",
        ["command.heatmap.label"] = "Interaktions-Heatmap",
        ["command.performance.label"] = "Performance-Overlay",
        ["cheatsheet.title"] = "Jetzt verfügbare Tastenkürzel",
        ["cheatsheet.hint"] = "Esc oder Strg+/ schließt die Übersicht",
        ["palette.title"] = "Befehlspalette",
        ["palette.empty"] = "Kein passender Befehl",
        ["profile.signout"] = "Abmelden",
        ["profile.notSignedIn"] = "Nicht angemeldet",
    };
}
