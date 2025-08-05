using MaktabDataContracts.Enums;

namespace Application.Users.Contracts
{
    public class UserInformation
    {
        public Guid UserId { get; set; }
        public Guid FamilyId { get; set; } = Guid.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public Relationship Relationship { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedOn { get; set; }
        public bool IsAdmin { get; set; } = false;
        public bool IsTempPassword { get; set; } = false;
    }
}
