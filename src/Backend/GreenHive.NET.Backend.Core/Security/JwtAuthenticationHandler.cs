using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GreenHive.NET.Backend.Core.Security;

/// <summary>Authentication handler for "Authorization: Bearer &lt;token&gt;" (and "access_token" query for event streams).</summary>
public sealed class JwtAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, TokenService tokens)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "GreenHive";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string? token = null;
        var header = Request.Headers.Authorization.ToString();
        if (header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            token = header["Bearer ".Length..].Trim();
        }
        else if (Request.Query.TryGetValue("access_token", out var queryToken))
        {
            token = queryToken.ToString();
        }

        if (string.IsNullOrEmpty(token))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        return Task.FromResult(tokens.Validate(token).Match(
            principal => AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)),
            error => AuthenticateResult.Fail(error.Code)));
    }
}
