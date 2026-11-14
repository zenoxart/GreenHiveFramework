using GreenHive.NET.Monads;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace GreenHive.NET.Backend.Core.Http;

/// <summary>
/// Last line of defence: catches exceptions that escaped an adapter (bugs), logs them with the correlation ID and
/// returns a 500 Problem Details. Every hit is a bug to fix.
/// </summary>
public sealed class ExceptionBoundaryMiddleware(RequestDelegate next, ILogger<ExceptionBoundaryMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // Client went away - nothing to answer.
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception for {Method} {Path} (correlation {CorrelationId})",
                context.Request.Method, context.Request.Path, context.TraceIdentifier);
            if (!context.Response.HasStarted)
            {
                context.Response.Clear();
                await ResultHttpMapper.Problem(Error.Unexpected("server.unhandled", "errors:unexpected", ex)).ExecuteAsync(context);
            }
        }
    }
}

/// <summary>Reads or creates the correlation ID (header X-Correlation-Id) and echoes it in the response.</summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string Header = "X-Correlation-Id";

    public Task InvokeAsync(HttpContext context)
    {
        var id = context.Request.Headers[Header].ToString();
        if (string.IsNullOrWhiteSpace(id) || id.Length > 64)
        {
            id = Guid.NewGuid().ToString("n");
        }

        context.TraceIdentifier = id;
        context.Response.Headers[Header] = id;
        return next(context);
    }
}
