
namespace Application.Users.Contracts
{
    public class UserInformationResponse1
    {
        public Guid UserId { get; set; }
        public string FirstName { get;set; }
        public string LastName {  get;set; }
        public string UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedOn { get;set; }  
        public bool IfTempUser { get; set;} = true;
 
    }
}
