namespace Users.Contracts
{
    public class AddExtendedUserInformationInternal
    {
        public Guid UserId { get; set; }
        public Guid FamilyId { get; set; }
        public Guid AddressId { get; set; }
        public string SIN { get; set; } = string.Empty;
    }
}
