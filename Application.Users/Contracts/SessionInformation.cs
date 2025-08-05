using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Users.Contracts
{
    public class SessionInformation1
    {
        public Guid SessionID { get; set; }
        public Guid UserId { get; set; }
        public string Token { get; set; }
        public DateTime TokenExpiryTime { get; set; }
        public bool IsActive { get; set; }
        public DateTime LoginTime { get; set; }
        public DateTime LogoutTime { get; set; }
        public string IPAddress { get; set; }   

     
    }
}
