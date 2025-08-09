namespace Application.Users.Contracts
{
    public class ExtendedUserInformationDetail
    {
        public Guid UserId { get; set; }
        public Guid FamilyId { get; set; }
        public Guid AddressId { get; set; }
        public string SIN { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedOn { get; set; }
        public bool IsActiveTaxCreditRecipient { get; set; } = false;
    }
}