using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Application.Users.Contracts
{
    public class UpdateUserRegistrationInformation
    {
        public Guid UserId { get; set; }
        public string EmailVerificationCode { get; set; } = string.Empty;
        public string PhoneVerificationCode { get; set; } = string.Empty;
        public DateTime UpdatedOn { get; set; }
    }
}
