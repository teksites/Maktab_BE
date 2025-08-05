using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Application.Users.Contracts
{
    public class AddUserInformation1
    {
        public string FirstName { get;set; }
        public string LastName {  get;set; }
        public string UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; }
        public string Password { get; set; } = string.Empty;
    }
}
