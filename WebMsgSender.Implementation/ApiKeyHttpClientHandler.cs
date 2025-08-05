using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace WebMsgSender.Implementation
{
    /// <summary>
    /// Handler to authorize each requests with partner API key
    /// </summary>
    public class ApiKeyHttpClientHandler : HttpClientHandler
    {
        private const string ApiKeyHeader = "X-Api-Key";

        public string ApiKey { get; }

        public ApiKeyHttpClientHandler(string apiKey)
        {
            ApiKey = apiKey;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (!string.IsNullOrWhiteSpace(ApiKey))
            {
                request.Headers.Add(ApiKeyHeader, ApiKey);
            }
            return base.SendAsync(request, cancellationToken);
        }
    }
}
