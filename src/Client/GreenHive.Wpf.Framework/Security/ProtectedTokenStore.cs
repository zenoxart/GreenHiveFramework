using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GreenHive.Wpf.Framework.Mvvm;

namespace GreenHive.Wpf.Framework.Security;

/// <summary>
/// Keeps the access token of "remember me" sessions encrypted per Windows user (DPAPI, CurrentUser scope) in
/// LocalDataPath\session.bin - never in plain text.
/// </summary>
public sealed class ProtectedTokenStore : AppObject, ITokenStore
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("GreenHive.NET.Session.v1");

    private string FilePath => Path.Combine(AppContext.LocalDataPath, "session.bin");

    public StoredSession? Load(string serverUrl)
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return null;
            }

            var json = Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(FilePath), Entropy, DataProtectionScope.CurrentUser));
            var session = JsonSerializer.Deserialize<StoredSession>(json);
            return session is not null && session.ServerUrl.Equals(serverUrl, StringComparison.OrdinalIgnoreCase) ? session : null;
        }
        catch (Exception ex) when (ex is CryptographicException or IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public void Save(StoredSession session)
    {
        try
        {
            var bytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(session)), Entropy, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(FilePath, bytes);
        }
        catch (Exception ex) when (ex is CryptographicException or IOException or UnauthorizedAccessException)
        {
            AppContext.Log.Write($"The session could not be stored: {ex.Message}", LogEntryType.Warning, nameof(ProtectedTokenStore));
        }
    }

    public void Clear()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                File.WriteAllBytes(FilePath, []);
                File.Delete(FilePath);
            }
        }
        catch (IOException)
        {
        }
    }
}
