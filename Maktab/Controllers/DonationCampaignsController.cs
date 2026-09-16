using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Courses.Services;
using Helcim.Repository;
using Maktab.Attributes;
using MaktabDataContracts.Enums;
using MaktabDataContracts.Requests.DonationCampaign;
using MaktabDataContracts.Responses.DonationCampaign;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Users.Services;

[ApiController]
[Route("api/donation-campaigns")]
[EnableCors("corspolicy")]
public sealed class DonationCampaignsController : ControllerBase
{
    private const UserRoleType CampaignAdminRoles = UserRoleType.Admin | UserRoleType.SuperUser | UserRoleType.SchoolAdmin;
    private readonly IDonationCampaignRepository _campaigns;
    private readonly IDataAccessVerificationService _access;
    private readonly IInstituteService _institutes;

    public DonationCampaignsController(
        IDonationCampaignRepository campaigns,
        IDataAccessVerificationService access,
        IInstituteService institutes)
        => (_campaigns, _access, _institutes) = (campaigns, access, institutes);

    // Public catalog: only active campaigns inside their configured date window are exposed.
    [HttpGet]
    public Task<IReadOnlyList<DonationCampaignResponse>> GetVisible([FromQuery] Guid? mosqueId = null)
        => _campaigns.GetVisible(mosqueId, DateTime.UtcNow.Date);

    [HttpGet("types")]
    public Task<IReadOnlyList<DonationCampaignTypeResponse>> GetTypes()
        => _campaigns.GetActiveTypes();

    [ApiAuthorize]
    [HttpGet("payments/mine")]
    public async Task<IReadOnlyList<DonationPaymentResponse>> GetMyPayments(
        [FromQuery] Guid? campaignId = null,
        [FromQuery] byte? campaignTypeId = null,
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null)
    {
        var session = await GetSession();
        return await _campaigns.GetPayments(new DonationPaymentQuery
        {
            UserId = session.UserId,
            CampaignId = campaignId,
            CampaignTypeId = campaignTypeId,
            FromUtc = fromUtc,
            ToUtc = toUtc
        });
    }

    [ApiAuthorize(false, false, CampaignAdminRoles)]
    [HttpGet("admin")]
    public Task<IReadOnlyList<DonationCampaignResponse>> GetAll([FromQuery] Guid? mosqueId = null)
        => _campaigns.GetAll(mosqueId);

    [ApiAuthorize(false, false, CampaignAdminRoles)]
    [HttpGet("admin/{campaignId:guid}")]
    public async Task<ActionResult<DonationCampaignResponse>> GetAdmin(Guid campaignId)
        => await _campaigns.Get(campaignId) is { } campaign ? Ok(campaign) : NotFound();

    [ApiAuthorize(false, false, CampaignAdminRoles)]
    [HttpGet("admin/payments")]
    public Task<IReadOnlyList<DonationPaymentResponse>> GetPayments(
        [FromQuery] Guid? campaignId = null,
        [FromQuery] byte? campaignTypeId = null,
        [FromQuery] Guid? mosqueId = null,
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null)
        => _campaigns.GetPayments(new DonationPaymentQuery
        {
            CampaignId = campaignId,
            CampaignTypeId = campaignTypeId,
            MosqueId = mosqueId,
            FromUtc = fromUtc,
            ToUtc = toUtc
        });

    [ApiAuthorize(false, false, CampaignAdminRoles)]
    [HttpPost("admin")]
    public async Task<ActionResult<DonationCampaignResponse>> Add([FromBody] UpsertDonationCampaignRequest request)
    {
        try
        {
            await Validate(request);
            var campaign = await _campaigns.Save(Guid.NewGuid(), request);
            return CreatedAtAction(nameof(GetVisible), new { mosqueId = campaign.MosqueId }, campaign);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message, code = "invalid_donation_campaign" });
        }
    }

    [ApiAuthorize(false, false, CampaignAdminRoles)]
    [HttpPut("admin/{campaignId:guid}")]
    public async Task<ActionResult<DonationCampaignResponse>> Update(Guid campaignId, [FromBody] UpsertDonationCampaignRequest request)
    {
        if (await _campaigns.Get(campaignId) == null) return NotFound();
        try
        {
            await Validate(request);
            return Ok(await _campaigns.Save(campaignId, request));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message, code = "invalid_donation_campaign" });
        }
    }

    [ApiAuthorize(false, false, CampaignAdminRoles)]
    [HttpDelete("admin/{campaignId:guid}")]
    public async Task<IActionResult> Delete(Guid campaignId)
        => await _campaigns.Deactivate(campaignId) ? NoContent() : NotFound();

    private async Task Validate(UpsertDonationCampaignRequest request)
    {
        if (request.MosqueId == Guid.Empty) throw new ArgumentException("MosqueId is required.");
        if (string.IsNullOrWhiteSpace(request.Name)) throw new ArgumentException("Campaign name is required.");
        if (request.Name.Length > 255 || request.ShortDescription.Length > 1000) throw new ArgumentException("Campaign text exceeds the supported length.");
        if (request.StartDate.HasValue && request.EndDate.HasValue && request.EndDate.Value.Date < request.StartDate.Value.Date)
            throw new ArgumentException("EndDate cannot be before StartDate.");
        if (!string.Equals(request.Currency, "CAD", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Only CAD campaigns are currently supported.");
        if (request.PresetAmounts.Count > 8 || request.PresetAmounts.Any(amount => amount <= 0) || request.PresetAmounts.Distinct().Count() != request.PresetAmounts.Count)
            throw new ArgumentException("PresetAmounts must contain at most eight distinct positive amounts.");
        if (!request.AllowCustomAmount && request.PresetAmounts.Count == 0)
            throw new ArgumentException("At least one preset amount is required when custom amounts are disabled.");

        var mosque = await _institutes.GetInstitute(request.MosqueId);
        if (mosque?.InstituteType != InstituteType.Mosque) throw new ArgumentException("MosqueId does not identify an active mosque.");
        if (!(await _campaigns.GetActiveTypes()).Any(type => type.CampaignTypeId == request.CampaignTypeId))
            throw new ArgumentException("CampaignTypeId is invalid or inactive.");
    }

    private async Task<SessionAccessContext> GetSession()
    {
        if (!Request.Headers.TryGetValue("Session_Info", out var value) || !Guid.TryParse(value, out var sessionId))
            throw new UnauthorizedAccessException("Session header not found or invalid.");
        return await _access.GetSessionAccessContext(sessionId)
            ?? throw new UnauthorizedAccessException("No active session found.");
    }
}
