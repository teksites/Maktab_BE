using Application.Users.Contracts;
using MaktabDataContracts.Requests.Cards;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Users.Contracts;

namespace Users.Repository
{
    public interface IUserCardsRepository
    {
        Task<ClientCardInformation> AddClientCard(ClientCardInformation clientCardInformation);
        Task<ClientCardInformation> UpdateClientCard(UpdateClientCardInformation clientCardInformation);
        Task<bool> DeleteClientCard(Guid clientCardId, bool ifHardDelete = false);
        Task<bool> DeleteUserCards(Guid userId, bool ifHardDelete = false);
        Task<ClientCardInformation> GetClientCard(Guid cardId);
        Task<ClientCardInformation> GetClientCardByNumber(string cardNumber);
        Task<IEnumerable<ClientCardInformation>> GetUserClientCards(Guid userId);
        Task<bool> CheckIfCardExisit(ClientCardVerification clientCard);
        Task<ClientCardInformation> SetSenderBankCardID(string cardNumber, Guid userId, string senderBankCardId);

    }
}
