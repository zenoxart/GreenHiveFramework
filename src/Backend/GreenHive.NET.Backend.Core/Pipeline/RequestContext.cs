using GreenHive.NET.Backend.Core.Security;
using Microsoft.AspNetCore.Http;

namespace GreenHive.NET.Backend.Core.Pipeline;

/// <summary>
/// Who calls and when: the authenticated user (or the RunAs user of a workflow / AI run, marked by Actor),
/// the correlation ID and the current time. Domain code never reads DateTime.Now; time comes from here.
/// </summary>
public sealed class RequestContext(IHttpContextAccessor accessor, TimeProvider time)
{
    private GreenHiveUser? _user;

    public GreenHiveUser User => _user ??= accessor.HttpContext is { } http ? GreenHiveUser.FromPrincipal(http.User) : GreenHiveUser.Anonymous;

    public string CorrelationId => accessor.HttpContext?.TraceIdentifier ?? Guid.NewGuid().ToString("n");

    public DateTimeOffset Now => time.GetUtcNow();

    /// <summary>The raw bearer token of the caller (to call other services on the user's behalf).</summary>
    public string? BearerToken
    {
        get
        {
            var header = accessor.HttpContext?.Request.Headers.Authorization.ToString();
            return header is not null && header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? header[7..].Trim() : null;
        }
    }

    /// <summary>Overrides the user for background work (workflow engine, AI agent).</summary>
    public RequestContext As(GreenHiveUser user)
    {
        _user = user;
        return this;
    }
}
