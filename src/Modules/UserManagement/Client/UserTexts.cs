namespace GreenHive.NET.Modules.UserManagement.Client;

/// <summary>Texts of the module (scope "users").</summary>
internal static class UserTexts
{
    public static readonly Dictionary<string, string> English = new()
    {
        ["module.name"] = "Users", ["module.label"] = "Users",
        ["menu.label"] = "_Users",
        ["view.users"] = "Users", ["view.users.label"] = "Users", ["view.users.tooltip"] = "User directory, roles and approval limits",
        ["view.organization"] = "Organization", ["view.organization.label"] = "Organization", ["view.organization.tooltip"] = "Departments and roles",
        ["cmd.open.label"] = "Users", ["cmd.open.tooltip"] = "Open the user directory",
        ["cmd.organization.label"] = "Organization", ["cmd.organization.tooltip"] = "Departments and roles",
        ["cmd.create.label"] = "New user...", ["cmd.create.tooltip"] = "Create a user account",
        ["step.createUser"] = "Create user",
        ["refresh"] = "Refresh", ["search"] = "Search", ["all"] = "All departments", ["inactive"] = "Show inactive", ["new"] = "New",
        ["save"] = "Save", ["cancel"] = "Cancel", ["edit"] = "Edit...", ["roles"] = "Roles...", ["resetPassword"] = "Reset password",
        ["empty"] = "No users found.", ["select"] = "Select a user.", ["departments"] = "Departments", ["roleList"] = "Roles",
        ["field.userName"] = "User name", ["field.displayName"] = "Name", ["field.email"] = "E-mail", ["field.department"] = "Department",
        ["field.jobTitle"] = "Job title", ["field.phone"] = "Phone", ["field.location"] = "Location", ["field.manager"] = "Manager",
        ["field.hiredOn"] = "Hired on", ["field.roles"] = "Roles", ["field.approvalLimit"] = "Approval limit (EUR)", ["field.lastLogin"] = "Last sign-in",
        ["field.active"] = "Active", ["field.password"] = "Initial password (optional)", ["field.status"] = "Status",
        ["status.active"] = "Active", ["status.inactive"] = "Inactive",
        ["created"] = "User {name} created.", ["createdTemp"] = "User {name} created. Temporary password: {password}",
        ["saved"] = "User saved.", ["rolesSaved"] = "Roles updated.", ["passwordReset"] = "Temporary password for {name}: {password}",
        ["confirmReset"] = "Reset the password of {name}? The user must sign in with the temporary password.",
        ["users"] = "{count} users", ["error.required"] = "{field} is required.",
        ["field.role"] = "Role", ["notification.userCreated"] = "New user",
        ["user.adminRoleForbidden"] = "Only administrators may assign the administrator role.", ["user.changedByOthers"] = "The user was changed by someone else. Reload and try again.",
        ["user.deactivateSelf"] = "You cannot deactivate your own account.", ["user.exists"] = "A user with this user name or e-mail already exists.",
        ["user.notFound"] = "The user was not found.", ["user.unknownRole"] = "Unknown role.", ["user.userNamePattern"] = "3-40 characters: start with a lower-case letter, then lower-case letters, digits, dots or dashes.",
    };

    public static readonly Dictionary<string, string> German = new()
    {
        ["module.name"] = "Benutzer", ["module.label"] = "Benutzer",
        ["menu.label"] = "_Benutzer",
        ["view.users"] = "Benutzer", ["view.users.label"] = "Benutzer", ["view.users.tooltip"] = "Benutzerverzeichnis, Rollen und Freigabelimits",
        ["view.organization"] = "Organisation", ["view.organization.label"] = "Organisation", ["view.organization.tooltip"] = "Abteilungen und Rollen",
        ["cmd.open.label"] = "Benutzer", ["cmd.open.tooltip"] = "Benutzerverzeichnis öffnen",
        ["cmd.organization.label"] = "Organisation", ["cmd.organization.tooltip"] = "Abteilungen und Rollen",
        ["cmd.create.label"] = "Neuer Benutzer...", ["cmd.create.tooltip"] = "Ein Benutzerkonto anlegen",
        ["step.createUser"] = "Benutzer anlegen",
        ["refresh"] = "Aktualisieren", ["search"] = "Suchen", ["all"] = "Alle Abteilungen", ["inactive"] = "Inaktive anzeigen", ["new"] = "Neu",
        ["save"] = "Speichern", ["cancel"] = "Abbrechen", ["edit"] = "Bearbeiten...", ["roles"] = "Rollen...", ["resetPassword"] = "Passwort zurücksetzen",
        ["empty"] = "Keine Benutzer gefunden.", ["select"] = "Wählen Sie einen Benutzer.", ["departments"] = "Abteilungen", ["roleList"] = "Rollen",
        ["field.userName"] = "Benutzername", ["field.displayName"] = "Name", ["field.email"] = "E-Mail", ["field.department"] = "Abteilung",
        ["field.jobTitle"] = "Funktion", ["field.phone"] = "Telefon", ["field.location"] = "Standort", ["field.manager"] = "Vorgesetzte(r)",
        ["field.hiredOn"] = "Eintritt", ["field.roles"] = "Rollen", ["field.approvalLimit"] = "Freigabelimit (EUR)", ["field.lastLogin"] = "Letzte Anmeldung",
        ["field.active"] = "Aktiv", ["field.password"] = "Startpasswort (optional)", ["field.status"] = "Status",
        ["status.active"] = "Aktiv", ["status.inactive"] = "Inaktiv",
        ["created"] = "Benutzer {name} angelegt.", ["createdTemp"] = "Benutzer {name} angelegt. Temporäres Passwort: {password}",
        ["saved"] = "Benutzer gespeichert.", ["rolesSaved"] = "Rollen aktualisiert.", ["passwordReset"] = "Temporäres Passwort für {name}: {password}",
        ["confirmReset"] = "Passwort von {name} zurücksetzen? Die Anmeldung erfolgt dann mit dem temporären Passwort.",
        ["users"] = "{count} Benutzer", ["error.required"] = "{field} ist erforderlich.",
        ["field.role"] = "Rolle", ["notification.userCreated"] = "Neuer Benutzer",
        ["user.adminRoleForbidden"] = "Nur Administratoren dürfen die Administratorrolle vergeben.", ["user.changedByOthers"] = "Der Benutzer wurde von jemand anderem geändert. Neu laden und erneut versuchen.",
        ["user.deactivateSelf"] = "Sie können Ihr eigenes Konto nicht deaktivieren.", ["user.exists"] = "Ein Benutzer mit diesem Namen oder dieser E-Mail existiert bereits.",
        ["user.notFound"] = "Der Benutzer wurde nicht gefunden.", ["user.unknownRole"] = "Unbekannte Rolle.", ["user.userNamePattern"] = "3-40 Zeichen: mit einem Kleinbuchstaben beginnen, dann Kleinbuchstaben, Ziffern, Punkte oder Bindestriche.",
    };
}
