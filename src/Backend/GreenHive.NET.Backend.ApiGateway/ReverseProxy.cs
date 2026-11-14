using System.Net;
using GreenHive.NET.Backend.Core.Http;
using GreenHive.NET.Monads;

namespace GreenHive.NET.Backend.ApiGateway;

/// <summary>Forwards a request to a backend service and streams the response back (flushes per chunk for event streams).</summary>
public sealed class ReverseProxy(IHttpClientFactory factory, ILogger<ReverseProxy> logger)
{
    private static readonly HashSet<string> HopByHop = new(StringComparer.OrdinalIgnoreCase)
    {
        "Connection", "Keep-Alive", "Proxy-Authenticate", "Proxy-Authorization", "TE", "Trailer", "Transfer-Encoding", "Upgrade", "Host",
    };

    public async Task ForwardAsync(HttpContext context, string targetBase)
    {
        var request = context.Request;
        var target = $"{targetBase.TrimEnd('/')}{request.PathBase}{request.Path}{request.QueryString}";
        using var message = new HttpRequestMessage(new HttpMethod(request.Method), target);

        if (request.ContentLength > 0 || request.Headers.TransferEncoding.Count > 0)
        {
            message.Content = new StreamContent(request.Body);
        }

        foreach (var (name, values) in request.Headers)
        {
            if (HopByHop.Contains(name))
            {
                continue;
            }

            if (!message.Headers.TryAddWithoutValidation(name, values.ToArray()))
            {
                message.Content?.Headers.TryAddWithoutValidation(name, values.ToArray());
            }
        }

        message.Headers.TryAddWithoutValidation("X-Forwarded-For", context.Connection.RemoteIpAddress?.ToString());
        message.Headers.TryAddWithoutValidation("X-Forwarded-Proto", request.Scheme);
        message.Headers.TryAddWithoutValidation("X-Forwarded-Host", request.Host.Value);
        message.Headers.Remove(CorrelationIdMiddleware.Header);
        message.Headers.TryAddWithoutValidation(CorrelationIdMiddleware.Header, context.TraceIdentifier);

        HttpResponseMessage response;
        try
        {
            response = await factory.CreateClient("proxy").SendAsync(message, HttpCompletionOption.ResponseHeadersRead, context.RequestAborted);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !context.RequestAborted.IsCancellationRequested)
        {
            logger.LogWarning("Backend {Target} unreachable: {Message}", targetBase, ex.Message);
            await ResultHttpMapper.Problem(Error.Unavailable("gateway.backend.unavailable", "errors:unavailable").WithMetadata("service", targetBase))
                .ExecuteAsync(context);
            return;
        }

        using (response)
        {
            context.Response.StatusCode = (int)response.StatusCode;
            foreach (var (name, values) in response.Headers.Concat(response.Content.Headers))
            {
                if (!HopByHop.Contains(name))
                {
                    context.Response.Headers[name] = values.ToArray();
                }
            }

            if (response.StatusCode == HttpStatusCode.NoContent || request.Method == HttpMethods.Head)
            {
                return;
            }

            var streaming = response.Content.Headers.ContentType?.MediaType == "text/event-stream";
            if (streaming)
            {
                context.Response.Headers.CacheControl = "no-cache";
                context.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpResponseBodyFeature>()?.DisableBuffering();
            }

            await using var body = await response.Content.ReadAsStreamAsync(context.RequestAborted);
            var buffer = new byte[16 * 1024];
            try
            {
                int read;
                while ((read = await body.ReadAsync(buffer, context.RequestAborted)) > 0)
                {
                    await context.Response.Body.WriteAsync(buffer.AsMemory(0, read), context.RequestAborted);
                    if (streaming)
                    {
                        await context.Response.Body.FlushAsync(context.RequestAborted);
                    }
                }
            }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
            {
                // client disconnected (event streams end this way)
            }
        }
    }
}
