namespace Application.Users.Contracts
{
    public class UserVerification
    {
        public Guid UserId { get; set; }
        public string EmailVerificationCode { get; set; }
        public string? PhoneVerificationCode { get; set; }
    }
}