using Helcim.Configuration;
using Microsoft.Extensions.Configuration;

namespace Helcim.Implementation.Configuration
{
    public sealed class CardBinCheckConfiguration : ICardBinCheckConfiguration
    {
        private const string ConfigurationSection = "CardBinCheck";

        public bool Enabled { get; }
        public string BaseUrl { get; }
        public string ApiKey { get; }
        public string ApiKeyHeaderName { get; }
        public TimeSpan RequestTimeout { get; }
        public TimeSpan CacheDuration { get; }
        public int MaximumLookupsPerResponse { get; }

        public CardBinCheckConfiguration(IConfiguration configuration)
        {
            Enabled = !bool.TryParse(configuration[$"{ConfigurationSection}:Enabled"], out var enabled) || enabled;
            BaseUrl = configuration[$"{ConfigurationSection}:BaseUrl"]?.TrimEnd('/')
                ?? "https://cardbincheck.com";
            ApiKey = configuration[$"{ConfigurationSection}:ApiKey"] ?? string.Empty;
            ApiKeyHeaderName = string.IsNullOrWhiteSpace(configuration[$"{ConfigurationSection}:ApiKeyHeaderName"])
                ? "X-Api-Key"
                : configuration[$"{ConfigurationSection}:ApiKeyHeaderName"]!;
            RequestTimeout = TimeSpan.FromSeconds(
                int.TryParse(configuration[$"{ConfigurationSection}:RequestTimeoutSeconds"], out var timeoutSeconds)
                    ? Math.Clamp(timeoutSeconds, 1, 30)
                    : 5);
            CacheDuration = TimeSpan.FromHours(
                int.TryParse(configuration[$"{ConfigurationSection}:CacheDurationHours"], out var cacheDurationHours)
                    ? Math.Clamp(cacheDurationHours, 1, 720)
                    : 24);
            MaximumLookupsPerResponse = int.TryParse(configuration[$"{ConfigurationSection}:MaximumLookupsPerResponse"], out var maximumLookups)
                ? Math.Clamp(maximumLookups, 1, 25)
                : 10;
        }
    }
}
