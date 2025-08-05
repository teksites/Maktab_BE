using InternalContracts;
using System.Xml.Serialization;

namespace Elavon.Requests
{
    public class TransferMoneyRequest2 : BaseRequest
    {
        [XmlAttribute(AttributeName = "ssl_merchant_id")]
        public required string MerchantId { get; set; }

        [XmlAttribute(AttributeName = "ssl_user_id")]
        public required string UserId { get; set; }

        [XmlAttribute(AttributeName = "ssl_pin")]
        public required string Pin { get; set; }

        [XmlAttribute(AttributeName = "ssl_test_mode")]
        public bool TestMode { get; set; }

        [XmlAttribute(AttributeName = "ssl_transaction_type")]
        public required string TransactionType { get; set; }
        
        [XmlAttribute(AttributeName = "ssl_card_number")]
        public required string CardNumber { get; set; }

        [XmlAttribute(AttributeName = "ssl_exp_date")]
        public required string ExpiryDate { get; set; }

        [XmlAttribute(AttributeName = "ssl_amount")]
        public required double Amount { get; set; }


/*        <txn>\n
    <ssl_merchant_id>0022986</ssl_merchant_id>\n
    <ssl_user_id> apiuser</ssl_user_id>\n
    <ssl_pin> UEO4XQHE3Y28V77BMPV1ZFLTIMGISQSVRTK80905NIKS0TWWK4AEQ8CC14NPG99U</ssl_pin>\n
    <ssl_test_mode>false</ssl_test_mode>\n
    <ssl_transaction_type> ccsale</ssl_transaction_type>\n
    <ssl_card_number>4000000000000002</ssl_card_number>\n
    <ssl_exp_date>1226</ssl_exp_date>\n
    <ssl_amount>5.00</ssl_amount>\n
</txn>\n*/



    }
}
