using InternalContracts;
using System.Xml.Serialization;

namespace Elavon.Requests
{
    [XmlRoot("txn")]
    public class ElavonTransferMoneyRequest : BaseRequest
    {
        [XmlElement("ssl_account_id")]
        public string MerchantId { get; set; }

        [XmlElement("ssl_user_id")]
        public string UserId { get; set; }

        [XmlElement("ssl_pin")]
        public string Pin { get; set; }

        [XmlElement("ssl_test_mode")]
        public bool TestMode { get; set; }

        [XmlElement("ssl_transaction_type")]
        public string TransactionType { get; set; }
        
        [XmlElement("ssl_card_number")]
        public required string CardNumber { get; set; }

        [XmlElement("ssl_exp_date")]
        public required string ExpiryDate { get; set; }

        [XmlElement("ssl_amount")]
        public required double Amount { get; set; }
        
        [XmlElement("ssl_avs_zip")]
        public string AvsZip { get; set; }

        [XmlElement("ssl_first_name")]
        public string FirstName { get; set; }

        [XmlElement("ssl_last_name")]
        public string LastName { get; set; }

        [XmlElement("ssl_cvv2cvc2_indicator")]
        public int CvcIndicator { get; set; } = 1;

        [XmlElement("ssl_cvv2cvc2")]
        public int CvcCode { get; set; } = 1;
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
