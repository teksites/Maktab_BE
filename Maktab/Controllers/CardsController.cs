using Application.Users.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Maktab.Attributes;
using MaktabDataContracts.Requests.Cards;
using MaktabDataContracts.Responses.Cards;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Users.Services;

namespace Maktab.Controllers
{

    [Route("api")]
    [ApiController]
    [ApiAuthorize()]

    public class CardsController : ControllerBase
    {
        private readonly IUserCardsService _cardsService;

        private readonly ILogger<CardsController> _logger;

        public CardsController(IUserCardsService cardsService, ILogger<CardsController> logger)
        {
            _cardsService = cardsService;
            _logger = logger;
        }

        [Authorize]
        [HttpGet("cards/{cardId:guid}")]
        [EnableCors("corspolicy")]
        public async Task<ClientCardResponse> GetCard(Guid cardId)
        {
            return await _cardsService.GetClientCard(cardId).ConfigureAwait(false);
        }
        
        [Authorize]
        [HttpGet("users/{id:guid}/cards")]
        [EnableCors("corspolicy")]
        public async Task<IEnumerable<ClientCardResponse>> GetUserCards(Guid id)
        {
            return await _cardsService.GetUserClientCards(id).ConfigureAwait(false);
        }

        [Authorize]
        [HttpPost("user/{userId:guid}/cards/add")]
        [EnableCors("corspolicy")]
        public async Task<ClientCardResponse> AddUserAddress(Guid userId, AddClientCardRequest card)
        {
            return await _cardsService.AddClientCard(new AddClientCard
            {
                CardHolderName = card.CardHolderName,
                 CardNumber = card.CardNumber,
                 UserId = userId,
                 CardProvider = card.CardProvider,
                 CvcCode = card.CvcCode,
                 ExpiryDate = card.ExpiryDate,
                 IsDefault = card.IsDefault

            }).ConfigureAwait(false);
        }

        [Authorize]
        [HttpPost("cards/{cardId:guid}/delete")]
        [EnableCors("corspolicy")]
        public async Task<bool> DeleteAddress(Guid cardId, bool ifHardDelete = false)
        {
            return await _cardsService.DeleteClientCard(cardId, ifHardDelete).ConfigureAwait(false);
        }

        [Authorize]
        [HttpPost("users/{userId:guid}/cards/delete")]
        [EnableCors("corspolicy")]
        public async Task<bool> DeleteAdressByConnectId(Guid userId, bool ifHardDelete = false)
        {
            return await _cardsService.DeleteUserCards(userId, ifHardDelete).ConfigureAwait(false);
        }

        [Authorize]
        [HttpPost("cards/check")]
        [EnableCors("corspolicy")]
        public async Task<bool> CheckIfUserCardExisit(ClientCardVerification clientCard)
        {
            return await _cardsService.CheckIfCardExisit(clientCard).ConfigureAwait(false);
        }
    }
}
