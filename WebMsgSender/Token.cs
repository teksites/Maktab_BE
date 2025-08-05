using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebMsgSender
{
    public class Token
    {
        [JsonProperty("access")]
        public string AccessToken { get; set; }

        [JsonProperty("token_type")]
        public string TokenType { get; set; }

        [JsonProperty("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonProperty("refresh")]
        public string RefreshToken { get; set; }

        public string Error { get; set; }

        //Access _token issue date UTC.
        [JsonProperty("AccessTokenIssueDateUtc")]
        public DateTime? AccessTokenIssueDateUtc { get; set; }

        //Access _token UTC expiration date
        [JsonProperty("AccessTokenExpirationDateUtc")]
        public DateTime? AccessTokenExpirationDateUtc { get; set; }

        //Access _token issue date UTC
        [JsonProperty("RefreshTokenExpirationDateUtc")]
        public DateTime? RefreshTokenExpirationDateUtc { get; set; }
    }
}
