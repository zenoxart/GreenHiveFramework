using GreenHive.NET.Contracts;
using GreenHive.NET.Monads;

namespace GreenHive.NET.ModuleContracts;

/// <summary>Facts about a host, used to decide whether a module fits.</summary>
public sealed record HostInfo(string HostVersion, string DesktopRuntimeVersion, int ContractsVersion = 1);

/// <summary>Rules for whether a module fits a host. Evaluated identically on the server (Registry) and the client (Loader).</summary>
public static class ModuleCompatibility
{
    public static Validation<ModuleManifest> Check(ModuleManifest manifest, HostInfo host, IReadOnlyCollection<ModuleManifest>? installed = null)
    {
        var errors = new List<Error>();

        if (SemanticVersion.TryParse(host.HostVersion, out var hostVersion) &&
            SemanticVersion.TryParse(manifest.MinHostVersion, out var minHost) && hostVersion < minHost)
        {
            errors.Add(Error.Validation("modules.compatibility.host", $"Host {host.HostVersion} is older than {manifest.MinHostVersion}."));
        }

        if (manifest.Client is { } client && SemanticVersion.TryParse(host.DesktopRuntimeVersion, out var sdk) &&
            !new VersionRange(client.DesktopRuntime).Satisfies(sdk))
        {
            errors.Add(Error.Validation("modules.compatibility.desktopRuntime",
                $".NET Desktop Runtime {host.DesktopRuntimeVersion} is outside {client.DesktopRuntime}."));
        }

        if (manifest.ContractsVersion != host.ContractsVersion)
        {
            errors.Add(Error.Validation("modules.compatibility.contracts",
                $"Module uses contracts v{manifest.ContractsVersion}, the host provides v{host.ContractsVersion}."));
        }

        if (installed is not null)
        {
            foreach (var dependency in manifest.Dependencies)
            {
                var match = installed.FirstOrDefault(m => m.ModuleId.Equals(dependency.ModuleId, StringComparison.OrdinalIgnoreCase));
                if (match is null)
                {
                    errors.Add(Error.Validation("modules.compatibility.dependencyMissing", $"Requires module '{dependency.ModuleId}'."));
                }
                else if (!new VersionRange(dependency.VersionRange).Satisfies(match.Version))
                {
                    errors.Add(Error.Validation("modules.compatibility.dependencyVersion",
                        $"Requires '{dependency.ModuleId}' {dependency.VersionRange}, found {match.Version}."));
                }
            }
        }

        return errors.Count == 0 ? Validation<ModuleManifest>.Valid(manifest) : Validation<ModuleManifest>.Invalid(errors);
    }

    /// <summary>Orders modules so that dependencies come first; fails on cycles.</summary>
    public static Result<IReadOnlyList<ModuleManifest>> OrderByDependencies(IReadOnlyCollection<ModuleManifest> modules)
    {
        var byId = modules.ToDictionary(m => m.ModuleId, StringComparer.OrdinalIgnoreCase);
        var ordered = new List<ModuleManifest>();
        var state = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        bool Visit(ModuleManifest module)
        {
            if (state.TryGetValue(module.ModuleId, out var s))
            {
                return s == 2;
            }

            state[module.ModuleId] = 1;
            foreach (var dependency in module.Dependencies)
            {
                if (byId.TryGetValue(dependency.ModuleId, out var target) && !Visit(target))
                {
                    return false;
                }
            }

            state[module.ModuleId] = 2;
            ordered.Add(module);
            return true;
        }

        foreach (var module in modules.OrderBy(m => m.ModuleId, StringComparer.OrdinalIgnoreCase))
        {
            if (!Visit(module))
            {
                return Error.Conflict("modules.dependencies.cycle", $"Dependency cycle involving '{module.ModuleId}'.");
            }
        }

        return ordered;
    }
}
