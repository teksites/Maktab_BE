using Helcim.Repository;
using Maktab.Attributes;
using MaktabDataContracts.Enums;
using MaktabDataContracts.Responses.Helcim;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Users.Services;

[ApiController]
[Route("api/helcim/saved-cards")]
public sealed class HelcimSavedCardsController : ControllerBase
{
    private readonly IHelcimCardVaultRepository _repository;
    private readonly IDataAccessVerificationService _access;

    public HelcimSavedCardsController(IHelcimCardVaultRepository repository, IDataAccessVerificationService access)
    {
        _repository = repository;
        _access = access;
    }

    [ApiAuthorize]
    [HttpGet]
    public async Task<IReadOnlyList<SavedCardResponse>> GetCards()
    {
        var session = await GetSession();
        return (await _repository.GetActiveCards(session.UserId)).Select(Map).ToList();
    }

    [ApiAuthorize]
    [HttpPut("{cardId:guid}/default")]
    public async Task<IActionResult> SetDefault(Guid cardId)
    {
        try
        {
            var session = await GetSession();
            await _repository.SetDefault(cardId, session.UserId);
            return NoContent();
        }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [ApiAuthorize]
    [HttpDelete("{cardId:guid}")]
    public async Task<IActionResult> Delete(Guid cardId)
    {
        try
        {
            var session = await GetSession();
            await _repository.Deactivate(cardId, session.UserId);
            return NoContent();
        }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    private async Task<SessionAccessContext> GetSession()
    {
        if (!Request.Headers.TryGetValue("Session_Info", out var value) || !Guid.TryParse(value, out var sessionId))
            throw new UnauthorizedAccessException("Session header not found or invalid.");
        return await _access.GetSessionAccessContext(sessionId)
            ?? throw new UnauthorizedAccessException("No active session found.");
    }

    private static SavedCardResponse Map(HelcimSavedCardRecord card) => new()
    {
        CardId = card.CardId, CardCompany = card.CardCompany, CardFundingType = card.CardFundingType,
        LastFourDigits = card.LastFourDigits, CardHolderName = card.CardHolderName,
        ExpiryMonth = card.ExpiryMonth, ExpiryYear = card.ExpiryYear, IsDefault = card.IsDefault,
        CreatedAt = card.CreatedAt
    };
}
