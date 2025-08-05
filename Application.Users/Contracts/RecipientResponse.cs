namespace Application.Users.Contracts
{
    public class ChildResponse1
    {
        public Guid RecepientId { get; set; }
        public Guid UserId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; }
        public string IBAN { get; set; } = string.Empty;
        public string AccountNmber { get; set; } = string.Empty;
        public string AccountTitle { get; set; } = string.Empty;
        public string BranchCode { get; set; } = string.Empty;
        public string BankCode { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedOn { get; set; }

    }
}