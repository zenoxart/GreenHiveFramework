using System.Security.Cryptography;
using GreenHive.NET.ModuleContracts.Api;
using GreenHive.NET.Monads;
using GreenHive.Wpf.Framework.Mvvm;
using GreenHive.Wpf.Framework.Security;

namespace GreenHive.Wpf.Framework.ModuleLoader;

/// <summary>
/// Checks a downloaded package before anything is extracted or loaded: SHA-256 must match the registry information and the
/// ECDSA signature must verify against the delivery service's public key (pinned on first use in AppData).
/// </summary>
public sealed class ModulePackageValidator : AppObject
{
    private string KeyFile => Path.Combine(AppContext.LocalDataPath, "Modules", "signing-key.pem");

    public async Task<Result<Unit>> ValidateAsync(string file, PackageInfo info, CancellationToken ct)
    {
        byte[] hash;
        await using (var stream = File.OpenRead(file))
        {
            hash = await SHA256.HashDataAsync(stream, ct);
        }

        if (!Convert.ToHexStringLower(hash).Equals(info.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            return Error.Validation("modules.package.hash", "modules:package.hashMismatch").WithMetadata("module", info.ModuleId);
        }

        if (string.IsNullOrEmpty(info.Signature))
        {
            return Error.Validation("modules.package.unsigned", "modules:package.unsigned");
        }

        var key = await PublicKeyAsync(ct);
        if (!key.TryGetValue(out var pem, out var keyError))
        {
            return keyError;
        }

        using var ecdsa = ECDsa.Create();
        ecdsa.ImportFromPem(pem);
        return ecdsa.VerifyHash(hash, Convert.FromBase64String(info.Signature))
            ? Result.Success()
            : Error.Validation("modules.package.signature", "modules:package.signatureInvalid").WithMetadata("module", info.ModuleId);
    }

    private async Task<Result<string>> PublicKeyAsync(CancellationToken ct)
    {
        if (File.Exists(KeyFile))
        {
            return await File.ReadAllTextAsync(KeyFile, ct);
        }

        var backend = Context<ISecurityContext>().Backend;
        using var request = backend.CreateRequest(HttpMethod.Get, "/api/packages/signing-key");
        try
        {
            using var response = await backend.Http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                return Error.Unavailable("modules.package.key", "errors:unavailable");
            }

            var pem = await response.Content.ReadAsStringAsync(ct);
            Directory.CreateDirectory(Path.GetDirectoryName(KeyFile)!);
            await File.WriteAllTextAsync(KeyFile, pem, ct);
            AppContext.Log.Write("Module signing key pinned (trust on first use)", LogEntryType.Information, nameof(ModulePackageValidator));
            return pem;
        }
        catch (HttpRequestException ex)
        {
            return Try.Map(ex, "modules.package.key");
        }
    }
}
