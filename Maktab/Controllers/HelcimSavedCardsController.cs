using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Helcim;
using Helcim.Repository;
using Helcim.Services;
using Maktab.Attributes;
using MaktabDataContracts.Requests.Helcim;
using MaktabDataContracts.Responses.Helcim;
using Microsoft.AspNetCore.Mvc;
using Users.Services;

[ApiController]
[Route("api/helcim/saved-cards")]
public sealed class HelcimSavedCardsController : ControllerBase
{
    private readonly IHelcimCardVaultRepository _cards;
    private readonly IHelcimTransactionService _transactions;
    private readonly IDataAccessVerificationService _access;

    public HelcimSavedCardsController(
        IHelcimCardVaultRepository cards,
        IHelcimTransactionService transactions,
        IDataAccessVerificationService access)
        => (_cards, _transactions, _access) = (cards, transactions, access);

    [ApiAuthorize]
    [HttpGet]
    public async Task<IReadOnlyList<SavedCardResponse>> Get()
    {
        var session = await GetSession();
        return (await _cards.GetActiveCards(session.UserId)).Select(card => new SavedCardResponse
        {
            CardId = card.CardId, CardCompany = card.CardCompany, CardFundingType = card.CardFundingType,
            LastFourDigits = card.LastFourDigits, CardHolderName = card.CardHolderName,
            IsDefault = card.IsDefault, CreatedAt = card.CreatedAt
        }).ToList();
    }

    [ApiAuthorize]
    [HttpPut("{cardId:guid}/default")]
    public async Task<IActionResult> SetDefault(Guid cardId)
    {
        try { await _cards.SetDefault(cardId, (await GetSession()).UserId); return NoContent(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [ApiAuthorize]
    [HttpDelete("{cardId:guid}")]
    public async Task<IActionResult> Delete(Guid cardId)
    {
        try { await _cards.Deactivate(cardId, (await GetSession()).UserId); return NoContent(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [ApiAuthorize]
    [HttpPost("charge")]
    public async Task<ActionResult<SavedCardPaymentAttemptResponse>> Charge(ChargeSavedCardRequest request)
    {
        try
        {
            var session = await GetSession();
            return Ok(await _transactions.ChargeSavedCard(request, session.UserId, session.FamilyId));
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new { error = exception.Message, code = "saved_card_or_transaction_not_found" });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message, code = "invalid_saved_card_payment_request" });
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { error = exception.Message, code = "saved_card_payment_not_allowed" });
        }
        catch (HelcimRequestException exception)
        {
            return StatusCode(exception.IsUpstreamFailure ? 502 : 400, new
            {
                error = exception.Message,
                code = exception.IsUpstreamFailure ? "helcim_unavailable" : "helcim_payment_rejected"
            });
        }
    }

    private async Task<SessionAccessContext> GetSession()
    {
        if (!Request.Headers.TryGetValue("Session_Info", out var value) || !Guid.TryParse(value, out var sessionId))
            throw new UnauthorizedAccessException("Session header not found or invalid.");
        return await _access.GetSessionAccessContext(sessionId)
            ?? throw new UnauthorizedAccessException("No active session found.");
    }
}
