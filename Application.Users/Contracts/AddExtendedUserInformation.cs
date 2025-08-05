using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Users.Contracts
{
    public class AddExtendedUserInformation
    {
        public Guid UserId { get; set; }
        public string IdNumber { get; set; }
        public DateTime DateOfBirth { get; set; }
        public string Occupation { get; set; } = string.Empty;
        public string BusinesName { get; set; } = string.Empty;
    }
}
