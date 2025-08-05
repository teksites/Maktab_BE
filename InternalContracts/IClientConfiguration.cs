
namespace InternalContracts
{
    public interface IClientConfiguration
    {
        string BaseUrl { get; init; }
        HttpClientType ClientType { get; init; }
        string RelativeUrl { get; init; }
        TimeSpan RequestTimeout { get; init; }
        int RetryAttempts { get; init; }
    }
}