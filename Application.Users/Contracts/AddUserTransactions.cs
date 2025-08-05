using Users.Contracts;

namespace Application.Users.Contracts
{
    public class AddUserTransactions1
    {
        public Guid UserId { get; set; }
        public Guid CardId { get; set; }
        public Guid RecepientId { get; set; }
        public float TransferredAmount { get; set; }
        public float TransferredFee { get; set; }
        public Guid CouponId { get; set; }
        public float ReceivededAmount { get; set; }
        public Guid ExchangeRateID { get; set; }
    }
}