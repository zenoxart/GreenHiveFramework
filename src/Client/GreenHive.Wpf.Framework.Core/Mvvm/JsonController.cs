using System.Text.Json;

namespace GreenHive.Wpf.Framework.Mvvm;

/// <summary>
/// Typed service to save and load data as JSON (WIFI: XmlController). Writes to a temporary file
/// first and then replaces the target, so a crash never leaves a half-written file.
/// </summary>
public sealed class JsonController<T> : AppObject
    where T : class
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    /// <summary>Serializes the data into the file. Throws on failure.</summary>
    public void Save(T data, string toPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(toPath)!);
        var temporary = toPath + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(data, Options));
        File.Move(temporary, toPath, overwrite: true);
    }

    /// <summary>Deserializes the file. Throws if it is missing or invalid.</summary>
    public T Load(string fromPath) =>
        JsonSerializer.Deserialize<T>(File.ReadAllText(fromPath), Options)
        ?? throw new InvalidDataException($"'{fromPath}' does not contain {typeof(T).Name} data.");

    /// <summary>Loads the file or returns null; a missing file (new user) is not an error.</summary>
    public T? TryLoad(string fromPath)
    {
        if (!File.Exists(fromPath))
        {
            return null;
        }

        try
        {
            return Load(fromPath);
        }
        catch (Exception ex)
        {
            OnErrorOccurred(new IOException($"Could not read '{fromPath}': {ex.Message}", ex));
            return null;
        }
    }

    /// <summary>Saves the file and reports errors via ErrorOccurred instead of throwing.</summary>
    public bool TrySave(T data, string toPath)
    {
        try
        {
            Save(data, toPath);
            return true;
        }
        catch (Exception ex)
        {
            OnErrorOccurred(new IOException($"Could not write '{toPath}': {ex.Message}", ex));
            return false;
        }
    }
}
