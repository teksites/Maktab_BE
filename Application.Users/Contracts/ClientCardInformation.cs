using MaktabDataContracts.Enums;
using System.Net.Sockets;

namespace Application.Users.Contracts
{
    public class ClientCardInformation
    {
        public Guid CardId { get; set; }
        public Guid UserId { get; set; }
        public string CardHolderName { get; set; } = string.Empty;
        public CardType CardProvider { get; set; }
        public string? CvcCode { get; set; }
        public string CardNumber { get; set; }
        public DateTime ExpiryDate { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedOn { get; set; }
        public bool isDefault { get; set; }
        public string? SenderBankCardId { get;set; }

        public ClientCardInformation()
        {

        }
    }
}