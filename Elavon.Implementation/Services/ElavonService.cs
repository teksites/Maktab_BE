using Elavon.Configuration;
using Elavon.Requests;
using Elavon.Response;
using Elavon.Services;
using InternalContracts;
using MaktabDataContracts.Models;
using System.Xml.Serialization;
using WebMsgSender;

namespace Elavon.Implementation.Services
{
    public class ElavonService : IElavonService
    {
        private readonly IWebMsgSenderService _senderService;
        private readonly IElavonClientConfiguration _clientConfiguration;

        public ElavonService(IWebMsgSenderService senderService, IElavonClientConfiguration clientConfiguration)
        {
            _senderService = senderService;
            _clientConfiguration = clientConfiguration;
        }
        public async Task<ElavonTransferMoneyResponse> TransferMoney(ElavonTransferMoneyRequest transferMoney)
        {
            transferMoney.MerchantId = _clientConfiguration.MerchantId;
            transferMoney.UserId = _clientConfiguration.UserId;
            transferMoney.Pin = _clientConfiguration.Pin;
            //transferMoney.CardNumber = transferMoney.CardNumber;
            //transferMoney.ExpiryDate = transferMoney.ExpiryDate;
            //transferMoney.Amount = 10.0;
            transferMoney.TestMode = _clientConfiguration.TestMode;
            transferMoney.TransactionType = "ccsale";

            transferMoney.CvcIndicator = 1;
            //transferMoney.CvcCode = card
            string xmlMessage =  MyXmlSerializer<ElavonTransferMoneyRequest>.Serialize(transferMoney) ;

            xmlMessage = xmlMessage.Replace("\n", "").Replace("\r", "");

            var formData = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("xmldata", xmlMessage),
            };

            // Encodes the key-value pairs for the ContentType 'application/x-www-form-urlencoded'
    //        HttpContent content = new FormUrlEncodedContent(formData);

            XmlMessageData messageData = new XmlMessageData
            {
                ExternalEndpoint = _clientConfiguration.BaseUrl + _clientConfiguration.RelativeUrl,

                // Encodes the key-value pairs for the ContentType 'application/x-www-form-urlencoded'
                Payload = new FormUrlEncodedContent(formData)
            };

            var responseStr = await _senderService.SendMessage(messageData, _clientConfiguration, HttpMethod.Post).ConfigureAwait(false);

            var response = ConverXmlToObject(responseStr);
            return response;
        }

        private string ConvertObjectToXml(ElavonTransferMoneyRequest request)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(ElavonTransferMoneyRequest));

            // Serialize the object to a string
            using (StringWriter writer = new StringWriter())
            {
                serializer.Serialize(writer, request);
               // var str = "<txn>\n" + writer.ToString() + "</txn>\n";
                var str =  writer.ToString();
               // _clientConfiguration.
                return str;
            }
        }
        private ElavonTransferMoneyResponse ConverXmlToObject(string xmlResponse)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(ElavonTransferMoneyResponse));
            using (TextReader reader = new StringReader(xmlResponse))
            {
                ElavonTransferMoneyResponse result = (ElavonTransferMoneyResponse)serializer.Deserialize(reader);
                return result;
            }
        }


    }
}
