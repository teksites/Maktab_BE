using Microsoft.Extensions.Configuration;

namespace InternalContracts.Implementation
{
    public class ClientConfiguration : IClientConfiguration
    {
        public string BaseUrl { get; init; }
        public string RelativeUrl { get; init; }
        public TimeSpan RequestTimeout { get; init; }
        public int RetryAttempts { get; init; }
        public HttpClientType ClientType { get; init; }

        public ClientConfiguration(IConfiguration configuration, HttpClientType clientType)//Elavon or Maktab
        {
            string configPrefix = clientType.ToString();

            BaseUrl = String.IsNullOrEmpty(configuration[configPrefix + ":BaseUrl"]) ? throw new InvalidOperationException("Base URL is null.") : configuration[configPrefix + ":BaseUrl"].ToString();
            RelativeUrl = String.IsNullOrEmpty(configuration[configPrefix + ":RelativeUrl"]) ? throw new InvalidOperationException("Relative URL is null.") : configuration[configPrefix + ":RelativeUrl"].ToString();
            RequestTimeout = String.IsNullOrEmpty(configuration[configPrefix + ":RequestTimeout"]) ? throw new InvalidOperationException("RequestTimeout is null.") : TimeSpan.FromSeconds(Convert.ToInt32(configuration[configPrefix + ":RequestTimeout"].ToString()));
            RetryAttempts = String.IsNullOrEmpty(configuration[configPrefix + ":RetryAttempts"]) ? throw new InvalidOperationException("RetryAttempts is null.") : Convert.ToInt32(configuration[configPrefix + ":RetryAttempts"].ToString());
            ClientType = clientType;
        }
    }
}
