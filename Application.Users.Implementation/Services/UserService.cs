using Application.Users.Contracts;
using Email;
using Microsoft.Extensions.Configuration;
using MaktabDataContracts.Enums;
using MaktabDataContracts.Requests.Users;
using MaktabDataContracts.Responses.Addresses;
using MaktabDataContracts.Responses.Course;
using MaktabDataContracts.Responses.OtherContacts;
using MaktabDataContracts.Responses.Users;
using MaktabDataContracts.Helpers;
using System.Text;
using Users.Contracts;
using Users.Repository;
using Users.Services;
using Users.Utils.Implementation;

namespace Application.Users.Implementation
{
    public class UserService : IUserService
    {
        private const string ActivationCodeEmailSubject = "ICC Maktab account registration activation code - code d’activation d’inscription ICC Maktab";
        private readonly IConfiguration _configuration;
        private readonly IUserRepository _repository;
        private readonly ITempUserRepository _tempUserRepository;
        private readonly IAddressService _addressService;
        private readonly IOtherContactsService _otherContactsService;
        private readonly IUserChildrenService _userChildsService;
        private readonly IUserChildrenRepository _userChildrenRepository;
        private readonly ISendEmailService _sendEmailService;

        public UserService(IConfiguration configuration, IUserRepository repository, ITempUserRepository tempUserRepository, IAddressService addressService, 
            IOtherContactsService otherContactsService, IUserChildrenService userChildsService, IUserChildrenRepository userChildrenRepository, ISendEmailService sendEmailService) 
        {
            _configuration = configuration;
            _repository = repository;
            _tempUserRepository = tempUserRepository;
            _addressService = addressService;
            _otherContactsService = otherContactsService;
            _userChildsService = userChildsService;
            _userChildrenRepository = userChildrenRepository;
            _sendEmailService = sendEmailService;
        }

        public async Task<UserInformationResponse> AddTemporaryUser(AddUserInformation userInformation)
        {
            await EnsureParentRelationshipIsAvailableAsync(userInformation.FamilyId, userInformation.Relationship).ConfigureAwait(false);

            var userInforationToStore = MapToUserRegistrationInformation(userInformation);
            var tempuser = await _tempUserRepository.AddTemporaryUser(userInforationToStore).ConfigureAwait(false);
           
            var success = _sendEmailService.SendEmail(new EmailData
            {
                To = userInformation.Email,
                 Subject = ActivationCodeEmailSubject,
                 Body = BuildActivationCodeEmailBody(
                     userInformation.FirstName,
                     userInformation.LastName,
                     userInforationToStore.EmailVerificationCode)
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
                else
                {
                    await EnsureParentRelationshipIsAvailableAsync(
                        tempUser.FamilyId,
                        tempUser.Relationship,
                        tempUser.UserId).ConfigureAwait(false);
                }

                var result = await _repository.AddUser(tempUser).ConfigureAwait(false);
               
                if (result != null)
                {
                    if (!await CreateLinkedChildIfRequired(tempUser).ConfigureAwait(false))
                    {
                        await _repository.DeleteUser(result.UserId, true).ConfigureAwait(false);
                        return false;
                    }

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
                await _otherContactsService.DeleteFamilyOtherContact(userId, ifHardDelete).ConfigureAwait(false);
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

        public async Task<UserRoleType> GetUserRoles(Guid userId)
        {
            return await _repository.GetUserRoles(userId).ConfigureAwait(false);
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

                    Subject = ActivationCodeEmailSubject,
                    Body = BuildActivationCodeEmailBody(
                        user.FirstName,
                        user.LastName,
                        updateData.EmailVerificationCode)
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
                       "<div>&nbsp;</div>" +
                       "<div><strong>ICC Brossard School Registration</strong></div>"

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

        private static string BuildActivationCodeEmailBody(string firstName, string lastName, string activationCode)
        {
            return $"<p><strong>Greetings {firstName} {lastName}</strong>,</p>" +
                   "<p>Activation code for the registration of your ICC Brossard Schools and Activities portal account (Maktab).</p>" +
                   $"<div>Your activation code is : <strong>{activationCode}</strong>.</div>" +
                   "<div></div>" +
                   "<div>Please activate your account by entering this code inside your ICC Brossard Schools and Activities portal.</div>" +
                   "<div>&nbsp;</div>" +
                   "<div>&nbsp;</div>" +
                   "<div><strong>ICC Brossard Schools and Activities Registration Portal (Maktab)</strong></div>" +
                   "<div>&nbsp;</div>" +
                   "<hr />" +
                   $"<p><strong>Bonjour {firstName} {lastName},</strong></p>" +
                   "<p>Code d'activation pour l'inscription a votre compte sur le portail des ecoles et activites ICC Brossard (Maktab).</p>" +
                   $"<div>Votre code d'activation est : <strong>{activationCode}</strong>.</div>" +
                   "<div></div>" +
                   "<div>Veuillez saisir ce code pour activer votre compte sur le portail des ecoles et activites ICC Brossard.</div>" +
                   "<div>&nbsp;</div>" +
                   "<div>&nbsp;</div>" +
                   "<div><strong>Portail d'inscription des ecoles et activites ICC Brossard (Maktab)</strong></div>";
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
            var userRole = UserRoleHelper.FromStrings(addUserInformation.UserRoles);
            if (userRole == UserRoleType.None)
            {
                userRole = UserRoleType.Normal;
            }

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
                FamilyId = addUserInformation.FamilyId,
                UserRole = userRole
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
                IfTempUser = ifTempUser,
                UserRoles = UserRoleHelper.ToStrings(userInformation.UserRole)
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

        public async Task<UserInformationResponse> GetUserInformationByEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return null;
            }

            return await GetUserInformation(email.Trim(), null, true).ConfigureAwait(false);
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
            var familyUsers = await _repository.GetAllFamilyUsersInformation(familyId, ifOnlyActive).ConfigureAwait(false);

            return ApplyParentRelationshipPrecedence(familyUsers ?? Enumerable.Empty<UserInformation>())
                .Select(user => MapToUserInformationResponse(user, user.IfTempUser))
                .ToList();
        }

        public async Task<UserInformationResponse> LinkUserToAFamily(Guid userId, Guid familyId)
        {
            return MapToUserInformationResponse(await _repository.LinkUserToAFamily(userId, familyId).ConfigureAwait(false), false);
        }

        public async Task<Guid> GetUserFamilyInformation(UserFamilyInformationRequest userInformation)
        {
            return await _repository.GetUserFamilyInformation(userInformation).ConfigureAwait(false);
        }

        public async Task<FamilyInformationDetailsResponse> GetFamilyInformation(Guid familyId)
        {
            var familyUsersTask = _repository.GetAllFamilyUsersInformation(familyId, true);
            var otherContactsTask = _otherContactsService.GetFamilyOtherContacts(familyId, Array.Empty<ContactType>());
            var familyAddressesTask = _addressService.GetAddressWithConnectedId(familyId, includeInactive: false);

            await Task.WhenAll(familyUsersTask, otherContactsTask, familyAddressesTask).ConfigureAwait(false);

            var familyUsers = await familyUsersTask.ConfigureAwait(false) ?? Enumerable.Empty<UserInformation>();
            var otherContacts = await otherContactsTask.ConfigureAwait(false) ?? Enumerable.Empty<OtherContactResponse>();
            var familyAddresses = await familyAddressesTask.ConfigureAwait(false) ?? Enumerable.Empty<AddressResponse>();

            return new FamilyInformationDetailsResponse
            {
                FamilyInformation = ApplyParentRelationshipPrecedence(familyUsers)
                    .Where(user => IsFamilyInformationRelationship(user.Relationship))
                    .Select(MapToFamilyInfo)
                    .ToList(),
                OtherContacts = otherContacts
                    .Select(MapToOtherContactInfo)
                    .ToList(),
                FamilyAddress = familyAddresses.ToList()
            };
        }

        private async Task<bool> CreateLinkedChildIfRequired(UserInformation userInformation)
        {
            if (!TryMapRelationshipToUserType(userInformation.Relationship, out var userType))
            {
                return true;
            }

            try
            {
                var linkedChild = await _userChildrenRepository.AddChild(new Child
                {
                    ChildId = userInformation.UserId,
                    FamilyId = userInformation.FamilyId,
                    FirstName = userInformation.FirstName,
                    LastName = userInformation.LastName,
                    DateOfBirth = GetLinkedUserPlaceholderDate(),
                    Gender = MapLinkedUserGender(userInformation.Relationship),
                    RAMQNumber = string.Empty,
                    RAMQExpiry = GetLinkedUserPlaceholderDate(),
                    RAMQSequenceNumber = 0,
                    Allergies = string.Empty,
                    OtherHealthConditions = string.Empty,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedOn = DateTime.UtcNow,
                    AcedemicGroup = AcedemicGroupType.Adults,
                    HasAllergy = false,
                    Consent = string.Empty,
                    UserType = userType
                }).ConfigureAwait(false);

                return linkedChild != null;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryMapRelationshipToUserType(Relationship relationship, out UserType userType)
        {
            switch (relationship)
            {
                case Relationship.Mother:
                    userType = UserType.Mother;
                    return true;
                case Relationship.Father:
                    userType = UserType.Father;
                    return true;
                case Relationship.Guardian:
                    userType = UserType.Guardian;
                    return true;
                default:
                    userType = UserType.Child;
                    return false;
            }
        }

        private static DateTime GetLinkedUserPlaceholderDate()
        {
            return new DateTime(1900, 1, 1);
        }

        private static Gender MapLinkedUserGender(Relationship relationship)
        {
            return relationship switch
            {
                Relationship.Mother => Gender.Female,
                Relationship.Father => Gender.Male,
                _ => Gender.Unknown
            };
        }

        private async Task EnsureParentRelationshipIsAvailableAsync(Guid familyId, Relationship relationship, Guid? excludedUserId = null)
        {
            if (familyId == Guid.Empty || !IsSingleParentRelationship(relationship))
            {
                return;
            }

            var familyUsers = await _repository.GetAllFamilyUsersInformation(familyId, true).ConfigureAwait(false)
                ?? Enumerable.Empty<UserInformation>();

            var duplicateParentExists = familyUsers.Any(user =>
                user.Relationship == relationship &&
                user.UserId != excludedUserId);

            if (duplicateParentExists)
            {
                throw new InvalidOperationException(
                    $"{GetParentRelationshipDisplayName(relationship)} is already added and multiple same parents can't be added");
            }
        }

        private static IEnumerable<UserInformation> ApplyParentRelationshipPrecedence(IEnumerable<UserInformation> familyUsers)
        {
            var users = familyUsers.ToList();
            var prioritizedParents = new List<UserInformation>();

            foreach (var relationship in new[] { Relationship.Mother, Relationship.Father })
            {
                var latestVerifiedParent = users
                    .Where(user => user.Relationship == relationship && !user.IfTempUser)
                    .OrderByDescending(user => user.UpdatedOn)
                    .ThenByDescending(user => user.CreatedAt)
                    .ThenByDescending(user => user.UserId)
                    .FirstOrDefault();

                if (latestVerifiedParent != null)
                {
                    prioritizedParents.Add(latestVerifiedParent);
                    continue;
                }

                var latestPendingParent = users
                    .Where(user => user.Relationship == relationship && user.IfTempUser)
                    .OrderByDescending(user => user.UpdatedOn)
                    .ThenByDescending(user => user.CreatedAt)
                    .ThenByDescending(user => user.UserId)
                    .FirstOrDefault();

                if (latestPendingParent != null)
                {
                    prioritizedParents.Add(latestPendingParent);
                }
            }

            return users
                .Where(user => !IsSingleParentRelationship(user.Relationship))
                .Concat(prioritizedParents)
                .OrderBy(user => user.Relationship)
                .ThenBy(user => user.IfTempUser)
                .ThenByDescending(user => user.UpdatedOn)
                .ThenByDescending(user => user.CreatedAt)
                .ThenByDescending(user => user.UserId)
                .ToList();
        }

        private static bool IsFamilyInformationRelationship(Relationship relationship)
        {
            return relationship == Relationship.Mother
                || relationship == Relationship.Father
                || relationship == Relationship.Guardian;
        }

        private static FamilyInfo MapToFamilyInfo(UserInformation userInformation)
        {
            return new FamilyInfo
            {
                UserId = userInformation.UserId,
                UserName = BuildDisplayName(userInformation.FirstName, userInformation.LastName, userInformation.UserName),
                Email = userInformation.Email,
                Phone = userInformation.Phone,
                Relationship = userInformation.Relationship
            };
        }

        private static OtherContactInfo MapToOtherContactInfo(OtherContactResponse otherContact)
        {
            return new OtherContactInfo
            {
                ContactId = otherContact.ContactId,
                UserName = BuildDisplayName(otherContact.FirstName, otherContact.LastName),
                Phone = otherContact.Phone,
                Relationship = otherContact.Relationship,
                ContactType = otherContact.ContactType
            };
        }

        private static string BuildDisplayName(string? firstName, string? lastName, string? fallback = null)
        {
            var fullName = $"{firstName} {lastName}".Trim();
            return string.IsNullOrWhiteSpace(fullName) ? (fallback ?? string.Empty) : fullName;
        }

        private static bool IsSingleParentRelationship(Relationship relationship)
        {
            return relationship == Relationship.Mother || relationship == Relationship.Father;
        }

        private static string GetParentRelationshipDisplayName(Relationship relationship)
        {
            return relationship switch
            {
                Relationship.Mother => "Mother",
                Relationship.Father => "Father",
                _ => relationship.ToString()
            };
        }

        /*public async Task<MaktabApiResult<UserTransactionsDetails>> CreateUserTransaction(AddUserTransaction addUserTransactions)
        {
            return await _userTransactionsService.CreateUserTransaction(addUserTransactions).ConfigureAwait(false);
        }*/
    }
}
