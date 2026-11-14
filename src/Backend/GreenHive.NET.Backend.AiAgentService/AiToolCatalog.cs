using GreenHive.NET.Backend.Core.Http;
using GreenHive.NET.ModuleContracts;
using GreenHive.NET.WorkflowContracts;

namespace GreenHive.NET.Backend.AiAgentService;

/// <summary>
/// Turns workflow building blocks into tools the model can call: only the step's allowed tools that are headless module
/// step types (never approval steps, UI-only steps or administrative commands).
/// </summary>
public sealed class AiToolCatalog(ServiceClient services)
{
    private IReadOnlyList<ModuleManifest> _modules = [];
    private DateTimeOffset _loaded = DateTimeOffset.MinValue;

    public async Task<IReadOnlyList<AiTool>> ToolsForAsync(WorkflowStep step, CancellationToken ct)
    {
        if (step.Tools.Count == 0)
        {
            return [];
        }

        if (DateTimeOffset.UtcNow - _loaded > TimeSpan.FromMinutes(1))
        {
            var loaded = await services.GetAsync<IReadOnlyList<ModuleManifest>>("module-registry", "/internal/modules", null, ct);
            if (loaded.TryGetValue(out var modules))
            {
                _modules = modules;
                _loaded = DateTimeOffset.UtcNow;
            }
        }

        return _modules.SelectMany(m => m.ContractSurface.StepTypes)
            .Where(s => s.Headless && step.Tools.Contains(s.Id, StringComparer.OrdinalIgnoreCase))
            .Select(s => new AiTool(s.Id, s.Title, s.Input ?? new InputSchema(s.Id)))
            .ToList();
    }
}
