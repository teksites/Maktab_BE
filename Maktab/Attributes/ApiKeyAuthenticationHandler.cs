using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace Maktab.Attributes
{
    public class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        private readonly string _apiKey;

        public ApiKeyAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            ISystemClock clock,
            IConfiguration config)
            : base(options, logger, encoder, clock)
        {
            _apiKey = config["ApiSettings:ApiKey"]; // stored in appsettings.json
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("X-Api-Key", out var extractedApiKey))
                return Task.FromResult(AuthenticateResult.NoResult());

            if (_apiKey != extractedApiKey)
                return Task.FromResult(AuthenticateResult.Fail("Invalid API Key"));

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "ApiKeyUser"),
                new Claim(ClaimTypes.Name, "API Key User")
            };

            var identity = new ClaimsIdentity(claims, nameof(ApiKeyAuthenticationHandler));
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}