using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Stripe.Api;

public sealed class StripeApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "StripeApiKey";
    public const string HeaderName = "X-Stripe-Api-Key";
    private readonly IConfiguration _configuration;

    public StripeApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        System.Text.Encodings.Web.UrlEncoder encoder,
        IConfiguration configuration)
        : base(options, logger, encoder)
    {
        _configuration = configuration;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var expectedApiKey = _configuration["StripeApi:ApiKey"];
        if (string.IsNullOrWhiteSpace(expectedApiKey))
            return Task.FromResult(AuthenticateResult.Fail("Stripe API key is not configured."));

        if (!Request.Headers.TryGetValue(HeaderName, out var submittedApiKey))
            return Task.FromResult(AuthenticateResult.NoResult());

        var expectedBytes = Encoding.UTF8.GetBytes(expectedApiKey);
        var submittedBytes = Encoding.UTF8.GetBytes(submittedApiKey.ToString());
        if (!CryptographicOperations.FixedTimeEquals(expectedBytes, submittedBytes))
            return Task.FromResult(AuthenticateResult.Fail("Invalid Stripe API key."));

        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "StripeApiClient") }, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}
