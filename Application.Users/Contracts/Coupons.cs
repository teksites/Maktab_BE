namespace Application.Users.Contracts
{
    public class Coupons1
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public float Amount { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool UpdatedOn { get; set; }
    }
}