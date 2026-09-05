using System.Net;
using System.Text;
using Helcim.Configuration;
using Helcim.Implementation.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace Courses.Test;

public class CardBinLookupServiceTests
{
    [Fact]
    public async Task GetDisplayDetailsAsync_WhenProviderReturnsVisaDebit_UsesFundingTypeAndCachesTheLookup()
    {
        var handler = new StubHttpMessageHandler(_ => JsonResponse("""
            {
              "success": true,
              "data": {
                "bin": "450644",
                "scheme": "Visa",
                "funding": "Debit",
                "segment": "Classic",
                "issuer": "Example Bank",
                "country_code": "CA",
                "country_name": "Canada"
              }
            }
            """));

        using var cache = new MemoryCache(new MemoryCacheOptions());
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://cardbincheck.example") };
        var service = CreateService(client, cache);

        var first = await service.GetDisplayDetailsAsync("VI", "4506445439");
        var second = await service.GetDisplayDetailsAsync("VI", "4506440000");

        Assert.Equal("Debit Card", first.PaymentInstrument);
        Assert.Equal("Visa", first.CardCompany);
        Assert.Equal("Debit", first.CardFundingType);
        Assert.True(first.CardFundingTypeKnown);
        Assert.Equal("Classic", first.CardProduct);
        Assert.Equal("Example Bank", first.CardIssuer);
        Assert.Equal("CA", first.CardIssuerCountryCode);
        Assert.Equal("Canada", first.CardIssuerCountryName);
        Assert.Equal(first.CardFundingType, second.CardFundingType);
        Assert.Equal(1, handler.RequestCount);
        Assert.Equal("/api/bin/450644", handler.LastRequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task GetDisplayDetailsAsync_WhenHelcimIdentifiesInterac_DoesNotCallProvider()
    {
        var handler = new StubHttpMessageHandler(_ => throw new InvalidOperationException("Provider must not be called for Interac."));

        using var cache = new MemoryCache(new MemoryCacheOptions());
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://cardbincheck.example") };
        var service = CreateService(client, cache);

        var result = await service.GetDisplayDetailsAsync("DB", "1234567890");

        Assert.Equal("Interac Debit", result.PaymentInstrument);
        Assert.Equal("Interac", result.CardCompany);
        Assert.Equal("Debit", result.CardFundingType);
        Assert.True(result.CardFundingTypeKnown);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task GetDisplayDetailsAsync_WhenProviderFails_ReturnsSafeBrandOnlyFallback()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests));

        using var cache = new MemoryCache(new MemoryCacheOptions());
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://cardbincheck.example") };
        var service = CreateService(client, cache);

        var result = await service.GetDisplayDetailsAsync("VI", "4506445439");

        Assert.Equal("Card", result.PaymentInstrument);
        Assert.Equal("Visa", result.CardCompany);
        Assert.Equal("Unknown", result.CardFundingType);
        Assert.False(result.CardFundingTypeKnown);
    }

    private static CardBinLookupService CreateService(HttpClient client, IMemoryCache cache)
        => new(
            client,
            cache,
            new TestCardBinCheckConfiguration(),
            NullLogger<CardBinLookupService>.Instance);

    private static HttpResponseMessage JsonResponse(string content)
        => new(HttpStatusCode.OK)
        {
            Content = new StringContent(content, Encoding.UTF8, "application/json")
        };

    private sealed class TestCardBinCheckConfiguration : ICardBinCheckConfiguration
    {
        public bool Enabled => true;
        public string BaseUrl => "https://cardbincheck.example";
        public string ApiKey => string.Empty;
        public string ApiKeyHeaderName => "X-Api-Key";
        public TimeSpan RequestTimeout => TimeSpan.FromSeconds(5);
        public TimeSpan CacheDuration => TimeSpan.FromHours(24);
        public int MaximumLookupsPerResponse => 10;
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        public int RequestCount { get; private set; }
        public Uri? LastRequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            LastRequestUri = request.RequestUri;
            return Task.FromResult(_responseFactory(request));
        }
    }
}
