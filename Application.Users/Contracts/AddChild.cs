namespace Application.Users.Contracts
{
    public class AddChild
    {
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
    }
}