using InternalContracts;
using System.Globalization;
using System.Xml.Serialization;

namespace Elavon.Response
{
    [XmlRoot("txn")]
    public class ElavonTransferMoneyResponse : BaseRequest
    {
        
        [XmlElement("errorCode")]
        public string ErrorCode { get; set; }

        [XmlElement("errorName")]
        public string ErrorName { get; set; }

        [XmlElement("ssl_issuer_response")]
        public string IssuerResponse { get; set; }

        [XmlElement("ssl_first_name")]
        public string FirstName { get; set; }

        [XmlElement("ssl_last_name")]
        public string LastName { get; set; }

        [XmlElement("ssl_company")]
        public string Company { get; set; }

        [XmlElement("ssl_phone")]
        public string Phone { get; set; }

        [XmlElement("ssl_card_number")]
        public string CardNumber { get; set; }

        [XmlElement("ssl_departure_date")]
        public required string DepartureDate { get; set; }

        [XmlElement("ssl_oar_data")]
        public string OarData { get; set; }

        [XmlElement("ssl_result")]
        public string Result { get; set; }

        [XmlElement("ssl_txn_id")]
        public string TxnId { get; set; }

        [XmlElement("ssl_loyalty_program")]
        public string LoyalityProgram { get; set; }

        [XmlElement("ssl_avs_response")]
        public string AvsResponse { get; set; }

        [XmlElement("ssl_approval_code")]
        public string ApprovalCode { get; set; }

        [XmlElement("ssl_account_status")]
        public string AccountStatus { get; set; }

        [XmlElement("ssl_email")]
        public string Email { get; set; }

        [XmlIgnore]
        public double Amount { get; set; }

        [XmlElement("ssl_amount")]
        public string AmountStr
        {
            get { return Amount.ToString(); }
            set { Amount = !string.IsNullOrEmpty(value) ? Convert.ToDouble(value) : 0; }
        }

        [XmlElement("ssl_avs_zip")]
        public string AvsZip { get; set; }

        [XmlIgnore]
        public DateTime TransactionTime { get; set; }

        [XmlElement("ssl_txn_time")]
        public string TxnTime
        {
            get { return this.TransactionTime.ToString(); }
            set { this.TransactionTime = !string.IsNullOrEmpty(value) ? DateTime.ParseExact(value, "MM/dd/yyyy h:mm:ss tt", CultureInfo.InvariantCulture): DateTime.MinValue; }
        }

        [XmlElement("ssl_description")]
        public string Description { get; set; }

        [XmlElement("ssl_exp_date")]
        public string ExpiryDate { get; set; }

        [XmlElement("ssl_card_short_description")]
        public string CardShortDescription { get; set; }

        [XmlIgnore]
        public DateTime CompletionDate { get; set; }

        [XmlElement("ssl_completion_date")]
        public string ComplDate
        {
            get { return this.CompletionDate.ToString(); }
            set { this.CompletionDate = !string.IsNullOrEmpty(value) ? DateTime.ParseExact(value, "MM/dd/yyyy h:mm:ss tt", CultureInfo.InvariantCulture) : DateTime.MinValue; }
        }

        [XmlElement("ssl_address2")]
        public string Address2 { get; set; }

        [XmlElement("ssl_get_token")]
        public string GetToken { get; set; }

        [XmlElement("ssl_customer_code")]
        public string CustomerCode { get; set; }

        [XmlElement("ssl_country")]
        public string Country { get; set; }

        [XmlElement("ssl_city")]
        public string City { get; set; }

        [XmlElement("ssl_card_type")]
        public string CardType { get; set; }

        [XmlElement("ssl_access_code")]
        public string AccessCode { get; set; }

        [XmlElement("ssl_transaction_type")]
        public string TransactionType { get; set; }

        [XmlIgnore]
        public double LoyalityAccountBalance { get; set; }

        [XmlElement("ssl_loyalty_account_balance")]
        public string LoyalityAccountBalanceStr
        {
            get { return LoyalityAccountBalance.ToString(); }
            set { LoyalityAccountBalance = !string.IsNullOrEmpty(value) ? Convert.ToDouble(value) : 0; }
        }

        [XmlIgnore]
        public double SalesTax { get; set; }

        [XmlElement("ssl_salestax")]
        public string SalesTaxStr
        {
            get { return SalesTax.ToString(); }
            set { SalesTax = !string.IsNullOrEmpty(value) ? Convert.ToDouble(value) : 0; }
        }

        [XmlElement("ssl_avs_address")]
        public string AvsAddress { get; set; }

     
        [XmlIgnore]
        public double AccountBalance { get; set; }

        [XmlElement("ssl_account_balance")]
        public string AccountBalanceStr
        {
            get { return AccountBalance.ToString(); }
            set { AccountBalance = !string.IsNullOrEmpty(value) ? Convert.ToDouble(value) : 0; }
        }

        [XmlElement("ssl_ps2000_data")]
        public string Ps2000Data { get; set; }

        [XmlElement("ssl_state")]
        public string State { get; set; }

        [XmlElement("ssl_ship_to_zip")]
        public string ShipToZip { get; set; }

        [XmlElement("ssl_result_message")]
        public string ResultMessage { get; set; }

        [XmlElement("ssl_invoice_number")]
        public string InvoiceNumber { get; set; }

        [XmlElement("ssl_ship_to_address1")]
        public string ShipToAddress1 { get; set; }

        [XmlElement("ssl_cvv2_response")]
        public string Cvv2Response { get; set; }

        [XmlIgnore]
        public double TenderAmount { get; set; }

        [XmlElement("ssl_tender_amount")]
        public string TenderAmountStr
        {
            get { return TenderAmount.ToString(); }
            set { TenderAmount = !string.IsNullOrEmpty(value) ? Convert.ToDouble(value) : 0; }
        }
        [XmlElement("ssl_partner_app_id")]
        public string PartnerAppId { get; set; }
    }
}
