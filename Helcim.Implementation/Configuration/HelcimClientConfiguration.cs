using Helcim.Configuration;
using InternalContracts;
using InternalContracts.Implementation;
using Microsoft.Extensions.Configuration;

namespace Helcim.Implementation.Configuration
{
    public class HelcimClientConfiguration : ClientConfiguration, IHelcimClientConfiguration
    {
        public string ApiVersionPath { get; init; }
        public string ApiToken { get; init; }
        public string SignatureVerificationToken { get; init; }

        public HelcimClientConfiguration(IConfiguration configuration) : base(configuration, HttpClientType.Helcim)
        {
            const string configPrefix = "Helcim";

            ApiVersionPath = string.IsNullOrWhiteSpace(configuration[$"{configPrefix}:api-version-path"])
                ? throw new InvalidOperationException("Helcim api-version-path is null.")
                : configuration[$"{configPrefix}:api-version-path"]!;

            var activeTokenKey = configuration[$"{configPrefix}:ActiveHelcimToken"];
            var activeEnvironmentProfile = configuration["ActiveEnvironmentProfile"];
            if (string.IsNullOrWhiteSpace(activeTokenKey) && !string.IsNullOrWhiteSpace(activeEnvironmentProfile))
            {
                activeTokenKey = configuration[$"{configPrefix}:EnvironmentTokens:{activeEnvironmentProfile}"];
            }

            if (!string.IsNullOrWhiteSpace(activeTokenKey))
            {
                var configuredToken = configuration[$"{configPrefix}:Tokens:{activeTokenKey}"];
                ApiToken = string.IsNullOrWhiteSpace(configuredToken)
                    ? throw new InvalidOperationException($"Helcim token '{activeTokenKey}' is null.")
                    : configuredToken!;
            }
            else
            {
                ApiToken = string.IsNullOrWhiteSpace(configuration[$"{configPrefix}:api-token"])
                    ? throw new InvalidOperationException("Helcim api-token is null.")
                    : configuration[$"{configPrefix}:api-token"]!;
            }

            SignatureVerificationToken = string.IsNullOrWhiteSpace(configuration[$"{configPrefix}:signature-verification-token"])
                ? throw new InvalidOperationException("Helcim signature-verification-token is null.")
                : configuration[$"{configPrefix}:signature-verification-token"]!;
        }
    }
}
