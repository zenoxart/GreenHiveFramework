using System.Collections.Concurrent;
using GreenHive.NET.Backend.Core.Http;
using GreenHive.NET.Contracts.Api;
using GreenHive.NET.ModuleContracts;
using GreenHive.NET.Monads;
using GreenHive.NET.WorkflowContracts;

namespace GreenHive.NET.Backend.WorkflowService;

/// <summary>
/// The current module manifests (from the Module Registry, cached for a minute): which module owns a command,
/// step type, schema or event, and where its headless endpoint is.
/// </summary>
public sealed class ModuleDirectory(ServiceClient services, ILogger<ModuleDirectory> logger)
{
    private IReadOnlyList<ModuleManifest> _modules = [];
    private DateTimeOffset _loadedAt = DateTimeOffset.MinValue;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task<IReadOnlyList<ModuleManifest>> ModulesAsync(CancellationToken ct)
    {
        if (DateTimeOffset.UtcNow - _loadedAt < TimeSpan.FromMinutes(1))
        {
            return _modules;
        }

        await _lock.WaitAsync(ct);
        try
        {
            if (DateTimeOffset.UtcNow - _loadedAt >= TimeSpan.FromMinutes(1))
            {
                var loaded = await services.GetAsync<IReadOnlyList<ModuleManifest>>("module-registry", "/internal/modules", null, ct);
                if (loaded.TryGetValue(out var modules, out var error))
                {
                    _modules = modules;
                    _loadedAt = DateTimeOffset.UtcNow;
                }
                else
                {
                    logger.LogWarning("Module manifests could not be loaded: {Error}", error);
                    _loadedAt = DateTimeOffset.UtcNow.AddSeconds(-45);
                }
            }
        }
        finally
        {
            _lock.Release();
        }

        return _modules;
    }

    public void Invalidate() => _loadedAt = DateTimeOffset.MinValue;

    public async Task<Func<string, string?>> ModuleOfAsync(CancellationToken ct)
    {
        var modules = await ModulesAsync(ct);
        return id =>
        {
            foreach (var m in modules)
            {
                var s = m.ContractSurface;
                if (s.Declares(DependencyKind.Command, id) || s.Declares(DependencyKind.StepType, id) ||
                    s.Declares(DependencyKind.InputSchema, id) || s.Declares(DependencyKind.Event, id) ||
                    (!string.IsNullOrEmpty(m.Prefix) && id.StartsWith(m.Prefix + ".", StringComparison.OrdinalIgnoreCase)))
                {
                    return m.ModuleId;
                }
            }

            return null;
        };
    }

    public async Task<Func<string, string?>> VersionOfAsync(CancellationToken ct)
    {
        var modules = await ModulesAsync(ct);
        return moduleId => modules.FirstOrDefault(m => m.ModuleId.Equals(moduleId, StringComparison.OrdinalIgnoreCase))?.Version;
    }

    /// <summary>Headless endpoint of a step type or command (relative to the gateway).</summary>
    public async Task<Result<string>> EndpointAsync(WorkflowStep step, CancellationToken ct)
    {
        var modules = await ModulesAsync(ct);
        string? endpoint = step.Kind switch
        {
            WorkflowStepKind.ModuleStep => modules.SelectMany(m => m.ContractSurface.StepTypes)
                .FirstOrDefault(s => s.Id.Equals(step.StepTypeId, StringComparison.OrdinalIgnoreCase))?.Endpoint,
            WorkflowStepKind.Command => modules.SelectMany(m => m.ContractSurface.Commands)
                .FirstOrDefault(c => c.Id.Equals(step.CommandId, StringComparison.OrdinalIgnoreCase))?.Endpoint,
            _ => null,
        };
        return endpoint is null
            ? Error.NotFound("workflow.step.endpoint", $"No headless endpoint for '{step.StepTypeId ?? step.CommandId}'.")
            : endpoint;
    }

    public async Task<StepTypeDescriptor?> StepTypeAsync(string id, CancellationToken ct) =>
        (await ModulesAsync(ct)).SelectMany(m => m.ContractSurface.StepTypes).FirstOrDefault(s => s.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
}

/// <summary>On-behalf-of tokens from the IdentityService (cached until shortly before they expire).</summary>
public sealed class TokenBroker(ServiceClient services)
{
    private readonly ConcurrentDictionary<(Guid, string), LoginResponse> _cache = new();

    public async Task<Result<LoginResponse>> ForAsync(Guid userId, string actor, CancellationToken ct)
    {
        if (_cache.TryGetValue((userId, actor), out var cached) && cached.ExpiresAt > DateTimeOffset.UtcNow.AddMinutes(2))
        {
            return cached;
        }

        var token = await services.PostAsync<LoginResponse>("identity-service", "/internal/tokens/delegate", new DelegateTokenRequest(userId, actor), null, ct);
        return token.Tap(t => _cache[(userId, actor)] = t);
    }
}
