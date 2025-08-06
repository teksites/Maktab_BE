using MaktabDataContracts.Enums;

namespace Application.Users.Contracts
{
    public class OtherContactInformation
    {
        public Guid ContactId { get; set; }
        public Guid FamilyId { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public ContactType ContactType { get; set; }
        public Relationship Relationship { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedOn { get; set; }
    }
}