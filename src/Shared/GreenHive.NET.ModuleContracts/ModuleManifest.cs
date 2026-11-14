using System.Text.Json;
using GreenHive.NET.Contracts;
using GreenHive.NET.Monads;

namespace GreenHive.NET.ModuleContracts;

/// <summary>How a module's views are built.</summary>
public enum ModuleViewMode
{
    /// <summary>Views are built in code (no XAML metadata needed) - the default for dynamically loaded modules.</summary>
    CodeOnly,

    /// <summary>XAML as embedded resources loaded with XamlReader.Load ({Binding} only).</summary>
    LooseXaml,

    /// <summary>Compiled XAML with a metadata provider (requires a restart to update).</summary>
    CompiledXaml,
}

public enum ModuleHotSwap
{
    Supported,
    RestartRequired,
}

public sealed record ModuleClientInfo
{
    public string Platform { get; init; } = "wpf";

    public string TargetFramework { get; init; } = "net10.0-windows";

    /// <summary>Version range of the .NET Desktop Runtime runtime.</summary>
    public string DesktopRuntime { get; init; } = ">=10.0.0";

    /// <summary>Package file name (zip) delivered by the ModuleDeliveryService.</summary>
    public string? Package { get; init; }

    public string? Sha256 { get; init; }

    public string? Signature { get; init; }

    public required string EntryAssembly { get; init; }

    public required string EntryType { get; init; }

    public ModuleViewMode ViewMode { get; init; } = ModuleViewMode.CodeOnly;

    public ModuleHotSwap HotSwap { get; init; } = ModuleHotSwap.Supported;
}

public sealed record ModuleBackendInfo(string Service, string ApiVersion, string Route);

public sealed record ModuleDependency(string ModuleId, string VersionRange);

/// <summary>Data model of a module manifest (Modules/*/manifest.json).</summary>
public sealed record ModuleManifest
{
    public required string ModuleId { get; init; }

    public required string Version { get; init; }

    /// <summary>TextKey of the module name ("documents:module.name") or plain text.</summary>
    public string DisplayName { get; init; } = string.Empty;

    public string? Description { get; init; }

    public string Icon { get; init; } = "library";

    public string? AccentColor { get; init; }

    public string MinHostVersion { get; init; } = "1.0.0";

    /// <summary>Major version of the framework contracts the module was built against.</summary>
    public int ContractsVersion { get; init; } = 1;

    /// <summary>Prefix of the module's TextKeys, command IDs, step types and events ("documents").</summary>
    public string Prefix { get; init; } = string.Empty;

    public ModuleClientInfo? Client { get; init; }

    public ModuleBackendInfo? Backend { get; init; }

    public IReadOnlyList<string> Permissions { get; init; } = [];

    public IReadOnlyList<ModuleDependency> Dependencies { get; init; } = [];

    public IReadOnlyList<string> Languages { get; init; } = ["en"];

    public string DefaultLanguage { get; init; } = "en";

    public ModuleContractSurface ContractSurface { get; init; } = new();

    public SemanticVersion SemanticVersion => SemanticVersion.Parse(Version);

    /// <summary>Reads a manifest (JSON with comments allowed) and checks the mandatory fields.</summary>
    public static Result<ModuleManifest> Parse(string json)
    {
        var parsed = Try.Run(() => JsonSerializer.Deserialize<ModuleManifest>(json, GreenHiveJson.Options), "modules.manifest");
        return parsed
            .Bind(m => m is null ? Error.Validation("modules.manifest.empty", "modules:manifest.empty") : Result.Success(m))
            .Bind(m => m.Check().ToResult());
    }

    public Validation<ModuleManifest> Check()
    {
        var errors = new List<Error>();
        if (!Contracts.ModuleId.IsValid(ModuleId))
        {
            errors.Add(Error.Field("moduleId", "modules.manifest.moduleId", $"'{ModuleId}' is not a valid module ID."));
        }

        if (!SemanticVersion.TryParse(Version, out _))
        {
            errors.Add(Error.Field("version", "modules.manifest.version", $"'{Version}' is not a semantic version."));
        }

        if (!SemanticVersion.TryParse(MinHostVersion, out _))
        {
            errors.Add(Error.Field("minHostVersion", "modules.manifest.minHostVersion", $"'{MinHostVersion}' is not a semantic version."));
        }

        foreach (var dependency in Dependencies)
        {
            if (!VersionRange.IsValid(dependency.VersionRange))
            {
                errors.Add(Error.Field("dependencies", "modules.manifest.dependency", $"Invalid version range '{dependency.VersionRange}'."));
            }
        }

        return errors.Count == 0 ? Validation<ModuleManifest>.Valid(this) : Validation<ModuleManifest>.Invalid(errors);
    }
}
