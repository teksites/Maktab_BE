using MaktabDataContracts.Responses.Helcim;

namespace Helcim.Services
{
    public interface ICardBinLookupService
    {
        Task<CardBinLookupResult> GetDisplayDetailsAsync(
            string? rawCardType,
            string? maskedCardNumber,
            CancellationToken cancellationToken = default);

        Task EnrichAsync(
            IList<HelcimTransactionResponse> transactions,
            CancellationToken cancellationToken = default);

        Task EnrichDetailedAsync(
            IList<HelcimTransactionResponseDetailed> transactions,
            CancellationToken cancellationToken = default);
    }
}
