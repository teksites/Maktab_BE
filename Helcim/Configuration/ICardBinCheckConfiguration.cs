namespace Helcim.Configuration
{
    public interface ICardBinCheckConfiguration
    {
        bool Enabled { get; }
        string BaseUrl { get; }
        string ApiKey { get; }
        string ApiKeyHeaderName { get; }
        TimeSpan RequestTimeout { get; }
        TimeSpan CacheDuration { get; }
        int MaximumLookupsPerResponse { get; }
    }
}
