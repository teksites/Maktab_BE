using Application.Users.Contracts;
using MaktabDataContracts.Requests.Users;

namespace Users.Repository
{
    public interface ITempUserRepository
    {
        Task<UserInformation> AddTemporaryUser(UserRegistrationInformation userInformation);
        Task<bool> VerifyTempUserVerificationCodes(UserVerification userVerification);
        Task<bool> DeleteTempUser(Guid userId);
        Task<UserInformation> GetTempUserInformation(Guid userId);
        Task<bool> CheckIfTempUserNameExisit(string userName);
        Task<bool> CheckIfTempUserAlreadyRegistered(string email, string phone);
        Task<UserInformation> UpdateRegistrationActivationCodes(UpdateUserRegistrationInformation updateData);
        Task<IEnumerable<UserInformation>> GetAllTempUsersInformation(bool ifOnlyActive = true);
        Task<IEnumerable<UserInformation>> GetAllFamilyTempUsersInformation(Guid familyId, bool ifOnlyActive = true);
        Task<UserInformation> GetTempUserInformation(string userName, string password);
    }
}
