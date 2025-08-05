using Application.Users.Contracts;
using Microsoft.Extensions.Configuration;
using MaktabDataContracts.Requests.Cards;
using MaktabDataContracts.Responses.Cards;
using Users.Contracts;
using Users.Repository;
using Users.Services;

namespace Application.Users.Implementation
{
    public class UserCardsService : IUserCardsService
    {
        private readonly IConfiguration _configuration;
        private readonly IUserCardsRepository _repository;

        public UserCardsService(IConfiguration configuration, IUserCardsRepository repository) 
        {
            _configuration = configuration;
            _repository = repository;
        
        }

        public async Task<ClientCardResponse> AddClientCard(AddClientCard clientCardInformation)
        {
           return MaptToClientCardResonse( await _repository.AddClientCard(MapToClientCardInformation(clientCardInformation)).ConfigureAwait(false));
        }

        private ClientCardResponse MaptToClientCardResonse(ClientCardInformation clientCardInformation)
        {
            if (clientCardInformation == null)
            {
                return null;
            }

            return new ClientCardResponse
            {
                CardHolderName = clientCardInformation.CardHolderName,
                CardId = clientCardInformation.CardId,
                CardNumber = clientCardInformation.CardNumber.Remove(0, 12).Insert(0, "XXXXXXXXXXXX"),
                UserId  = clientCardInformation.UserId,
                CardProvider = clientCardInformation.CardProvider,
                ExpiryDate = clientCardInformation.ExpiryDate,
                IsActive = clientCardInformation.IsActive,
                CreatedAt = clientCardInformation.CreatedAt,
                UpdatedOn = clientCardInformation.UpdatedOn,
                IsDefault = clientCardInformation.isDefault,
                SenderBankCardId = clientCardInformation.SenderBankCardId
            };
        }

        private ClientCardResponse MaptToClientCardDetailedResonse(ClientCardInformation clientCardInformation)
        {
            if (clientCardInformation == null)
            {
                return null;
            }

            return new ClientCardResponse
            {
                CardHolderName = clientCardInformation.CardHolderName,
                CardId = clientCardInformation.CardId,
                CardNumber = clientCardInformation.CardNumber,
                CvcCode = clientCardInformation.CvcCode,
                UserId = clientCardInformation.UserId,
                CardProvider = clientCardInformation.CardProvider,
                ExpiryDate = clientCardInformation.ExpiryDate,
                IsActive = clientCardInformation.IsActive,
                CreatedAt = clientCardInformation.CreatedAt,
                UpdatedOn = clientCardInformation.UpdatedOn,
                IsDefault = clientCardInformation.isDefault,
                SenderBankCardId = clientCardInformation.SenderBankCardId
            };
        }

        private ClientCardInformation MapToClientCardInformation(AddClientCard clientCardInformation, String senderBankCardId = null)
        {
            return new ClientCardInformation
            {
                CardId = Guid.NewGuid(),
                CardNumber = clientCardInformation.CardNumber,
                UserId = clientCardInformation.UserId,
                CardProvider = clientCardInformation.CardProvider,
                CardHolderName= clientCardInformation.CardHolderName,
                CreatedAt = DateTime.Now,
                UpdatedOn = DateTime.Now,
                CvcCode = clientCardInformation.CvcCode,
                ExpiryDate= clientCardInformation.ExpiryDate,
                IsActive = true,
                isDefault = clientCardInformation.IsDefault,
                SenderBankCardId = senderBankCardId
            };
        }

        public async Task<bool> CheckIfCardExisit(ClientCardVerification clientCardInformation)
        {
            return await _repository.CheckIfCardExisit(clientCardInformation).ConfigureAwait(false);
        }

   
        public async Task<bool> DeleteClientCard(Guid clientCardId, bool ifHardDelete)
        {
            return await _repository.DeleteClientCard(clientCardId, ifHardDelete).ConfigureAwait(false);
        }

        public async Task<bool> DeleteUserCards(Guid userId, bool ifHardDelete)
        {
            return await _repository.DeleteUserCards(userId, ifHardDelete).ConfigureAwait(false);
        }

        public async Task<ClientCardResponse> GetClientCard(Guid cardId)
        {
            return MaptToClientCardResonse(await _repository.GetClientCard(cardId).ConfigureAwait(false));
        }

        public async Task<ClientCardResponse> GetDetailedClientCard(Guid cardId)
        {
            return MaptToClientCardDetailedResonse(await _repository.GetClientCard(cardId).ConfigureAwait(false));
        }
        
        public async Task<IEnumerable<ClientCardResponse>> GetUserClientCards(Guid userId)
        {
            return (await _repository.GetUserClientCards(userId).ConfigureAwait(false)).
                Select(MaptToClientCardResonse).ToList();
        }

        Task<ClientCardResponse> IUserCardsService.UpdateClientCard(UpdateClientCardInformation clientCardInformation)
        {
            throw new NotImplementedException();
        }

        public async Task<ClientCardResponse> GetClientByNumber(string cardNumber)
        {
            return MaptToClientCardResonse(await _repository.GetClientCardByNumber(cardNumber).ConfigureAwait(false));
        }

        public async Task<ClientCardResponse> SetSenderBankCardID(string cardNumber, Guid userId, string senderBankCardId)
        {
            return MaptToClientCardResonse(await _repository.SetSenderBankCardID(cardNumber, userId, senderBankCardId).ConfigureAwait(false));
        }
    }
}
