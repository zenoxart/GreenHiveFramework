using GreenHive.NET.ModuleContracts;
using GreenHive.NET.Monads;

namespace GreenHive.Wpf.Framework.ModuleLoader;

/// <summary>Decides which modules can be loaded and in which order (dependencies first) - same rules as the registry.</summary>
public static class ModuleDependencyResolver
{
    public sealed record Plan(IReadOnlyList<LocalModule> Load, IReadOnlyList<(LocalModule Module, string Reason)> Skipped);

    public static Plan Resolve(IReadOnlyList<LocalModule> candidates, HostInfo host)
    {
        var manifests = candidates.Select(c => c.Manifest).ToList();
        var skipped = new List<(LocalModule, string)>();
        var usable = new List<LocalModule>();
        foreach (var candidate in candidates)
        {
            var check = ModuleCompatibility.Check(candidate.Manifest, host, manifests);
            if (check.IsValid)
            {
                usable.Add(candidate);
            }
            else
            {
                skipped.Add((candidate, string.Join("; ", check.Errors.Select(e => e.Message))));
            }
        }

        var ordered = ModuleCompatibility.OrderByDependencies(usable.Select(u => u.Manifest).ToList());
        return ordered.Match(
            list => new Plan(list.Select(m => usable.First(u => u.Manifest.ModuleId == m.ModuleId)).ToList(), skipped),
            error => new Plan([], skipped.Concat(usable.Select(u => (u, error.Message))).ToArray()));
    }
}
