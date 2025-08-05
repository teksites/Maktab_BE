using Application.Users.Contracts;
using MaktabDataContracts.Requests.Users;
using MaktabDataContracts.Responses.Users;
using Users.Contracts;

namespace Users.Services
{
    public interface IUserService
    {
        Task<UserInformationResponse> AddTemporaryUser(AddUserInformation userInformation);
        Task<bool> VerifyUserVerificationCodes(UserVerification userVerification);
        Task<UserInformationResponse> UpdateUser(UpdateUserPassword userInformation, bool ifTempPassword = false);
        Task<bool> DeleteUser(Guid userId, bool ifHardDelete);
        Task<UserInformationResponse> GetUserInformation(Guid userId);
        Task<bool> CheckIfUserNameExisit(string userName);
        Task<bool> CheckIfUserAlreadyRegistered(string email, string phone);
        Task<bool> CheckIfUserIsAdmin(Guid userId);
        Task<IEnumerable<UserInformationResponse>> GetAllUsersInformation(bool ifOnlyActive = true);
        Task<bool> SendActivationCode(Guid userId);
        Task<UserInformationResponse> GetUserInformation(string userName, string? password, bool ifForgotPassword);
        Task<bool> ForgotPassword(string userName, string? password);
        Task<bool> ResetUserPassword(UpdateUserPassword updateUserPassword);
        Task<bool> CheckIfTempUser(string userName);
        //Task<MaktabApiResult<UserTransactionsDetails>> CreateUserTransaction(AddUserTransaction addUserTransactions);

    }
}