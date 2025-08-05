namespace InternalContracts
{
    public class AddSession
    {
        public Guid SessionId { get; set; }
        public Guid UserId { get; set; }
        public Guid FamilyId { get; set; }        
        public string Token { get; set; } = string.Empty;
        public DateTime TokenExpiry { get; set; }
        public string? IpAddress { get; set; }
        public bool IsActive { get; set; }
        public DateTime LogInTime { get; set; }
        public DateTime? LogOutTime { get; set; }
    }
}
