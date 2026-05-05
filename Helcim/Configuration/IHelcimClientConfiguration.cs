using InternalContracts;

namespace Helcim.Configuration
{
    public interface IHelcimClientConfiguration : IClientConfiguration
    {
        string ApiToken { get; init; }
    }
}
