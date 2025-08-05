using Users.Contracts;

namespace Application.Users.Contracts
{
    public class AddressResponse1
    {
        public Guid AddressId { get; set; }
        public Guid ConnectedId { get; set; }
        public string UnitNo { get; set; }
        public string ApartmentNo { get; set; }
        public string AddressLine1 {get;set; }
        public string AddressLine2 { get;set;}
        public string PostalCode { get; set; }
        public string City { get; set; }
        public string Province { get; set; }

        public string Country { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedOn { get; set;}
    }
}