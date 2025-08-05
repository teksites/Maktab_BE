using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InternalContracts
{
    public class AddSession
    {
        public Guid SessionId { get; set; }
        public Guid UserId { get; set; }
        public string Token { get; set; }
        public DateTime TokenExpiry { get; set; }
        public string? IpAddress { get; set; }
        public bool IsActive { get; set; }
        public DateTime LogInTime { get; set; }
        public DateTime? LogOutTime { get; set; }

    }
}
