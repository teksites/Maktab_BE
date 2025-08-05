using Elavon.Configuration;
using InternalContracts;
using InternalContracts.Implementation;
using Microsoft.Extensions.Configuration;

namespace Elavon.Implementation.Configuration
{
    public class ElavonClientConfiguration : ClientConfiguration, IElavonClientConfiguration
    {
        public string MerchantId { get; init; }

        public string UserId { get; init; }

        public string Pin { get; init; }

        public bool TestMode { get; init; }

        public ElavonClientConfiguration(IConfiguration configuration) : base(configuration, HttpClientType.Elavon)//Elavon or 
        {
            string configPrefix = "Elavon";

            MerchantId = String.IsNullOrEmpty(configuration[configPrefix + ":MerchantId"]) ? throw new InvalidOperationException("MerchantId is null.") : configuration[configPrefix + ":MerchantId"].ToString();
            UserId = String.IsNullOrEmpty(configuration[configPrefix + ":UserId"]) ? throw new InvalidOperationException("UserId Elavon is null.") : configuration[configPrefix + ":UserId"].ToString();
            Pin = String.IsNullOrEmpty(configuration[configPrefix + ":Pin"]) ? throw new InvalidOperationException("Pin is null.") : configuration[configPrefix + ":Pin"].ToString();
            TestMode = String.IsNullOrEmpty(configuration[configPrefix + ":TestMode"]) ? throw new InvalidOperationException("TestMode is null.") : Convert.ToBoolean(configuration[configPrefix + ":TestMode"].ToString());
        }
    }
}
