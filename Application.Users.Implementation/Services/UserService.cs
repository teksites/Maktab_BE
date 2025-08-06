using Application.Users.Contracts;
using Email;
using Microsoft.Extensions.Configuration;
using MaktabDataContracts.Requests.Users;
using MaktabDataContracts.Responses.Users;
using System.Text;
using Users.Contracts;
using Users.Repository;
using Users.Services;
using Users.Utils.Implementation;

namespace Application.Users.Implementation
{
    public class UserService : IUserService
    {
        private readonly IConfiguration _configuration;
        private readonly IUserRepository _repository;
        private readonly ITempUserRepository _tempUserRepository;
        private readonly IAddressService _addressService;
        private readonly IOtherContactsService _userCardsService;
        private readonly IUserChildrenService _userChildsService;
        private readonly ISendEmailService _sendEmailService;

        public UserService(IConfiguration configuration, IUserRepository repository, ITempUserRepository tempUserRepository, IAddressService addressService, 
            IOtherContactsService userCardsService, IUserChildrenService userChildsService, ISendEmailService sendEmailService) 
        {
            _configuration = configuration;
            _repository = repository;
            _tempUserRepository = tempUserRepository;
            _addressService = addressService;
            _userCardsService = userCardsService;
            _userChildsService = userChildsService;
           _sendEmailService = sendEmailService;
        }
        public async Task<UserInformationResponse> AddTemporaryUser(AddUserInformation userInformation)
        {
            var userInforationToStore = MapToUserRegistrationInformation(userInformation);
            var tempuser = await _tempUserRepository.AddTemporaryUser(userInforationToStore).ConfigureAwait(false);
           
            var success = _sendEmailService.SendEmail(new EmailData
            {
                To = userInformation.Email,
                 Subject = "Maktab Money transfer registration activation code",
                 Body = $"<p><strong>Greetings {userInformation.FirstName} {userInformation.LastName}</strong>,</p>"+
                       "<p>Activation code for the regisration of your Maktab account </p>"+
                       $"<div>Your activation code is : <strong>{ userInforationToStore.EmailVerificationCode} </strong>.</div>" +
                       "<div></div>" +
                       "<div>Please activate your account by entering the code inside the Maktab App.</div>" +
                       "<div>&nbsp;</div>" +
                       "<div>Cheers</div>" +
                       "<div>&nbsp;</div>" +
                       "<div><strong>Maktab Money Transfer</strong></div>"
            });

            return MapToUserInformationResponse(tempuser, true); 
        }

        public async Task<bool> VerifyUserVerificationCodes(UserVerification userVerification)
        {
            if (await _tempUserRepository.VerifyTempUserVerificationCodes(userVerification).ConfigureAwait(false))
            {
                var tempUser = await _tempUserRepository.GetTempUserInformation(userVerification.UserId).ConfigureAwait(false);
                
                if (tempUser.FamilyId == Guid.Empty)// This check means that the first memeber of the family is getting registered. For second time, there will be a 
                    // valid family id with which second user will be connected to
                {
                    tempUser.FamilyId = Guid.NewGuid();
                }

                var result = await _repository.AddUser(tempUser).ConfigureAwait(false);
               
                if (result != null)
                {
                    return await _tempUserRepository.DeleteTempUser(userVerification.UserId).ConfigureAwait(false);
                }
                return false;
            }
            else
            {
                return false;
            }
        }

        public async Task<bool> CheckIfUserAlreadyRegistered(string email, string phone)
        {
            return await _repository.CheckIfUserAlreadyRegistered(email, phone).ConfigureAwait(false) || await _tempUserRepository.CheckIfTempUserAlreadyRegistered(email, phone).ConfigureAwait(false);
        }

        public async Task<bool> CheckIfUserNameExisit(string userName)
        {
            return await _repository.CheckIfUserNameExisit(userName).ConfigureAwait(false) || await _tempUserRepository.CheckIfTempUserNameExisit(userName).ConfigureAwait(false);
        }

        public async Task<bool> DeleteUser(Guid userId, bool ifHardDelete)
        {
            var ifDeleted = await _repository.DeleteUser(userId, ifHardDelete).ConfigureAwait(false);
            
            if (ifDeleted)
            {
                await _addressService.DeleteAddressByConnectedId(userId, ifHardDelete).ConfigureAwait(false);
                await _userCardsService.DeleteFamilyOtherContact(userId, ifHardDelete).ConfigureAwait(false);
                await _userChildsService.DeleteUserChilds(userId, ifHardDelete).ConfigureAwait(false);
            }
            return ifDeleted;
        }

        public async Task<IEnumerable<UserInformationResponse>> GetAllUsersInformation(bool ifOnlyActive = true)
        {
            var users = (await _repository.GetAllUsersInformation(ifOnlyActive).ConfigureAwait(false)).
                Select(user => MapToUserInformationResponse(user,false)).ToList();

            var tempusers = (await _tempUserRepository.GetAllTempUsersInformation(ifOnlyActive).ConfigureAwait(false)).
                Select(user => MapToUserInformationResponse(user, true)).ToList();
            users.AddRange(tempusers);
            return users;
        }

        public async Task<UserInformationResponse> GetUserInformation(Guid userId)
        {
            var userInfo = await _repository.GetUserInformation(userId).ConfigureAwait(false);
            
            if (userInfo != null)
            {
                var mappedUser = MapToUserInformationResponse(userInfo, false);
                return mappedUser;
            }
            else 
            { 
                userInfo = await _tempUserRepository.GetTempUserInformation(userId).ConfigureAwait(false);
                if(userInfo != null)
                {
                    var mappedUser = MapToUserInformationResponse(userInfo, true);
                    return mappedUser;

                }
            }
            return null;
        }
        
        public async Task<UserInformationResponse> UpdateUser(UpdateUserPassword userInformation, bool ifTempPassword = false)
        {

            var userInfo = await _repository.UpdateUser(userInformation, ifTempPassword).ConfigureAwait(false);
          
            if (userInfo !=null)
            {
                var mappedUser = MapToUserInformationResponse(userInfo, false);
                return mappedUser;
            }

            return null;
        }

        public async Task<bool> CheckIfUserIsAdmin(Guid userId)
        {
            return await _repository.CheckIfUserIsAdmin(userId).ConfigureAwait(false);
        }
        public async Task<bool> SendActivationCode(Guid userId)
        {
            var updateData = new UpdateUserRegistrationInformation
            {
                UserId = userId,
                EmailVerificationCode = GenerateRandomVerificationCode(),
                PhoneVerificationCode = GenerateRandomVerificationCode(),
                UpdatedOn = DateTime.Now
            };
            
            UserInformation user = await _tempUserRepository.UpdateRegistrationActivationCodes(updateData).ConfigureAwait(false);
            
            if (user != null) 
            {
                //send an email to the user here
                return await _sendEmailService.SendEmail(new EmailData
                {
                    To = user.Email,
                    Subject = "Maktab Money transfer registration activation code",
                    Body = $"<p><strong>Greetings {user.FirstName} {user.LastName}</strong>,</p>" +
                        "<p>Activation code for the regisration of your Maktab account </p>" +
                        $"<div>Your activation code is : <strong> {updateData.EmailVerificationCode} </strong>.</div>" +
                        "<div></div>" +
                        "<div>Please activate your account by entering the code inside the Maktab App.</div>" +
                        "<div>&nbsp;</div>" +
                        "<div>Cheers</div>" +
                        "<div>&nbsp;</div>" +
                        "<div><strong>Maktab Money Transfer</strong></div>"
                }).ConfigureAwait(false);
                
            }
            return false;
        }

        public async Task<bool> ForgotPassword(string userName, string? password)
        {
            var userInformation = await GetUserInformation(userName, password, true).ConfigureAwait(false);
            
            if (userInformation == null)
            {
                return false;
            }

            var tempPassword = CreatePassword(10);
            await _repository.UpdateUser(new UpdateUserPassword
            {
                OldPassword = "",
                NewPassword = tempPassword,
                UserId = userInformation.UserId
            }, true).ConfigureAwait(false);

            var emailSent = await _sendEmailService.SendEmail( new EmailData
            {
                Subject= "Reset Password for Maktab account",
                To = userInformation.Email,
                Body = $"<p><strong>Greetings {userInformation.FirstName} {userInformation.LastName}</strong>,</p>" +
                       $"<div>The temporary password to reset your Maktab account is : <strong> {tempPassword}</strong></div>" +
                       "<div></div>" +
                       "<div>Please reset your account password before logging into Maktab App.</div>" +
                       "<div>&nbsp;</div>" +
                       "<div>Cheers</div>" +
                       "<div>&nbsp;</div>" +
                       "<div><strong>Maktab Money Transfer</strong></div>"

            }).ConfigureAwait(false);
            
            return emailSent;
        }
        private static string CreatePassword(int length)
        {
            const string valid = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890";
            StringBuilder res = new StringBuilder();
            Random rnd = new Random();
            while (0 < length--)
            {
                res.Append(valid[rnd.Next(valid.Length)]);
            }
            return res.ToString();
        }

        private UserInformation MapToUserInformation(AddUserInformation addUserInformation)
        {
            return new UserInformation
            {
                UserId = Guid.NewGuid(),
                UserName = addUserInformation.UserName,
                Email = addUserInformation.Email,
                Phone = addUserInformation.Phone,
                FirstName = addUserInformation.FirstName,
                LastName = addUserInformation.LastName,
                Password = addUserInformation.Password,
                IsActive = true,// Set it to fals when in future we implement user verification via email code
                CreatedAt = DateTime.Now,
                UpdatedOn = DateTime.Now,
            };
        }

        private string GenerateRandomVerificationCode()
        {
            Guid g = Guid.NewGuid();
            string GuidString = Convert.ToBase64String(g.ToByteArray());
            GuidString = GuidString.Replace("=", "");
            GuidString = GuidString.Replace("+", "");
            return GuidString.Substring(0, 6);
        }
        private UserRegistrationInformation MapToUserRegistrationInformation(AddUserInformation addUserInformation)
        {
            return new UserRegistrationInformation
            {
                UserId = Guid.NewGuid(),
                UserName = addUserInformation.UserName,
                Email = addUserInformation.Email,
                Phone = addUserInformation.Phone,
                FirstName = addUserInformation.FirstName,
                LastName = addUserInformation.LastName,
                Password = addUserInformation.Password,
                Relationship = addUserInformation.Relationship,
                IsActive = true,
                CreatedAt = DateTime.Now,
                EmailVerificationCode = GenerateRandomVerificationCode(),
                PhoneVerificationCode = GenerateRandomVerificationCode(),
                FamilyId = addUserInformation.FamilyId
            };
        }

        private UserInformationResponse MapToUserInformationResponse(UserInformation userInformation, bool ifTempUser)
        {
            return new UserInformationResponse
            {
                UserId = userInformation.UserId,
                FamilyId = userInformation.FamilyId,
                UserName = userInformation.UserName,
                Email = userInformation.Email,
                Phone = userInformation.Phone,
                FirstName = userInformation.FirstName,
                LastName = userInformation.LastName,
                Relationship = userInformation.Relationship,
                IsActive = true,
                CreatedAt = userInformation.CreatedAt,
                UpdatedOn = userInformation.UpdatedOn,
                IfTempUser = ifTempUser
            };
        }

        public async Task<UserInformationResponse> GetUserInformation(string userName, string? password, bool ifForgotPassword)
        {
            var userInfo = await _repository.GetUserInformation(userName, password, ifForgotPassword).ConfigureAwait(false);

            if (userInfo != null)
            {
                var mappedUser = MapToUserInformationResponse(userInfo, false);
                return mappedUser;
            }
            else
            {
                userInfo = await _tempUserRepository.GetTempUserInformation(userName, password).ConfigureAwait(false);
                if (userInfo != null)
                {
                    var mappedUser = MapToUserInformationResponse(userInfo, true);
                    return mappedUser;

                }
            }
            return null;
        }

        public async Task<bool> ResetUserPassword(UpdateUserPassword updateUserPassword)
        {
            var userInformation = await _repository.GetUserInformation(updateUserPassword.UserId).ConfigureAwait(false);

            if (userInformation == null)
            {
                return false;
            }

            // Use the password hasher to verify the old password
            bool isPasswordMatch = PasswordHelper.VerifyPassword(updateUserPassword.OldPassword, userInformation.Password);

            if (!isPasswordMatch)
            {
                return false;
            }

            var result = await _repository.UpdateUser(updateUserPassword, false).ConfigureAwait(false);
            
            if (result != null)
            {
                return true;
            }

            return false;
        }

        public async Task<bool> CheckIfTempUser(string userName)
        {
            return  await _repository.CheckIfUserNameExisit(userName).ConfigureAwait(false);
        }

        public async Task<IEnumerable<UserInformationResponse>> GetAllFamilyUsersInformation(Guid familyId, bool ifOnlyActive = true)
        {
            var users = (await _repository.GetAllFamilyUsersInformation(familyId, ifOnlyActive).ConfigureAwait(false)).
               Select(user => MapToUserInformationResponse(user, false)).ToList();

            return users;
        }

        public async Task<UserInformationResponse> LinkUserToAFamily(Guid userId, Guid familyId)
        {
            return MapToUserInformationResponse(await _repository.LinkUserToAFamily(userId, familyId).ConfigureAwait(false), false);
        }

        /*public async Task<MaktabApiResult<UserTransactionsDetails>> CreateUserTransaction(AddUserTransaction addUserTransactions)
        {
            return await _userTransactionsService.CreateUserTransaction(addUserTransactions).ConfigureAwait(false);
        }*/
    }
}
