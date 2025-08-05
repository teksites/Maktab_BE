using InternalContracts;
using System.Xml.Serialization;

namespace Elavon.Response
{
    public class TransferMoneyResponse2 : BaseRequest
    {
        [XmlAttribute(AttributeName = "ssl_issuer_response")]
        public string IssuerResponse { get; set; }

        [XmlAttribute(AttributeName = "ssl_first_name")]
        public string FirstName { get; set; }

        [XmlAttribute(AttributeName = "ssl_last_name")]
        public string LastName { get; set; }

        [XmlAttribute(AttributeName = "ssl_company")]
        public string Company { get; set; }

        [XmlAttribute(AttributeName = "ssl_phone")]
        public string Phone { get; set; }

        [XmlAttribute(AttributeName = "ssl_card_number")]
        public string CardNumber { get; set; }

        [XmlAttribute(AttributeName = "ssl_departure_date")]
        public required string DepartureDate { get; set; }

        [XmlAttribute(AttributeName = "ssl_oar_data")]
        public string OarData { get; set; }


        [XmlAttribute(AttributeName = "ssl_result")]
        public string Result { get; set; }

        [XmlAttribute(AttributeName = "ssl_txn_id")]
        public string TxnId { get; set; }

        [XmlAttribute(AttributeName = "ssl_loyalty_program")]
        public string LoyalityProgram { get; set; }

        [XmlAttribute(AttributeName = "ssl_avs_response")]
        public string AvsResponse { get; set; }

        [XmlAttribute(AttributeName = "ssl_approval_code")]
        public string ApprovalCode { get; set; }

        [XmlAttribute(AttributeName = "ssl_account_status")]
        public string AccountStatus { get; set; }

        [XmlAttribute(AttributeName = "ssl_email")]
        public string Email { get; set; }

        [XmlAttribute(AttributeName = "ssl_amount")]
        public double Amount { get; set; }

        [XmlAttribute(AttributeName = "ssl_avs_zip")]
        public string AvsZip { get; set; }

        [XmlAttribute(AttributeName = "ssl_txn_time")]
        public DateTime TxnTime { get; set; }

        [XmlAttribute(AttributeName = "ssl_description")]
        public string Description { get; set; }

        [XmlAttribute(AttributeName = "ssl_exp_date")]
        public string ExpiryDate { get; set; }

        [XmlAttribute(AttributeName = "ssl_card_short_description")]
        public string CardShortDescription { get; set; }

        [XmlAttribute(AttributeName = "ssl_completion_date")]
        public DateTime CompletionDate { get; set; }

        [XmlAttribute(AttributeName = "ssl_address2")]
        public string Address2 { get; set; }

        [XmlAttribute(AttributeName = "ssl_get_token")]
        public string GetToken { get; set; }

        [XmlAttribute(AttributeName = "ssl_customer_code")]
        public string CustomerCode { get; set; }

        [XmlAttribute(AttributeName = "ssl_country")]
        public string Country { get; set; }

        [XmlAttribute(AttributeName = "ssl_city")]
        public string City { get; set; }

        [XmlAttribute(AttributeName = "ssl_card_type")]
        public string CardType { get; set; }

        [XmlAttribute(AttributeName = "ssl_access_code")]
        public string AccessCode { get; set; }

        [XmlAttribute(AttributeName = "ssl_transaction_type")]
        public string TransactionType { get; set; }

        [XmlAttribute(AttributeName = "ssl_loyalty_account_balance")]
        public double LoyalityAccountBalance { get; set; }

        [XmlAttribute(AttributeName = "ssl_salestax")]
        public double SalesTax { get; set; }

        [XmlAttribute(AttributeName = "ssl_avs_address")]
        public string AvsAddress { get; set; }

        [XmlAttribute(AttributeName = "ssl_account_balance")]
        public double AccountBalance { get; set; }

        [XmlAttribute(AttributeName = "ssl_ps2000_data")]
        public string Ps2000Data { get; set; }

        [XmlAttribute(AttributeName = "ssl_state")]
        public string State { get; set; }

        [XmlAttribute(AttributeName = "ssl_ship_to_zip")]
        public string ShipToZip { get; set; }

        [XmlAttribute(AttributeName = "ssl_result_message")]
        public string ResultMessage { get; set; }

        [XmlAttribute(AttributeName = "ssl_invoice_number")]
        public string InvoiceNumber { get; set; }

        [XmlAttribute(AttributeName = "ssl_ship_to_address1")]
        public string ShipToAddress1 { get; set; }

        [XmlAttribute(AttributeName = "ssl_cvv2_response")]
        public string Cvv2Response { get; set; }

        [XmlAttribute(AttributeName = "ssl_tender_amount")]
        public double TenderAmount { get; set; }

        [XmlAttribute(AttributeName = "ssl_partner_app_id")]
        public string PartnerAppId { get; set; }

        /* 
         <txn>
            <ssl_issuer_response>00</ssl_issuer_response>
            <ssl_last_name></ssl_last_name>
            <ssl_company></ssl_company>
            <ssl_phone></ssl_phone>
            <ssl_card_number>40**********0002</ssl_card_number>
            <ssl_departure_date></ssl_departure_date>
            <ssl_oar_data>010011433606241445380000047554200000000000042576417614114336</ssl_oar_data>
            <ssl_result>0</ssl_result>
            <ssl_txn_id>240624O2C-D0116F3A-3CD8-4B6E-A93F-108CB2B62F44</ssl_txn_id>
            <ssl_loyalty_program></ssl_loyalty_program>
            <ssl_avs_response> </ssl_avs_response>
            <ssl_approval_code>042576</ssl_approval_code>
            <ssl_account_status></ssl_account_status>
            <ssl_email></ssl_email>
            <ssl_amount>5.00</ssl_amount>
            <ssl_avs_zip></ssl_avs_zip>
            <ssl_txn_time>06/24/2024 10:45:38 AM</ssl_txn_time>
            <ssl_description></ssl_description>
            <ssl_exp_date>1226</ssl_exp_date>
            <ssl_card_short_description>VISA</ssl_card_short_description>
            <ssl_completion_date></ssl_completion_date>
            <ssl_address2></ssl_address2>
            <ssl_get_token>N</ssl_get_token>
            <ssl_customer_code></ssl_customer_code>
            <ssl_country></ssl_country>
            <ssl_card_type>CREDITCARD</ssl_card_type>
            <ssl_access_code></ssl_access_code>
            <ssl_transaction_type>SALE</ssl_transaction_type>
            <ssl_loyalty_account_balance></ssl_loyalty_account_balance>
            <ssl_salestax></ssl_salestax>
            <ssl_avs_address></ssl_avs_address>
            <ssl_account_balance>0.00</ssl_account_balance>
            <ssl_ps2000_data>W7541765313966924021A</ssl_ps2000_data>
            <ssl_state></ssl_state>
            <ssl_ship_to_zip></ssl_ship_to_zip>
            <ssl_city></ssl_city>
            <ssl_result_message>APPROVAL</ssl_result_message>
            <ssl_first_name></ssl_first_name>
            <ssl_invoice_number></ssl_invoice_number>
            <ssl_ship_to_address1></ssl_ship_to_address1>
            <ssl_cvv2_response>S</ssl_cvv2_response>
            <ssl_tender_amount></ssl_tender_amount>
            <ssl_partner_app_id>01</ssl_partner_app_id>
        </txn>


         */



    }
}
