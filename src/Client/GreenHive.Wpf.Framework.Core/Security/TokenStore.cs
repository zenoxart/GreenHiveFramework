namespace GreenHive.Wpf.Framework.Security;

/// <summary>A remembered session (access token + expiry) for one server.</summary>
public sealed record StoredSession(string ServerUrl, string UserName, string AccessToken, DateTimeOffset ExpiresAt);

/// <summary>
/// Keeps the access token of "remember me" sessions. The WPF host uses <c>ProtectedTokenStore</c>
/// (encrypted per Windows user with DPAPI); tests and other hosts can supply their own store.
/// </summary>
public interface ITokenStore
{
    StoredSession? Load(string serverUrl);

    void Save(StoredSession session);

    void Clear();
}

/// <summary>Implemented by application contexts that provide a token store.</summary>
public interface ITokenStoreContext
{
    ITokenStore Tokens { get; }
}

/// <summary>Fallback that remembers nothing (used when the host provides no store).</summary>
public sealed class NoTokenStore : ITokenStore
{
    public StoredSession? Load(string serverUrl) => null;

    public void Save(StoredSession session)
    {
    }

    public void Clear()
    {
    }
}
