namespace InternalContracts
{
    public class SessionTwoFactorCode
    {
        public Guid SessionTwoFactorCodeId { get; set; }
        public Guid SessionId { get; set; }
        public Guid UserId { get; set; }
        public string Email { get; set; } = string.Empty;
        public string VerificationCodeHash { get; set; } = string.Empty;
        public DateTime ExpiresOn { get; set; }
        public int AttemptCount { get; set; }
        public DateTime? LastSentOn { get; set; }
        public bool IsVerified { get; set; }
        public DateTime? VerifiedOn { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedOn { get; set; }
    }
}
