using Application.Users.Contracts;
using MaktabDataContracts.Requests.Cards;
using MaktabDataContracts.Responses.Cards;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Users.Contracts;

namespace Users.Services
{
    public interface IUserCardsService
    {
        Task<ClientCardResponse> AddClientCard(AddClientCard clientCardInformation);
        Task<ClientCardResponse> UpdateClientCard(UpdateClientCardInformation clientCardInformation);
        Task<bool> DeleteClientCard(Guid clientCardId, bool ifHardDelete);
        Task<ClientCardResponse> GetClientCard(Guid cardId);
        Task<ClientCardResponse> GetDetailedClientCard(Guid cardId);
        Task<ClientCardResponse> GetClientByNumber(string cardNumber);
        Task<IEnumerable<ClientCardResponse>> GetUserClientCards(Guid userId);
        Task<bool> CheckIfCardExisit(ClientCardVerification card);
        Task<bool> DeleteUserCards(Guid userId, bool ifHardDelete);
        Task<ClientCardResponse> SetSenderBankCardID(string cardNumber, Guid userId, string  senderBankCardId);
    }
}