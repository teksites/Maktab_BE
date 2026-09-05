using System;
using System.Text.Json.Serialization;
using MaktabDataContracts.Enums;
using MaktabDataContracts.Enums.Helcim;

namespace Helcim
{
    public class HelcimTransactionResponseDetailedView
    {
        public string PaymentCode { get; set; } = string.Empty;
        public Guid MaktabTransactionId { get; set; }
        public Guid FamilyId { get; set; }
        public string? UserIp { get; set; }
        public int InvoiceId { get; set; }
        public string? InvoiceNumber { get; set; }
        public string? InvoiceToken { get; set; }
        public int CustomerId { get; set; }
        public string? CustomerCode { get; set; }
        public int TransactionId { get; set; }
        public int CardBatchId { get; set; }
        public string? User { get; set; }
        public string? ApprovalCode { get; set; }
        public string? CardToken { get; set; }
        public string? CardNumber { get; set; }
        public string? CardHolderName { get; set; }
        public string? CardType { get; set; }
        public string? AvsResponse { get; set; }
        public string? CvvResponse { get; set; }
        public string? Warning { get; set; }
        public decimal Amount { get; set; }
        public decimal AmountPaid { get; set; }
        public HelcimCurrency Currency { get; set; }
        public HelcimInvoiceStatus InvoiceStatus { get; set; }
        public HelcimCardTransactionStatus CardTransactionStatus { get; set; }
        public HelcimInvoiceType InvoiceType { get; set; }
        public HelcimCardTransactionType CardTransactionType { get; set; }
        public string PaymentInstrument { get; set; } = "Unknown";
        public string CardCompany { get; set; } = string.Empty;
        public string CardFundingType { get; set; } = "Unknown";
        public bool CardFundingTypeKnown { get; set; }
        public string? CardProduct { get; set; }
        public string? CardIssuer { get; set; }
        public string? CardIssuerCountryCode { get; set; }
        public string? CardIssuerCountryName { get; set; }
        public string? RawResponse { get; set; }
        public string? TransactionResponse { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedOn { get; set; }
        public DateTime? DatePaid { get; set; }
        public bool IsActive { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public HelcimNormalizedPaymentSourceType PaymentSourceType { get; set; }

        public string PaymentSourceLabel { get; set; } = string.Empty;

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public HelcimNormalizedCardType NormalizedCardType { get; set; }

        public string NormalizedCardTypeLabel { get; set; } = string.Empty;

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public CardType KnownCardType { get; set; }
    }
}
