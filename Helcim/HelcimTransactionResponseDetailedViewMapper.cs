using System;
using MaktabDataContracts.Enums;
using MaktabDataContracts.Responses.Helcim;

namespace Helcim
{
    public static class HelcimTransactionResponseDetailedViewMapper
    {
        public static HelcimTransactionResponseDetailedView ToView(this HelcimTransactionResponseDetailed source)
        {
            var normalizedCardType = NormalizeCardType(source.CardType);
            var paymentSourceType = NormalizePaymentSourceType(source, normalizedCardType);

            return new HelcimTransactionResponseDetailedView
            {
                PaymentCode = source.PaymentCode,
                MaktabTransactionId = source.MaktabTransactionId,
                FamilyId = source.FamilyId,
                UserIp = source.UserIp,
                InvoiceId = source.InvoiceId,
                InvoiceNumber = source.InvoiceNumber,
                InvoiceToken = source.InvoiceToken,
                CustomerId = source.CustomerId,
                CustomerCode = source.CustomerCode,
                TransactionId = source.TransactionId,
                CardBatchId = source.CardBatchId,
                User = source.User,
                ApprovalCode = source.ApprovalCode,
                CardToken = source.CardToken,
                CardNumber = source.CardNumber,
                CardHolderName = source.CardHolderName,
                CardType = source.CardType,
                AvsResponse = source.AvsResponse,
                CvvResponse = source.CvvResponse,
                Warning = source.Warning,
                Amount = source.Amount,
                AmountPaid = source.AmountPaid,
                Currency = source.Currency,
                InvoiceStatus = source.InvoiceStatus,
                CardTransactionStatus = source.CardTransactionStatus,
                InvoiceType = source.InvoiceType,
                CardTransactionType = source.CardTransactionType,
                PaymentInstrument = source.PaymentInstrument,
                CardCompany = source.CardCompany,
                CardFundingType = source.CardFundingType,
                CardFundingTypeKnown = source.CardFundingTypeKnown,
                CardProduct = source.CardProduct,
                CardIssuer = source.CardIssuer,
                CardIssuerCountryCode = source.CardIssuerCountryCode,
                CardIssuerCountryName = source.CardIssuerCountryName,
                RawResponse = source.RawResponse,
                TransactionResponse = source.TransactionResponse,
                CreatedAt = source.CreatedAt,
                UpdatedOn = source.UpdatedOn,
                DatePaid = source.DatePaid,
                IsActive = source.IsActive,
                PaymentSourceType = paymentSourceType,
                PaymentSourceLabel = GetPaymentSourceLabel(paymentSourceType),
                NormalizedCardType = normalizedCardType,
                NormalizedCardTypeLabel = GetNormalizedCardTypeLabel(normalizedCardType),
                KnownCardType = MapKnownCardType(normalizedCardType)
            };
        }

        private static HelcimNormalizedCardType NormalizeCardType(string? rawCardType)
        {
            var normalized = rawCardType?.Trim().ToUpperInvariant();
            return normalized switch
            {
                "ACH" => HelcimNormalizedCardType.Ach,
                "DB" => HelcimNormalizedCardType.Debit,
                "VI" => HelcimNormalizedCardType.Visa,
                "MC" => HelcimNormalizedCardType.Mastercard,
                "AX" or "AE" or "AM" or "AMEX" => HelcimNormalizedCardType.AmericanExpress,
                "DS" or "DI" or "DC" => HelcimNormalizedCardType.Discover,
                null or "" or "WEBHOOK_RAW" => HelcimNormalizedCardType.Unknown,
                _ => HelcimNormalizedCardType.Other
            };
        }

        private static HelcimNormalizedPaymentSourceType NormalizePaymentSourceType(HelcimTransactionResponseDetailed source, HelcimNormalizedCardType cardType)
            => source.PaymentInstrument switch
            {
                "ACH" => HelcimNormalizedPaymentSourceType.Ach,
                "Interac Debit" or "Debit Card" => HelcimNormalizedPaymentSourceType.DebitCard,
                "Credit Card" => HelcimNormalizedPaymentSourceType.CreditCard,
                "Card" or "Prepaid Card" or "Charge Card" => HelcimNormalizedPaymentSourceType.Card,
                _ => cardType switch
                {
                    HelcimNormalizedCardType.Ach => HelcimNormalizedPaymentSourceType.Ach,
                    HelcimNormalizedCardType.Debit => HelcimNormalizedPaymentSourceType.DebitCard,
                    HelcimNormalizedCardType.Unknown => HelcimNormalizedPaymentSourceType.Unknown,
                    _ => HelcimNormalizedPaymentSourceType.Card
                }
            };

        private static CardType MapKnownCardType(HelcimNormalizedCardType cardType)
            => cardType switch
            {
                HelcimNormalizedCardType.Visa => CardType.Visa,
                HelcimNormalizedCardType.Mastercard => CardType.Master,
                HelcimNormalizedCardType.AmericanExpress => CardType.Amex,
                _ => CardType.Unknown
            };

        private static string GetPaymentSourceLabel(HelcimNormalizedPaymentSourceType paymentSourceType)
            => paymentSourceType switch
            {
                HelcimNormalizedPaymentSourceType.Ach => "ACH",
                HelcimNormalizedPaymentSourceType.DebitCard => "Debit Card",
                HelcimNormalizedPaymentSourceType.CreditCard => "Credit Card",
                HelcimNormalizedPaymentSourceType.Card => "Card",
                _ => "Unknown"
            };

        private static string GetNormalizedCardTypeLabel(HelcimNormalizedCardType cardType)
            => cardType switch
            {
                HelcimNormalizedCardType.Ach => "ACH",
                HelcimNormalizedCardType.Debit => "Debit",
                HelcimNormalizedCardType.Visa => "Visa",
                HelcimNormalizedCardType.Mastercard => "Mastercard",
                HelcimNormalizedCardType.AmericanExpress => "American Express",
                HelcimNormalizedCardType.Discover => "Discover",
                HelcimNormalizedCardType.Other => "Other",
                _ => "Unknown"
            };
    }
}
