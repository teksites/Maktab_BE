using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Helcim.Configuration;
using Helcim.Services;
using MaktabDataContracts.Responses.Helcim;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Helcim.Implementation.Services
{
    public sealed class CardBinLookupService : ICardBinLookupService
    {
        private const string CacheKeyPrefix = "card-bin-check:";
        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _cache;
        private readonly ICardBinCheckConfiguration _configuration;
        private readonly ILogger<CardBinLookupService> _logger;

        public CardBinLookupService(
            HttpClient httpClient,
            IMemoryCache cache,
            ICardBinCheckConfiguration configuration,
            ILogger<CardBinLookupService> logger)
        {
            _httpClient = httpClient;
            _cache = cache;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<CardBinLookupResult> GetDisplayDetailsAsync(
            string? rawCardType,
            string? maskedCardNumber,
            CancellationToken cancellationToken = default)
        {
            var baseline = CreateBaseline(rawCardType);
            var bin = ExtractBin(maskedCardNumber);
            if (!_configuration.Enabled || !ShouldLookup(rawCardType, bin))
            {
                return baseline;
            }

            var providerDetails = await GetProviderDetailsAsync(bin!, cancellationToken).ConfigureAwait(false);
            return providerDetails == null ? baseline : Merge(baseline, providerDetails);
        }

        public Task EnrichAsync(
            IList<HelcimTransactionResponse> transactions,
            CancellationToken cancellationToken = default)
            => EnrichAsync(
                transactions,
                transaction => transaction.CardType,
                transaction => transaction.CardNumber,
                Apply,
                cancellationToken);

        public Task EnrichDetailedAsync(
            IList<HelcimTransactionResponseDetailed> transactions,
            CancellationToken cancellationToken = default)
            => EnrichAsync(
                transactions,
                transaction => transaction.CardType,
                transaction => transaction.CardNumber,
                Apply,
                cancellationToken);

        private async Task EnrichAsync<T>(
            IList<T> transactions,
            Func<T, string?> getCardType,
            Func<T, string?> getCardNumber,
            Action<T, CardBinLookupResult> apply,
            CancellationToken cancellationToken)
        {
            var uncachedLookupCount = 0;

            foreach (var transaction in transactions)
            {
                var cardType = getCardType(transaction);
                var cardNumber = getCardNumber(transaction);
                var bin = ExtractBin(cardNumber);

                if (ShouldLookup(cardType, bin) && !IsCached(bin!) && uncachedLookupCount >= _configuration.MaximumLookupsPerResponse)
                {
                    apply(transaction, CreateBaseline(cardType));
                    continue;
                }

                if (ShouldLookup(cardType, bin) && !IsCached(bin!))
                {
                    uncachedLookupCount++;
                }

                var details = await GetDisplayDetailsAsync(cardType, cardNumber, cancellationToken).ConfigureAwait(false);
                apply(transaction, details);
            }
        }

        private async Task<CardBinProviderDetails?> GetProviderDetailsAsync(string bin, CancellationToken cancellationToken)
        {
            var cacheKey = CacheKeyPrefix + bin;
            if (_cache.TryGetValue(cacheKey, out CardBinProviderDetails? cached))
            {
                return cached;
            }

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/bin/{bin}");
                request.Headers.TryAddWithoutValidation("Accept", "application/json");

                if (!string.IsNullOrWhiteSpace(_configuration.ApiKey))
                {
                    request.Headers.TryAddWithoutValidation(_configuration.ApiKeyHeaderName, _configuration.ApiKey);
                }

                using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    Cache(cacheKey, null, TimeSpan.FromHours(1));
                    return null;
                }

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Card BIN lookup returned status code {StatusCode}.", (int)response.StatusCode);
                    return null;
                }

                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                var result = JsonSerializer.Deserialize<CardBinCheckResponse>(responseContent, SerializerOptions);
                if (result == null || !result.Success || result.Data == null)
                {
                    return null;
                }

                var details = CardBinProviderDetails.From(result.Data);
                Cache(cacheKey, details, _configuration.CacheDuration);
                return details;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Card BIN lookup timed out.");
                return null;
            }
            catch (HttpRequestException exception)
            {
                _logger.LogWarning(exception, "Card BIN lookup request failed.");
                return null;
            }
            catch (JsonException exception)
            {
                _logger.LogWarning(exception, "Card BIN lookup returned an invalid response.");
                return null;
            }
        }

        private void Cache(string cacheKey, CardBinProviderDetails? details, TimeSpan duration)
        {
            // Cache negative results too, so an unknown BIN does not repeatedly consume the provider quota.
            _cache.Set(cacheKey, details, duration);
        }

        private bool IsCached(string bin)
            => _cache.TryGetValue(CacheKeyPrefix + bin, out CardBinProviderDetails? _);

        private static bool ShouldLookup(string? rawCardType, string? bin)
            => !string.IsNullOrWhiteSpace(bin)
                && !string.Equals(rawCardType?.Trim(), "ACH", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(rawCardType?.Trim(), "DB", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(rawCardType?.Trim(), "WEBHOOK_RAW", StringComparison.OrdinalIgnoreCase);

        private static string? ExtractBin(string? maskedCardNumber)
        {
            if (string.IsNullOrWhiteSpace(maskedCardNumber))
            {
                return null;
            }

            var digits = new string(maskedCardNumber.Where(char.IsDigit).ToArray());
            return digits.Length >= 6 ? digits[..6] : null;
        }

        private static CardBinLookupResult CreateBaseline(string? rawCardType)
        {
            var normalized = rawCardType?.Trim().ToUpperInvariant();
            return normalized switch
            {
                "ACH" => new CardBinLookupResult
                {
                    PaymentInstrument = "ACH",
                    CardCompany = "",
                    CardFundingType = "Unknown"
                },
                "DB" => new CardBinLookupResult
                {
                    PaymentInstrument = "Interac Debit",
                    CardCompany = "Interac",
                    CardFundingType = "Debit",
                    CardFundingTypeKnown = true
                },
                _ => new CardBinLookupResult
                {
                    PaymentInstrument = string.IsNullOrWhiteSpace(normalized) || normalized == "WEBHOOK_RAW" ? "Unknown" : "Card",
                    CardCompany = GetCardCompany(normalized),
                    CardFundingType = "Unknown"
                }
            };
        }

        private static CardBinLookupResult Merge(CardBinLookupResult baseline, CardBinProviderDetails providerDetails)
        {
            var fundingType = NormalizeFundingType(providerDetails.FundingType);
            var fundingKnown = fundingType != "Unknown";

            return new CardBinLookupResult
            {
                PaymentInstrument = GetPaymentInstrument(fundingType),
                CardCompany = string.IsNullOrWhiteSpace(providerDetails.Scheme)
                    ? baseline.CardCompany
                    : ToDisplayName(providerDetails.Scheme),
                CardFundingType = fundingType,
                CardFundingTypeKnown = fundingKnown,
                CardProduct = NullIfWhiteSpace(providerDetails.Segment),
                CardIssuer = NullIfWhiteSpace(providerDetails.Issuer),
                CardIssuerCountryCode = NullIfWhiteSpace(providerDetails.CountryCode)?.ToUpperInvariant(),
                CardIssuerCountryName = NullIfWhiteSpace(providerDetails.CountryName)
            };
        }

        private static string GetPaymentInstrument(string fundingType)
            => fundingType switch
            {
                "Credit" => "Credit Card",
                "Debit" => "Debit Card",
                "Prepaid" => "Prepaid Card",
                "Charge Card" => "Charge Card",
                _ => "Card"
            };

        private static string NormalizeFundingType(string? fundingType)
            => fundingType?.Trim().ToUpperInvariant() switch
            {
                "CREDIT" => "Credit",
                "DEBIT" => "Debit",
                "PREPAID" => "Prepaid",
                "CHARGE" or "CHARGE CARD" => "Charge Card",
                _ => "Unknown"
            };

        private static string GetCardCompany(string? rawCardType)
            => rawCardType switch
            {
                "VI" => "Visa",
                "MC" => "Mastercard",
                "AX" or "AE" or "AM" or "AMEX" => "American Express",
                "DS" or "DI" or "DC" => "Discover",
                _ => string.Empty
            };

        private static string ToDisplayName(string value)
            => value.Trim().ToUpperInvariant() switch
            {
                "VISA" => "Visa",
                "MASTERCARD" or "MASTER CARD" => "Mastercard",
                "AMERICAN EXPRESS" or "AMEX" => "American Express",
                _ => value.Trim()
            };

        private static string? NullIfWhiteSpace(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private static void Apply(HelcimTransactionResponse transaction, CardBinLookupResult details)
        {
            transaction.PaymentInstrument = details.PaymentInstrument;
            transaction.CardCompany = details.CardCompany;
            transaction.CardFundingType = details.CardFundingType;
            transaction.CardFundingTypeKnown = details.CardFundingTypeKnown;
            transaction.CardProduct = details.CardProduct;
            transaction.CardIssuer = details.CardIssuer;
            transaction.CardIssuerCountryCode = details.CardIssuerCountryCode;
            transaction.CardIssuerCountryName = details.CardIssuerCountryName;
        }

        private static void Apply(HelcimTransactionResponseDetailed transaction, CardBinLookupResult details)
        {
            transaction.PaymentInstrument = details.PaymentInstrument;
            transaction.CardCompany = details.CardCompany;
            transaction.CardFundingType = details.CardFundingType;
            transaction.CardFundingTypeKnown = details.CardFundingTypeKnown;
            transaction.CardProduct = details.CardProduct;
            transaction.CardIssuer = details.CardIssuer;
            transaction.CardIssuerCountryCode = details.CardIssuerCountryCode;
            transaction.CardIssuerCountryName = details.CardIssuerCountryName;
        }

        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private sealed class CardBinCheckResponse
        {
            public bool Success { get; set; }
            public CardBinCheckData? Data { get; set; }
        }

        private sealed class CardBinCheckData
        {
            public string? Scheme { get; set; }
            public string? Funding { get; set; }
            public string? Segment { get; set; }
            public string? Issuer { get; set; }

            [JsonPropertyName("country_code")]
            public string? CountryCode { get; set; }

            [JsonPropertyName("country_name")]
            public string? CountryName { get; set; }
        }

        private sealed class CardBinProviderDetails
        {
            public string? Scheme { get; init; }
            public string? FundingType { get; init; }
            public string? Segment { get; init; }
            public string? Issuer { get; init; }
            public string? CountryCode { get; init; }
            public string? CountryName { get; init; }

            public static CardBinProviderDetails From(CardBinCheckData data)
                => new()
                {
                    Scheme = data.Scheme,
                    FundingType = data.Funding,
                    Segment = data.Segment,
                    Issuer = data.Issuer,
                    CountryCode = data.CountryCode,
                    CountryName = data.CountryName
                };
        }
    }
}
