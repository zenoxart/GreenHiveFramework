using System.Text.Json;
using GreenHive.NET.Backend.Core.Data;
using GreenHive.NET.Contracts;
using GreenHive.NET.ModuleContracts;
using GreenHive.NET.ModuleContracts.Api;
using GreenHive.NET.Monads;

namespace GreenHive.NET.Backend.ModuleRegistryService;

public sealed record RegisteredModule(ModuleManifest Manifest, string Channel, string Status, DateTimeOffset PublishedAt, string? PublishedBy);

/// <summary>Storage of module versions (adapter).</summary>
public sealed class ModuleRegistry(Db db)
{
    private const string Columns = "manifest::text as manifest, channel, status, published_at, published_by";

    /// <summary>All published versions, newest first per module.</summary>
    public Task<Result<IReadOnlyList<RegisteredModule>>> PublishedAsync(CancellationToken ct) =>
        db.QueryAsync($"select {Columns} from registry.modules where status = 'published' order by module_id, major desc, minor desc, patch desc", Map, null, ct);

    public Task<Result<IReadOnlyList<RegisteredModule>>> LatestAsync(CancellationToken ct) =>
        PublishedAsync(ct).Map(list => (IReadOnlyList<RegisteredModule>)list.GroupBy(m => m.Manifest.ModuleId).Select(g => g.First()).ToList());

    public Task<Result<IReadOnlyList<RegisteredModule>>> VersionsAsync(string moduleId, CancellationToken ct) =>
        db.QueryAsync($"select {Columns} from registry.modules where module_id = @moduleId order by major desc, minor desc, patch desc", Map, new { moduleId }, ct);

    public Task<Result<Option<RegisteredModule>>> FindAsync(string moduleId, string version, CancellationToken ct) =>
        db.QuerySingleAsync($"select {Columns} from registry.modules where module_id = @moduleId and version = @version", Map, new { moduleId, version }, ct);

    public Task<Result<Unit>> InsertAsync(ModuleManifest manifest, string channel, string publishedBy, CancellationToken ct)
    {
        var v = manifest.SemanticVersion;
        return db.ExecuteAsync(
            """
            insert into registry.modules (module_id, version, major, minor, patch, channel, status, manifest, published_at, published_by)
            values (@moduleId, @version, @major, @minor, @patch, @channel, 'published', @manifest::jsonb, now(), @publishedBy)
            """,
            new
            {
                moduleId = manifest.ModuleId,
                version = manifest.Version,
                major = v.Major,
                minor = v.Minor,
                patch = v.Patch,
                channel,
                manifest = JsonSerializer.Serialize(manifest, GreenHiveJson.Options),
                publishedBy,
            }, ct).ToUnit();
    }

    public Task<Result<int>> SetStatusAsync(string moduleId, string version, string status, CancellationToken ct) =>
        db.ExecuteAsync("update registry.modules set status = @status where module_id = @moduleId and version = @version", new { moduleId, version, status }, ct);

    private static RegisteredModule Map(System.Data.Common.DbDataReader r) => new(
        JsonSerializer.Deserialize<ModuleManifest>(r.Get<string>("manifest"), GreenHiveJson.Options)!,
        r.Get<string>("channel"), r.Get<string>("status"), r.GetTime("published_at"), r.GetOrNull<string>("published_by"));
}
