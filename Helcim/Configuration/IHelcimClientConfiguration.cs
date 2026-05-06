using InternalContracts;

namespace Helcim.Configuration
{
    public interface IHelcimClientConfiguration : IClientConfiguration
    {
        string ApiVersionPath { get; init; }
        string ApiToken { get; init; }
        string SignatureVerificationToken { get; init; }
    }
}
