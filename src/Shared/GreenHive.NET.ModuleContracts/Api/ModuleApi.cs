using GreenHive.NET.WorkflowContracts;

namespace GreenHive.NET.ModuleContracts.Api;

// ---------------- Module Registry (/api/modules) ----------------

/// <summary>One module of the catalogue: the version assigned to the caller and whether it fits the caller's host.</summary>
public sealed record ModuleCatalogEntry(ModuleManifest Manifest, string Channel, DateTimeOffset PublishedAt)
{
    public bool Compatible { get; init; } = true;

    public IReadOnlyList<string> Problems { get; init; } = [];

    /// <summary>Relative URL of the client package (null when no package was uploaded).</summary>
    public string? PackageUrl { get; init; }

    /// <summary>Rollout stage that assigned this version (null = general availability).</summary>
    public string? RolloutStage { get; init; }
}

public sealed record ModuleVersionInfo(string ModuleId, string Version, string Channel, string Status, DateTimeOffset PublishedAt)
{
    public string? PublishedBy { get; init; }
}

public sealed record PublishModuleRequest(ModuleManifest Manifest, string Channel = "stable");

public sealed record PublishModuleResponse(string ModuleId, string Version)
{
    public string? PreviousVersion { get; init; }

    public Guid? ImpactReportId { get; init; }

    public int BrokenWorkflows { get; init; }

    public Guid? RolloutId { get; init; }
}

/// <summary>Gateway route of a module backend (from manifest.backend).</summary>
public sealed record RouteInfo(string Prefix, string Service);

// ---------------- Module Delivery (/api/packages) ----------------

public sealed record PackageInfo(string ModuleId, string Version, string FileName, string Sha256, long SizeBytes)
{
    /// <summary>ECDSA P-256 signature (base64) over the SHA-256 of the package.</summary>
    public string? Signature { get; init; }

    public string Url { get; init; } = string.Empty;
}

// ---------------- Workflow impact (internal) ----------------

public sealed record ImpactAnalysisRequest(string ModuleId, string FromVersion, string ToVersion, ModuleContractSurface Old, ModuleContractSurface New);

public sealed record ImpactAnalysisResponse(Guid ReportId, int AffectedWorkflows, int BrokenWorkflows, int ReviewWorkflows, int MigratedWorkflows)
{
    public IReadOnlyList<SurfaceChange> SurfaceDiff { get; init; } = [];
}
