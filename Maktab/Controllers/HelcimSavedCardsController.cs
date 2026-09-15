using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Helcim.Repository;
using Maktab.Attributes;
using MaktabDataContracts.Responses.Helcim;
using Microsoft.AspNetCore.Mvc;
using Users.Services;

[ApiController]
[Route("api/helcim/saved-cards")]
public sealed class HelcimSavedCardsController : ControllerBase
{
    private readonly IHelcimCardVaultRepository _cards;
    private readonly IDataAccessVerificationService _access;

    public HelcimSavedCardsController(IHelcimCardVaultRepository cards, IDataAccessVerificationService access)
        => (_cards, _access) = (cards, access);

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

    private async Task<SessionAccessContext> GetSession()
    {
        if (!Request.Headers.TryGetValue("Session_Info", out var value) || !Guid.TryParse(value, out var sessionId))
            throw new UnauthorizedAccessException("Session header not found or invalid.");
        return await _access.GetSessionAccessContext(sessionId)
            ?? throw new UnauthorizedAccessException("No active session found.");
    }
}
