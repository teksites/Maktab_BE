using MaktabDataContracts.Enums;
using System.Net.Sockets;

namespace Application.Users.Contracts
{
    public class AddClientCard
    {
        public Guid UserId { get; set; }
        public string CardHolderName { get; set; } = string.Empty;
        public CardType CardProvider { get; set; }
        public string CvcCode { get; set; }
        public DateTime ExpiryDate { get; set; }
        public string CardNumber { get; set; }
        public bool IsDefault { get; set; }
    }
}