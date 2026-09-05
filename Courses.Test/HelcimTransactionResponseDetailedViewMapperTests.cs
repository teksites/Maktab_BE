using Helcim;
using MaktabDataContracts.Enums;
using MaktabDataContracts.Enums.Helcim;
using MaktabDataContracts.Responses.Helcim;

namespace Courses.Test;

public class HelcimTransactionResponseDetailedViewMapperTests
{
    [Theory]
    [InlineData("ACH", HelcimNormalizedPaymentSourceType.Ach, "ACH", HelcimNormalizedCardType.Ach, "ACH", CardType.Unknown)]
    [InlineData("DB", HelcimNormalizedPaymentSourceType.DebitCard, "Debit Card", HelcimNormalizedCardType.Debit, "Debit", CardType.Unknown)]
    [InlineData("MC", HelcimNormalizedPaymentSourceType.Card, "Card", HelcimNormalizedCardType.Mastercard, "Mastercard", CardType.Master)]
    [InlineData("VI", HelcimNormalizedPaymentSourceType.Card, "Card", HelcimNormalizedCardType.Visa, "Visa", CardType.Visa)]
    [InlineData("AX", HelcimNormalizedPaymentSourceType.Card, "Card", HelcimNormalizedCardType.AmericanExpress, "American Express", CardType.Amex)]
    [InlineData("ZZ", HelcimNormalizedPaymentSourceType.Card, "Card", HelcimNormalizedCardType.Other, "Other", CardType.Unknown)]
    [InlineData("", HelcimNormalizedPaymentSourceType.Unknown, "Unknown", HelcimNormalizedCardType.Unknown, "Unknown", CardType.Unknown)]
    public void ToView_NormalizesCardTypeMetadata(
        string rawCardType,
        HelcimNormalizedPaymentSourceType expectedSourceType,
        string expectedSourceLabel,
        HelcimNormalizedCardType expectedNormalizedCardType,
        string expectedCardLabel,
        CardType expectedKnownCardType)
    {
        var source = CreateResponse(rawCardType);

        var result = source.ToView();

        Assert.Equal(rawCardType, result.CardType);
        Assert.Equal(expectedSourceType, result.PaymentSourceType);
        Assert.Equal(expectedSourceLabel, result.PaymentSourceLabel);
        Assert.Equal(expectedNormalizedCardType, result.NormalizedCardType);
        Assert.Equal(expectedCardLabel, result.NormalizedCardTypeLabel);
        Assert.Equal(expectedKnownCardType, result.KnownCardType);
    }

    [Fact]
    public void ToView_WhenBinLookupConfirmsVisaDebit_PreservesTheEnrichedDetails()
    {
        var source = CreateResponse("VI");
        source.PaymentInstrument = "Debit Card";
        source.CardCompany = "Visa";
        source.CardFundingType = "Debit";
        source.CardFundingTypeKnown = true;
        source.CardProduct = "Classic";
        source.CardIssuer = "Example Bank";
        source.CardIssuerCountryCode = "CA";
        source.CardIssuerCountryName = "Canada";

        var result = source.ToView();

        Assert.Equal(HelcimNormalizedPaymentSourceType.DebitCard, result.PaymentSourceType);
        Assert.Equal("Debit Card", result.PaymentSourceLabel);
        Assert.Equal("Debit Card", result.PaymentInstrument);
        Assert.Equal("Visa", result.CardCompany);
        Assert.Equal("Debit", result.CardFundingType);
        Assert.True(result.CardFundingTypeKnown);
        Assert.Equal("Classic", result.CardProduct);
        Assert.Equal("Example Bank", result.CardIssuer);
        Assert.Equal("CA", result.CardIssuerCountryCode);
        Assert.Equal("Canada", result.CardIssuerCountryName);
    }

    private static HelcimTransactionResponseDetailed CreateResponse(string rawCardType)
        => new()
        {
            PaymentCode = "PAY001",
            MaktabTransactionId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            FamilyId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            UserIp = "127.0.0.1",
            InvoiceId = 1001,
            InvoiceNumber = "INV-001",
            InvoiceToken = "token-1",
            CustomerId = 2001,
            CustomerCode = "CST1001",
            TransactionId = 3001,
            CardBatchId = 4001,
            User = "Helcim System",
            ApprovalCode = "APPROVED1",
            CardToken = "card-token",
            CardNumber = "5454545454",
            CardHolderName = "Test User",
            CardType = rawCardType,
            AvsResponse = "X",
            CvvResponse = "M",
            Warning = string.Empty,
            Amount = 100m,
            AmountPaid = 100m,
            Currency = HelcimCurrency.Cad,
            InvoiceStatus = HelcimInvoiceStatus.Paid,
            CardTransactionStatus = HelcimCardTransactionStatus.Approved,
            InvoiceType = HelcimInvoiceType.Invoice,
            CardTransactionType = HelcimCardTransactionType.Purchase,
            RawResponse = "{}",
            TransactionResponse = "{}",
            CreatedAt = new DateTime(2026, 8, 10, 12, 0, 0, DateTimeKind.Utc),
            UpdatedOn = new DateTime(2026, 8, 10, 12, 5, 0, DateTimeKind.Utc),
            DatePaid = new DateTime(2026, 8, 10, 12, 10, 0, DateTimeKind.Utc),
            IsActive = true
        };
}
