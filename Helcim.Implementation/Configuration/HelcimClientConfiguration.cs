using Helcim.Configuration;
using InternalContracts;
using InternalContracts.Implementation;
using Microsoft.Extensions.Configuration;

namespace Helcim.Implementation.Configuration
{
    public class HelcimClientConfiguration : ClientConfiguration, IHelcimClientConfiguration
    {
        public string ApiToken { get; init; }

        public HelcimClientConfiguration(IConfiguration configuration) : base(configuration, HttpClientType.Helcim)
        {
            const string configPrefix = "Helcim";

            ApiToken = string.IsNullOrWhiteSpace(configuration[$"{configPrefix}:api-token"])
                ? throw new InvalidOperationException("Helcim api-token is null.")
                : configuration[$"{configPrefix}:api-token"]!;
        }
    }
}
