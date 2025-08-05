using Application.Users.Contracts;
using Users.Contracts;

namespace Users.Repository
{
    public interface IUserRepository
    {
        Task<UserInformation> AddUser(UserInformation userInformation);
        Task<UserInformation> UpdateUser(UpdateUserPassword userInformation, bool ifTempPassword = false);
        Task<bool> DeleteUser(Guid userId, bool ifHardDelete = false);
        Task<UserInformation> GetUserInformation(Guid userId);
        Task<bool> CheckIfUserNameExisit(string userName);
        Task<bool> CheckIfUserAlreadyRegistered(string email, string phone);
        Task<bool> CheckIfUserIsAdmin(Guid userId);
        Task<bool> CheckIfTempUser(string userName);
        Task<IEnumerable<UserInformation>> GetAllUsersInformation(bool ifOnlyActive = true);
        Task<UserInformation> GetUserInformation(string userName, string? password, bool ifForgotPassword);
        Task<IEnumerable<UserInformation>> GetAllFamilyUsersInformation(Guid id, bool ifOnlyActive = true);
        Task<UserInformation> LinkUserToAFamily(Guid userId, Guid familyId);
    }
}
