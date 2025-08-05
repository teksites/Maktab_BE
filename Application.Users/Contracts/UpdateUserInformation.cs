using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Application.Users.Contracts
{
    public class UpdateUserInformation1
    {
        public Guid UserId { get; set; }
        public string Password { get; set; } = string.Empty;
    }
}
