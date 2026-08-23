using MaktabDataContracts.Enums;

namespace Application.Users.Contracts
{
    public class AdminUpdateUserInformation
    {
        public Guid UserId { get; set; }
        public Guid FamilyId { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string? NewPassword { get; set; }
        public Relationship Relationship { get; set; }
        public bool IsActive { get; set; }
        public bool IsMultiFactorLoginEnabled { get; set; }
        public bool IsAdmin { get; set; }
        public bool IsTempPassword { get; set; }
        public UserRoleType UserRole { get; set; }
    }
}
