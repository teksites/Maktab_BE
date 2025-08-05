using MaktabDataContracts.Enums;
using System.Net.Sockets;

namespace Application.Users.Contracts
{
    public class ExtendedUserInformationDetail
    {
        public Guid UserId { get; set; }
        public string IdNumber { get; set; }
        public DateTime DateOfBirth { get; set; }
        public string Occupation { get; set; }
        public string BusinesName { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedOn { get; set; }
    }
}