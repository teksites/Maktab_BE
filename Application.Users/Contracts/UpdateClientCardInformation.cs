using MaktabDataContracts.Enums;
using System.Net.Sockets;

namespace Application.Users.Contracts
{
    public class UpdateClientCardInformation1
    {
        public Guid CardId { get; set; }
        public string CardHolderName { get; set; } = string.Empty;
        public CardType CardProvider { get; set; }
        public string CvcCode { get; set; }
        public DateTime ExpiryDate { get; set; }
    //    public Address BillingAddress {  get; set; } 

    }
}