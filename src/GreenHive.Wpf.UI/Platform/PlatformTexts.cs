using GreenHive.Wpf.Framework.Mvvm;

namespace GreenHive.Wpf.UI.Platform;

/// <summary>
/// Texts of the platform scopes the backend services answer with (errors, validation, auth, identity, modules,
/// notifications, updates). Error messages arrive as TextKeys, optionally with "|" arguments ({0}, {1}).
/// </summary>
internal static class PlatformTexts
{
    public static void Register(LanguageManager languages)
    {
        foreach (var (scope, en, de) in Scopes)
        {
            languages.Register(scope, "en", en);
            languages.Register(scope, "de", de);
        }
    }

    private static readonly (string Scope, Dictionary<string, string> En, Dictionary<string, string> De)[] Scopes =
    [
        ("errors", new()
        {
            ["cancelled"] = "The operation was cancelled.",
            ["concurrency"] = "The item was changed by someone else. Reload and try again.",
            ["duplicate"] = "An item with these values already exists.",
            ["invalidResponse"] = "The server sent an answer that could not be read.",
            ["notFound"] = "The item was not found.",
            ["reference"] = "The item is still referenced and cannot be changed this way.",
            ["timeout"] = "The server did not answer in time.",
            ["unavailable"] = "The service is not available at the moment.",
            ["unexpected"] = "An unexpected error occurred.",
            ["unreachable"] = "The server cannot be reached. Check the connection.",
        }, new()
        {
            ["cancelled"] = "Der Vorgang wurde abgebrochen.",
            ["concurrency"] = "Der Eintrag wurde von jemand anderem geändert. Neu laden und erneut versuchen.",
            ["duplicate"] = "Ein Eintrag mit diesen Werten existiert bereits.",
            ["invalidResponse"] = "Die Antwort des Servers konnte nicht gelesen werden.",
            ["notFound"] = "Der Eintrag wurde nicht gefunden.",
            ["reference"] = "Der Eintrag wird noch verwendet und kann so nicht geändert werden.",
            ["timeout"] = "Der Server hat nicht rechtzeitig geantwortet.",
            ["unavailable"] = "Der Dienst ist im Moment nicht verfügbar.",
            ["unexpected"] = "Ein unerwarteter Fehler ist aufgetreten.",
            ["unreachable"] = "Der Server ist nicht erreichbar. Prüfen Sie die Verbindung.",
        }),
        ("validation", new()
        {
            ["required"] = "This field is required.",
            ["maxLength"] = "At most {0} characters.",
            ["range"] = "Enter a value between {0} and {1}.",
            ["oneOf"] = "Choose one of: {0}.",
            ["pattern"] = "The format is not valid.",
            ["email"] = "Enter a valid e-mail address.",
            ["number"] = "Enter a number.",
            ["date"] = "Enter a valid date.",
            ["requestMissing"] = "The request is empty.",
        }, new()
        {
            ["required"] = "Dieses Feld ist erforderlich.",
            ["maxLength"] = "Höchstens {0} Zeichen.",
            ["range"] = "Geben Sie einen Wert zwischen {0} und {1} ein.",
            ["oneOf"] = "Wählen Sie eines von: {0}.",
            ["pattern"] = "Das Format ist ungültig.",
            ["email"] = "Geben Sie eine gültige E-Mail-Adresse ein.",
            ["number"] = "Geben Sie eine Zahl ein.",
            ["date"] = "Geben Sie ein gültiges Datum ein.",
            ["requestMissing"] = "Die Anfrage ist leer.",
        }),
        ("auth", new()
        {
            ["required"] = "Please sign in.",
            ["sessionExpired"] = "Your session has expired. Please sign in again.",
            ["token.expired"] = "Your session has expired. Please sign in again.",
            ["token.invalid"] = "Your sign-in is not valid. Please sign in again.",
            ["permissionMissing"] = "You do not have the permission for this.",
            ["internalOnly"] = "This endpoint is only available to services.",
        }, new()
        {
            ["required"] = "Bitte melden Sie sich an.",
            ["sessionExpired"] = "Ihre Sitzung ist abgelaufen. Bitte melden Sie sich erneut an.",
            ["token.expired"] = "Ihre Sitzung ist abgelaufen. Bitte melden Sie sich erneut an.",
            ["token.invalid"] = "Ihre Anmeldung ist ungültig. Bitte melden Sie sich erneut an.",
            ["permissionMissing"] = "Ihnen fehlt die Berechtigung dafür.",
            ["internalOnly"] = "Dieser Endpunkt ist nur für Dienste verfügbar.",
        }),
        ("identity", new()
        {
            ["login.invalid"] = "User name or password is wrong.",
            ["login.inactive"] = "This account is deactivated.",
            ["avatar.none"] = "No profile picture.",
            ["avatar.invalid"] = "The picture is not a valid image.",
            ["avatar.tooLarge"] = "The picture is too large (at most 512 KB).",
            ["password.wrong"] = "The current password is wrong.",
            ["password.tooShort"] = "The new password needs at least {0} characters.",
            ["password.unchanged"] = "The new password must differ from the current one.",
        }, new()
        {
            ["login.invalid"] = "Benutzername oder Passwort ist falsch.",
            ["login.inactive"] = "Dieses Konto ist deaktiviert.",
            ["avatar.none"] = "Kein Profilbild.",
            ["avatar.invalid"] = "Das Bild ist ungültig.",
            ["avatar.tooLarge"] = "Das Bild ist zu groß (höchstens 512 KB).",
            ["password.wrong"] = "Das aktuelle Passwort ist falsch.",
            ["password.tooShort"] = "Das neue Passwort braucht mindestens {0} Zeichen.",
            ["password.unchanged"] = "Das neue Passwort muss sich vom aktuellen unterscheiden.",
        }),
        ("modules", new()
        {
            ["notLoaded"] = "The module is not loaded.",
            ["manifest.empty"] = "The module manifest is empty.",
            ["manifest.noClient"] = "The module has no client part.",
            ["package.notFound"] = "The module package was not found.",
            ["package.downloadFailed"] = "The module package could not be downloaded.",
            ["package.hashMismatch"] = "The module package is damaged (checksum mismatch).",
            ["package.signatureInvalid"] = "The signature of the module package is invalid.",
            ["package.unsigned"] = "The module package is not signed.",
            ["package.invalidVersion"] = "The module package has an invalid version.",
        }, new()
        {
            ["notLoaded"] = "Das Modul ist nicht geladen.",
            ["manifest.empty"] = "Das Modul-Manifest ist leer.",
            ["manifest.noClient"] = "Das Modul hat keinen Client-Teil.",
            ["package.notFound"] = "Das Modulpaket wurde nicht gefunden.",
            ["package.downloadFailed"] = "Das Modulpaket konnte nicht heruntergeladen werden.",
            ["package.hashMismatch"] = "Das Modulpaket ist beschädigt (Prüfsumme stimmt nicht).",
            ["package.signatureInvalid"] = "Die Signatur des Modulpakets ist ungültig.",
            ["package.unsigned"] = "Das Modulpaket ist nicht signiert.",
            ["package.invalidVersion"] = "Das Modulpaket hat eine ungültige Version.",
        }),
        ("notifications", new()
        {
            ["channel.invalid"] = "The push channel is not valid.",
        }, new()
        {
            ["channel.invalid"] = "Der Push-Kanal ist ungültig.",
        }),
    ];
}
