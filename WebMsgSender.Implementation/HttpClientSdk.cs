using InternalContracts;
//using Sedat.Implementation.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace WebMsgSender.Implementation
{
    public class HttpClientSdk : IHttpClientSdk
    {
        private readonly IClient _client;

        public HttpClientSdk(IClientConfiguration clientConfiguration)
        {
            switch (clientConfiguration.ClientType)
            {
                case HttpClientType.Elavon:
                case HttpClientType.ExchangeRates:
                    {
                        _client = new KeyClientFactory(clientConfiguration.BaseUrl, clientConfiguration.RelativeUrl, "");
                        break;
                    }
                //case HttpClientType.Sedat:
                //    {
                //        var token = new Token
                //        {
                //            AccessToken = ((SedatClientConfiguration)clientConfiguration).AccessToken,
                //            RefreshToken = ((SedatClientConfiguration)clientConfiguration).RefreshToken,
                //            AccessTokenExpirationDateUtc = ((SedatClientConfiguration)clientConfiguration).AccessTokenExpiryDateUtc
                //        };

                //        _client = new OAuth2ClientFactory((SedatClientConfiguration)clientConfiguration);
                //        break;
                //    }
                default:
                    throw new InvalidOperationException("Active configuration can have value Elavon or Sedat only");
            }
        }

        public HttpClient GetHttpClient()
        {
            return _client.GetHttpClient();
        }
    }
}
