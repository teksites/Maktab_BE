namespace Helcim.Services
{
    public sealed class CardBinLookupResult
    {
        public string PaymentInstrument { get; init; } = "Unknown";
        public string CardCompany { get; init; } = string.Empty;
        public string CardFundingType { get; init; } = "Unknown";
        public bool CardFundingTypeKnown { get; init; }
        public string? CardProduct { get; init; }
        public string? CardIssuer { get; init; }
        public string? CardIssuerCountryCode { get; init; }
        public string? CardIssuerCountryName { get; init; }
    }
}
