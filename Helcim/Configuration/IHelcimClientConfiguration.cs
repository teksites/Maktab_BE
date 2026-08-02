using InternalContracts;

namespace Helcim.Configuration
{
    public interface IHelcimClientConfiguration : IClientConfiguration
    {
        string ApiVersionPath { get; init; }
        string ApiToken { get; init; }
        string SignatureVerificationToken { get; init; }
        bool ReconciliationEnabled { get; init; }
        string ReconciliationCronSchedule { get; init; }
        int ReconciliationLookbackDays { get; init; }
        int AchReconciliationPageSize { get; init; }
        int CardReconciliationPageSize { get; init; }
    }
}
