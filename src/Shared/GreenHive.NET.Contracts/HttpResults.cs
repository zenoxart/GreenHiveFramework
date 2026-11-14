using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GreenHive.NET.Monads;

namespace GreenHive.NET.Contracts;

/// <summary>
/// Turns HTTP responses into Results - used by the client API clients and by service-to-service calls alike.
/// Failures are read from the ApiError body (Problem Details); network failures become Unavailable/Timeout.
/// </summary>
public static class HttpResults
{
    public static async Task<Result<T>> SendAsync<T>(HttpClient client, HttpRequestMessage request, CancellationToken ct = default)
    {
        try
        {
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            return await ReadAsync<T>(response, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or TimeoutException or OperationCanceledException)
        {
            return Try.Map(ex, "http.request");
        }
    }

    public static Task<Result<T>> GetAsync<T>(HttpClient client, string url, CancellationToken ct = default) =>
        SendAsync<T>(client, new HttpRequestMessage(HttpMethod.Get, url), ct);

    public static Task<Result<T>> PostAsync<T>(HttpClient client, string url, object? body, CancellationToken ct = default) =>
        SendAsync<T>(client, WithBody(HttpMethod.Post, url, body), ct);

    public static Task<Result<T>> PutAsync<T>(HttpClient client, string url, object? body, CancellationToken ct = default) =>
        SendAsync<T>(client, WithBody(HttpMethod.Put, url, body), ct);

    public static async Task<Result<Unit>> DeleteAsync(HttpClient client, string url, CancellationToken ct = default) =>
        (await SendAsync<JsonElement?>(client, new HttpRequestMessage(HttpMethod.Delete, url), ct).ConfigureAwait(false)).ToUnit();

    public static HttpRequestMessage WithBody(HttpMethod method, string url, object? body) => new(method, url)
    {
        Content = body is null ? null : JsonContent.Create(body, body.GetType(), options: GreenHiveJson.Options),
    };

    public static async Task<Result<T>> ReadAsync<T>(HttpResponseMessage response, CancellationToken ct = default)
    {
        if (response.IsSuccessStatusCode)
        {
            if (typeof(T) == typeof(Unit))
            {
                return (T)(object)Unit.Value;
            }

            if (response.StatusCode == HttpStatusCode.NoContent || response.Content.Headers.ContentLength == 0)
            {
                return Result<T>.Success(default!);
            }

            try
            {
                var value = await response.Content.ReadFromJsonAsync<T>(GreenHiveJson.Options, ct).ConfigureAwait(false);
                return Result<T>.Success(value!);
            }
            catch (JsonException ex)
            {
                return Error.Unexpected("http.response.invalid", "errors:invalidResponse", ex);
            }
        }

        ApiError? apiError = null;
        try
        {
            var text = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            if (text.TrimStart().StartsWith('{'))
            {
                apiError = JsonSerializer.Deserialize<ApiError>(text, GreenHiveJson.Options);
            }
        }
        catch (JsonException)
        {
        }

        var status = (int)response.StatusCode;
        if (apiError is not null && apiError.Code != "unknown")
        {
            return (apiError with { Status = status }).ToError();
        }

        var kind = ApiError.KindOf(status);
        return new Error(kind, $"http.{status}", $"errors:http.{status}");
    }
}
