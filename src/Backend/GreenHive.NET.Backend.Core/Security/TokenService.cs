using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using GreenHive.NET.Backend.Core.Hosting;
using GreenHive.NET.Monads;
using Microsoft.Extensions.Options;

namespace GreenHive.NET.Backend.Core.Security;

/// <summary>An issued token.</summary>
public sealed record IssuedToken(string AccessToken, DateTimeOffset ExpiresAt);

/// <summary>
/// Self-written, minimal JWT (HS256) issuer and validator - no external package needed.
/// Every service shares the signing key, so every service can validate tokens issued by the IdentityService.
/// </summary>
public sealed class TokenService(IOptions<GreenHiveOptions> options, TimeProvider time)
{
    private readonly GreenHiveOptions _options = options.Value;

    public IssuedToken Issue(GreenHiveUser user, TimeSpan? lifetime = null)
    {
        var now = time.GetUtcNow();
        var expires = now + (lifetime ?? TimeSpan.FromMinutes(_options.TokenLifetimeMinutes));
        var payload = new JsonObject
        {
            ["iss"] = _options.JwtIssuer,
            ["aud"] = _options.JwtAudience,
            ["iat"] = now.ToUnixTimeSeconds(),
            ["nbf"] = now.ToUnixTimeSeconds() - 30,
            ["exp"] = expires.ToUnixTimeSeconds(),
            [GreenHiveClaims.Subject] = user.Id.ToString(),
            [GreenHiveClaims.Name] = user.UserName,
            [GreenHiveClaims.DisplayName] = user.DisplayName,
            [GreenHiveClaims.Tenant] = user.TenantId.ToString(),
            [GreenHiveClaims.Permission] = new JsonArray(user.Permissions.Order().Select(p => (JsonNode?)JsonValue.Create(p)).ToArray()),
        };
        if (user.Actor is not null)
        {
            payload[GreenHiveClaims.Actor] = user.Actor;
        }

        if (user.Service is not null)
        {
            payload[GreenHiveClaims.Service] = user.Service;
        }

        var header = Base64Url(Encoding.UTF8.GetBytes("""{"alg":"HS256","typ":"JWT"}"""));
        var body = Base64Url(Encoding.UTF8.GetBytes(payload.ToJsonString()));
        var signature = Base64Url(Sign($"{header}.{body}"));
        return new IssuedToken($"{header}.{body}.{signature}", expires);
    }

    /// <summary>Issues a short-lived token for a service acting on its own behalf (all permissions it needs internally).</summary>
    public IssuedToken IssueServiceToken(string serviceName, IReadOnlySet<string> permissions) =>
        Issue(new GreenHiveUser(Guid.Empty, serviceName, serviceName, Guid.Empty, permissions) { Service = serviceName }, TimeSpan.FromMinutes(10));

    public Result<ClaimsPrincipal> Validate(string token)
    {
        var parts = token.Split('.');
        if (parts.Length != 3)
        {
            return Error.Unauthorized("auth.token.malformed", "auth:token.invalid");
        }

        byte[] signature;
        JsonObject? payload;
        try
        {
            signature = FromBase64Url(parts[2]);
            payload = JsonNode.Parse(FromBase64Url(parts[1]))?.AsObject();
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            return Error.Unauthorized("auth.token.malformed", "auth:token.invalid");
        }

        if (!CryptographicOperations.FixedTimeEquals(signature, Sign($"{parts[0]}.{parts[1]}")))
        {
            return Error.Unauthorized("auth.token.signature", "auth:token.invalid");
        }

        if (payload is null)
        {
            return Error.Unauthorized("auth.token.payload", "auth:token.invalid");
        }

        var now = time.GetUtcNow().ToUnixTimeSeconds();
        if (payload["exp"]?.GetValue<long>() is not { } exp || exp < now)
        {
            return Error.Unauthorized("auth.token.expired", "auth:token.expired");
        }

        if (payload["iss"]?.GetValue<string>() != _options.JwtIssuer || payload["aud"]?.GetValue<string>() != _options.JwtAudience)
        {
            return Error.Unauthorized("auth.token.audience", "auth:token.invalid");
        }

        var claims = new List<Claim>();
        foreach (var (key, value) in payload)
        {
            switch (value)
            {
                case JsonArray array:
                    claims.AddRange(array.Where(v => v is not null).Select(v => new Claim(key, v!.GetValue<string>())));
                    break;
                case JsonValue v when key is not ("iat" or "nbf" or "exp"):
                    claims.Add(new Claim(key, v.ToString()));
                    break;
            }
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "GreenHive", GreenHiveClaims.Name, "role"));
    }

    private byte[] Sign(string data) => HMACSHA256.HashData(Encoding.UTF8.GetBytes(_options.JwtKey), Encoding.UTF8.GetBytes(data));

    private static string Base64Url(byte[] data) => Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] FromBase64Url(string text)
    {
        var s = text.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(s.PadRight(s.Length + ((4 - (s.Length % 4)) % 4), '='));
    }
}
