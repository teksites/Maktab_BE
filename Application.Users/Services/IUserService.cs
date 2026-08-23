using Application.Users.Contracts;
using MaktabDataContracts.Enums;
using MaktabDataContracts.Requests.Users;
using MaktabDataContracts.Responses.Users;
using Users.Contracts;

namespace Users.Services
{
    public interface IUserService
    {
        Task<UserInformationResponse> AddTemporaryUser(AddUserInformation userInformation);
        Task<bool> VerifyUserVerificationCodes(UserVerification userVerification);
        Task<UserInformationResponse> AdminUpdateUser(Guid userId, AdminUpdateUserRequest userInformation);
        Task<UserInformationResponse> UpdateUserProfile(Guid userId, UpdateUserProfileRequest userInformation);
        Task<UserInformationResponse> UpdateUser(UpdateUserPassword userInformation, bool ifTempPassword = false);
        Task<bool> DeleteUser(Guid userId, bool ifHardDelete);
        Task<UserInformationResponse> GetUserInformation(Guid userId);
        Task<bool> CheckIfUserNameExisit(string userName);
        Task<Guid> GetUserFamilyInformation(UserFamilyInformationRequest userInformation);
        Task<FamilyInformationDetailsResponse> GetFamilyInformation(Guid familyId);
        Task<bool> CheckIfUserAlreadyRegistered(string email, string phone);
        Task<bool> CheckIfUserIsAdmin(Guid userId);
        Task<UserRoleType> GetUserRoles(Guid userId);
        Task<IEnumerable<UserInformationResponse>> GetAllUsersInformation(bool ifOnlyActive = true);
        Task<bool> SendActivationCode(Guid userId);
        Task<UserInformationResponse> GetUserInformation(string userName, string? password, bool ifForgotPassword);
        Task<UserInformationResponse> GetUserInformationByEmail(string email);
        Task<bool> ForgotPassword(string userName, string? password);
        Task<bool> ResetUserPassword(UpdateUserPassword updateUserPassword);
        Task<bool> CheckIfTempUser(string userName);
        Task<IEnumerable<UserInformationResponse>> GetAllFamilyUsersInformation(Guid familyId, bool ifOnlyActive = true);
        Task<IReadOnlyList<string>> GetVerifiedFamilyNotificationEmailAddresses(Guid familyId);
        Task<UserInformationResponse> LinkUserToAFamily(Guid userId, Guid familyId);
        //Task<MaktabApiResult<UserTransactionsDetails>> CreateUserTransaction(AddUserTransaction addUserTransactions);

    }
}
