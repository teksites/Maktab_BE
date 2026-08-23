namespace Application.Users.Contracts
{
    public class UpdateUserProfileInformation
    {
        public Guid UserId { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public bool IsMultiFactorLoginEnabled { get; set; }
    }
}
