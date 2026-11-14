using System.Security.Cryptography;

namespace GreenHive.NET.Backend.ModuleDeliveryService;

/// <summary>
/// ECDSA P-256 signatures over the SHA-256 of a package. The key pair is created on first start and kept in the
/// package volume (/data/packages/.signing/key.pem); clients pin the public key on first use.
/// </summary>
public sealed class PackageSigning : IDisposable
{
    private readonly ECDsa _key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

    public PackageSigning(PackageStorage storage)
    {
        var folder = Path.Combine(storage.Root, ".signing");
        var file = Path.Combine(folder, "key.pem");
        Directory.CreateDirectory(folder);
        if (File.Exists(file))
        {
            _key.ImportFromPem(File.ReadAllText(file));
        }
        else
        {
            File.WriteAllText(file, _key.ExportECPrivateKeyPem());
        }
    }

    public string PublicKeyPem => _key.ExportSubjectPublicKeyInfoPem();

    public string Sign(byte[] sha256) => Convert.ToBase64String(_key.SignHash(sha256));

    public bool Verify(byte[] sha256, string signature) => _key.VerifyHash(sha256, Convert.FromBase64String(signature));

    public void Dispose() => _key.Dispose();
}
