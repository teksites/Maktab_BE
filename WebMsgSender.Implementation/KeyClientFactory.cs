using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace WebMsgSender.Implementation
{
    public class KeyClientFactory : IClient
    {
        private readonly HttpClient _httpClient;

        public KeyClientFactory(string baseUrl, string relativeUrl, string apiKey)
        {
            var baseAddress = new Uri(new Uri(baseUrl), relativeUrl);

            var apiKeyHandler = new ApiKeyHttpClientHandler(apiKey)
            {
                AllowAutoRedirect = false
            };

            _httpClient = new HttpClient(apiKeyHandler, true)
            {
                BaseAddress = baseAddress,
                Timeout = TimeSpan.FromSeconds(600)
            };
        }

        public HttpClient GetHttpClient()
        {
            return _httpClient;
        }
    }
}
