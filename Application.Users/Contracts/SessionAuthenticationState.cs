namespace Application.Users.Contracts
{
    public class SessionAuthenticationState
    {
        public Guid SessionId { get; set; }
        public Guid UserId { get; set; }
        public Guid FamilyId { get; set; }
        public bool IsActive { get; set; }
        public bool RequiresTwoFactorVerification { get; set; }
        public bool IsTwoFactorVerified { get; set; }
        public DateTime? TwoFactorVerifiedOn { get; set; }
    }
}
