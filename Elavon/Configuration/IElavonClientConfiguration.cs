using InternalContracts;

namespace Elavon.Configuration
{
    public interface IElavonClientConfiguration : IClientConfiguration
    {
        string MerchantId { get; init; }
        string Pin { get; init; }
        bool TestMode { get; init; }
        string UserId { get; init; }
    }
}