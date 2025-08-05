namespace Application.Users.Contracts
{
    public class AddAddress2
    {
        public Guid ConnectedId { get; set; }
        public string UnitNo { get; set; }
        public string ApartmentNo { get; set; }
        public string AddressLine1 {get;set; }
        public string AddressLine2 { get;set;}
        public string PostalCode { get; set; }
        public string City { get; set; }
        public string Province { get; set; }
        public string Country { get; set; }
    }
}