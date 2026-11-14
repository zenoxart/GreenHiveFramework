using System.Security.Cryptography;
using GreenHive.NET.Backend.Core.Data;
using GreenHive.NET.Contracts;
using GreenHive.NET.ModuleContracts.Api;
using GreenHive.NET.Monads;

namespace GreenHive.NET.Backend.ModuleDeliveryService;

/// <summary>File storage of the packages (volume) plus their metadata (delivery.packages).</summary>
public sealed class PackageStorage(IConfiguration configuration)
{
    public string Root { get; } = Directory.CreateDirectory(configuration["GreenHive:PackagesPath"] ?? "/data/packages").FullName;

    public string? FindFile(string moduleId, string version)
    {
        var folder = Folder(moduleId, version);
        return folder is null || !Directory.Exists(folder) ? null : Directory.GetFiles(folder, "*.zip").FirstOrDefault();
    }

    public string? Folder(string moduleId, string version) =>
        ModuleId.IsValid(moduleId) && SemanticVersion.TryParse(version, out _) && !version.Contains("..") ? Path.Combine(Root, moduleId, version) : null;

    public IEnumerable<(string ModuleId, string Version, string File)> Scan() =>
        Directory.EnumerateDirectories(Root)
            .Where(d => ModuleId.IsValid(Path.GetFileName(d)))
            .SelectMany(m => Directory.EnumerateDirectories(m).Select(v => (Module: Path.GetFileName(m), Version: Path.GetFileName(v), Dir: v)))
            .SelectMany(x => Directory.EnumerateFiles(x.Dir, "*.zip").Take(1).Select(f => (x.Module, x.Version, f)));

    public static async Task<(string Hex, byte[] Hash, long Size)> HashAsync(string file, CancellationToken ct)
    {
        await using var stream = File.OpenRead(file);
        var hash = await SHA256.HashDataAsync(stream, ct);
        return (Convert.ToHexStringLower(hash), hash, stream.Length);
    }
}

/// <summary>Metadata of packages (adapter).</summary>
public sealed class PackageCatalog(Db db, PackageStorage storage, PackageSigning signing)
{
    public Task<Result<IReadOnlyList<PackageInfo>>> ListAsync(CancellationToken ct) =>
        db.QueryAsync("select * from delivery.packages order by module_id, uploaded_at desc", Map, null, ct);

    public Task<Result<PackageInfo>> FindAsync(string moduleId, string version, CancellationToken ct) =>
        db.FindAsync("select * from delivery.packages where module_id = @moduleId and version = @version", Map, new { moduleId, version },
            Error.NotFound("packages.notFound", "modules:package.notFound").WithMetadata("module", $"{moduleId}@{version}"), ct);

    /// <summary>Hashes, signs and records a package file.</summary>
    public async Task<Result<PackageInfo>> RegisterAsync(string moduleId, string version, string file, CancellationToken ct)
    {
        var (hex, hash, size) = await PackageStorage.HashAsync(file, ct);
        var info = new PackageInfo(moduleId, version, Path.GetFileName(file), hex, size)
        {
            Signature = signing.Sign(hash),
            Url = $"/api/packages/{moduleId}/{version}",
        };
        var saved = await db.ExecuteAsync(
            """
            insert into delivery.packages (module_id, version, file_name, sha256, size_bytes, signature, uploaded_at)
            values (@moduleId, @version, @fileName, @sha256, @size, @signature, now())
            on conflict (module_id, version) do update set file_name = excluded.file_name, sha256 = excluded.sha256,
                size_bytes = excluded.size_bytes, signature = excluded.signature, uploaded_at = now()
            """, new { moduleId, version, fileName = info.FileName, sha256 = hex, size, signature = info.Signature }, ct);
        return saved.Map(_ => info);
    }

    /// <summary>Registers files dropped into the volume that are unknown or changed.</summary>
    public async Task<int> SyncAsync(CancellationToken ct)
    {
        var known = (await ListAsync(ct)).GetValueOrDefault([]).ToDictionary(p => $"{p.ModuleId}@{p.Version}", p => p.Sha256);
        var count = 0;
        foreach (var (moduleId, version, file) in storage.Scan())
        {
            var (hex, _, _) = await PackageStorage.HashAsync(file, ct);
            if (!known.TryGetValue($"{moduleId}@{version}", out var sha) || sha != hex)
            {
                if ((await RegisterAsync(moduleId, version, file, ct)).IsSuccess)
                {
                    count++;
                }
            }
        }

        return count;
    }

    private static PackageInfo Map(System.Data.Common.DbDataReader r) =>
        new(r.Get<string>("module_id"), r.Get<string>("version"), r.Get<string>("file_name"), r.Get<string>("sha256"), r.Get<long>("size_bytes"))
        {
            Signature = r.Get<string>("signature"),
            Url = $"/api/packages/{r.Get<string>("module_id")}/{r.Get<string>("version")}",
        };
}

/// <summary>Registers packages found in the volume at start and every minute (e.g. copied by the build script).</summary>
public sealed class PackageScanner(PackageCatalog catalog, ILogger<PackageScanner> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        do
        {
            try
            {
                var count = await catalog.SyncAsync(stoppingToken);
                if (count > 0)
                {
                    logger.LogInformation("Registered {Count} package(s) from the volume", count);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning(ex, "Package scan failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
