using InternalContracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Polly;
using Polly.Timeout;
using System.Net;
using System.Text;

namespace WebMsgSender.Implementation
{
    public class WebMsgSenderService : IWebMsgSenderService
    {
        private readonly ILogger _logger;

        public WebMsgSenderService( ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger("WebMsgSenderService");
        }

        private readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            ContractResolver = new DefaultContractResolver
            {
                NamingStrategy = new CamelCaseNamingStrategy(),
            },
            NullValueHandling = NullValueHandling.Ignore
        };

        public async Task<string> SendMessage(JsonMessageData message, IClientConfiguration clientConfiguration, HttpMethod httpMethod)
        {
            var httpSdk = new HttpClientSdk(clientConfiguration);
            var _client = httpSdk.GetHttpClient();

            var failedAttempts = 0;
            var timeOutPolicy = Policy.TimeoutAsync<HttpResponseMessage>(
                   clientConfiguration.RequestTimeout, TimeoutStrategy.Pessimistic);

            var retryCounter = 1;

            var httpRetryPolicy = Policy<HttpResponseMessage>.Handle<HttpRequestException>()
                .OrResult(r => r.StatusCode == HttpStatusCode.InternalServerError || r.StatusCode == HttpStatusCode.NotFound || r.StatusCode == HttpStatusCode.Unauthorized)
                .RetryAsync(retryCount: clientConfiguration.RetryAttempts, onRetryAsync: async (exception, retryCount, context) =>
                {
                    try
                    {
                        int statusCode = exception.Result != null ? (int)exception.Result.StatusCode : 500;

                        _logger.LogInformation($"Performed retry operation : {retryCounter++} to send the data : {message.Payload} message raised on external endpoint : {message.ExternalEndpoint} completed with status code : {statusCode}");

                        return;
                    }

                    catch (Exception e)
                    {
                        if (e is HttpRequestException || e is TaskCanceledException || e is InvalidOperationException)
                        {
                            failedAttempts += 1;
                        }

                        _logger.LogError($"{e.ToString()}: Exception on sending a retry operation : {retryCounter++} for the data on external endpoint : {message.ExternalEndpoint}");
                    }
                });

            using (_client = httpSdk.GetHttpClient())//.CreateWebhookHttpClient(subscription.ExternalEndpoint, subscription.DefaultHeaders))
            {
                var requestMesssage = await CreateHttpMessageRequest(message.ExternalEndpoint, message.Payload, httpMethod, null).ConfigureAwait(false);

                try
                {
                    var response = await httpRetryPolicy.ExecuteAsync(() =>
                    timeOutPolicy.ExecuteAsync(async () =>
                    {
                        return await _client.SendAsync(await CreateHttpMessageRequest(message.ExternalEndpoint, message.Payload, httpMethod, null).ConfigureAwait(false)).ConfigureAwait(false);

                    }));

                    if (response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.BadRequest)
                    {
                        if (response != null)
                        {
                            var readstr = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                            return readstr;
                        }
                        _logger.LogInformation($"Sent the Transaction data : {message.Payload}  on external endpoint : {message.ExternalEndpoint} with status code : {response.StatusCode} on attempt {retryCounter++}");
                        return String.Empty;
                    }
                }

                catch (Exception)
                {
                    _logger.LogError($"Exception on sending message for : {message.Payload} on external endpoint : {message.ExternalEndpoint}");
                    return String.Empty;
                }
                return String.Empty;
            }
        }


        private StringContent CreateContent(JsonMessageData request)
        {
            return new StringContent(
                JsonConvert.SerializeObject(request, Settings),
                Encoding.UTF8,
                "application/json"
            );
        }

        public async Task<HttpRequestMessage> CreateHttpMessageRequest(string url, StringContent content, HttpMethod method, Dictionary<string, string> headers = null)
        {
            var httpRequest = new HttpRequestMessage()
            {
                RequestUri = new Uri(url),
                Method = method,
                Content = content
            };

        

            if (headers != null)
            {
                httpRequest.Headers.Clear();
                headers.ToList().ForEach(header => httpRequest.Headers.Add(header.Key, header.Value));
            }

            return httpRequest;
        }

        public async Task<HttpRequestMessage> CreateHttpMessageRequest(string url, HttpContent content, HttpMethod method, Dictionary<string, string> headers = null)
        {
            var httpRequest = new HttpRequestMessage()
            {
                RequestUri = new Uri(url),
                Method = method,
                Content = content
            };



            if (headers != null)
            {
                httpRequest.Headers.Clear();
                headers.ToList().ForEach(header => httpRequest.Headers.Add(header.Key, header.Value));
            }

            return httpRequest;
        }

        public async Task<string> SendMessage(XmlMessageData message, IClientConfiguration clientConfiguration, HttpMethod httpMethod)
        {
            var httpSdk = new HttpClientSdk(clientConfiguration);
            var _client = httpSdk.GetHttpClient();

            var failedAttempts = 0;
            var timeOutPolicy = Policy.TimeoutAsync<HttpResponseMessage>(
                   clientConfiguration.RequestTimeout, TimeoutStrategy.Pessimistic);

            var retryCounter = 1;

            var httpRetryPolicy = Policy<HttpResponseMessage>.Handle<HttpRequestException>()
                .OrResult(r => r.StatusCode == HttpStatusCode.InternalServerError || r.StatusCode == HttpStatusCode.NotFound || r.StatusCode == HttpStatusCode.Unauthorized)
                .RetryAsync(retryCount: clientConfiguration.RetryAttempts, onRetryAsync: async (exception, retryCount, context) =>
                {
                    try
                    {
                        int statusCode = exception.Result != null ? (int)exception.Result.StatusCode : 500;

                        _logger.LogInformation($"Performed retry operation : {retryCounter++} to send the data : {message.Payload} message raised on external endpoint : {message.ExternalEndpoint} completed with status code : {statusCode}");

                        return;
                    }

                    catch (Exception e)
                    {
                        if (e is HttpRequestException || e is TaskCanceledException || e is InvalidOperationException)
                        {
                            failedAttempts += 1;
                        }

                        _logger.LogError($"{e.ToString()}: Exception on sending a retry operation : {retryCounter++} for the data on external endpoint : {message.ExternalEndpoint}");
                    }
                });

            using (_client = httpSdk.GetHttpClient())//.CreateWebhookHttpClient(subscription.ExternalEndpoint, subscription.DefaultHeaders))
            {
                var requestMesssage = await CreateHttpMessageRequest(message.ExternalEndpoint, message.Payload, httpMethod, null).ConfigureAwait(false);

                try
                {
                    var response = await httpRetryPolicy.ExecuteAsync(() =>
                    timeOutPolicy.ExecuteAsync(async () =>
                    {
                        return await _client.SendAsync(await CreateHttpMessageRequest(message.ExternalEndpoint, message.Payload, httpMethod, null).ConfigureAwait(false)).ConfigureAwait(false);

                    }));

                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        if (response != null)
                        {
                            var readstr = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                            return readstr;
                        }
                        _logger.LogInformation($"Sent the Transaction data : {message.Payload}  on external endpoint : {message.ExternalEndpoint} with status code : {response.StatusCode} on attempt {retryCounter++}");
                        return String.Empty;
                    }
                }

                catch (Exception)
                {
                    _logger.LogError($"Exception on sending message for : {message.Payload} on external endpoint : {message.ExternalEndpoint}");
                    return String.Empty;
                }
                return String.Empty;
            }
        }
    }
}
