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
        public bool ReconciliationEnabled { get; init; }
        public string ReconciliationCronSchedule { get; init; }
        public int ReconciliationLookbackDays { get; init; }
        public int AchReconciliationPageSize { get; init; }
        public int CardReconciliationPageSize { get; init; }

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

            var activeSignatureTokenKey = configuration[$"{configPrefix}:ActiveSignatureVerificationToken"];
            if (string.IsNullOrWhiteSpace(activeSignatureTokenKey) && !string.IsNullOrWhiteSpace(activeEnvironmentProfile))
            {
                activeSignatureTokenKey = configuration[$"{configPrefix}:EnvironmentSignatureVerificationTokens:{activeEnvironmentProfile}"];
            }

            if (!string.IsNullOrWhiteSpace(activeSignatureTokenKey))
            {
                var configuredSignatureToken = configuration[$"{configPrefix}:SignatureVerificationTokens:{activeSignatureTokenKey}"];
                SignatureVerificationToken = string.IsNullOrWhiteSpace(configuredSignatureToken)
                    ? throw new InvalidOperationException($"Helcim signature verification token '{activeSignatureTokenKey}' is null.")
                    : configuredSignatureToken!;
            }
            else
            {
                SignatureVerificationToken = string.IsNullOrWhiteSpace(configuration[$"{configPrefix}:signature-verification-token"])
                    ? throw new InvalidOperationException("Helcim signature-verification-token is null.")
                    : configuration[$"{configPrefix}:signature-verification-token"]!;
            }

            ReconciliationEnabled = bool.TryParse(configuration[$"{configPrefix}:Reconciliation:Enabled"], out var enabled)
                ? enabled
                : true;

            ReconciliationCronSchedule = string.IsNullOrWhiteSpace(configuration[$"{configPrefix}:Reconciliation:CronSchedule"])
                ? "0 0 2 * * ?"
                : configuration[$"{configPrefix}:Reconciliation:CronSchedule"]!;

            ReconciliationLookbackDays = int.TryParse(configuration[$"{configPrefix}:Reconciliation:LookbackDays"], out var lookbackDays)
                ? Math.Max(1, lookbackDays)
                : 7;

            AchReconciliationPageSize = int.TryParse(configuration[$"{configPrefix}:Reconciliation:AchPageSize"], out var achPageSize)
                ? Math.Clamp(achPageSize, 1, 125)
                : 125;

            CardReconciliationPageSize = int.TryParse(configuration[$"{configPrefix}:Reconciliation:CardPageSize"], out var cardPageSize)
                ? Math.Clamp(cardPageSize, 1, 1000)
                : 1000;
        }
    }
}
