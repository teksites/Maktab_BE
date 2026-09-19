using MaktabDataContracts.Requests.DonationCampaign;
using MaktabDataContracts.Responses.DonationCampaign;

namespace Helcim.Repository;

public sealed class DonationPaymentQuery
{
    public Guid? CampaignId { get; init; }
    public byte? CampaignTypeId { get; init; }
    public Guid? MosqueId { get; init; }
    public Guid? UserId { get; init; }
    public DateTime? FromUtc { get; init; }
    public DateTime? ToUtc { get; init; }
}

public interface IDonationCampaignRepository
{
    Task<IReadOnlyList<DonationCampaignTypeResponse>> GetActiveTypes();
    Task<IReadOnlyList<DonationCampaignResponse>> GetVisible(Guid? mosqueId, DateTime utcToday);
    Task<IReadOnlyList<DonationCampaignResponse>> GetAll(Guid? mosqueId = null);
    Task<DonationCampaignResponse?> Get(Guid campaignId);
    Task<DonationCampaignResponse> Save(Guid campaignId, UpsertDonationCampaignRequest request);
    Task<bool> Deactivate(Guid campaignId);
    Task<IReadOnlyList<DonationPaymentResponse>> GetPayments(DonationPaymentQuery query);
}
