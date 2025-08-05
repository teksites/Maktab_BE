using MaktabDataContracts.Enums;

namespace Application.Users.Contracts
{
    public class Child
    {
        public Guid ChildId { get; set; }
        public Guid FamilyId { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public DateTime DateOfBirth { get; set; } = DateTime.MinValue;
        public Gender Gender { get; set; }
        public string RAMQNumber { get; set; } = string.Empty;
        public DateTime RAMQExpiry { get; set; } = DateTime.MinValue;
        public int RAMQSequenceNumber { get; set; } = 1;
        public string Allergies { get; set; } = string.Empty;
        public string OtherHealthConditions { get; set; } = string.Empty;
        public bool WillUseDayCareServices { get; set; } = false;
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedOn { get; set; }
    }
}