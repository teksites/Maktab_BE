using Application.Users.Contracts;
using InternalContracts;

namespace Users.Repository
{
    public interface IUserLoginRepository
    {
        Task<bool> ValidateUser(string username, string password);
        Task<string> Authenticate(string userName, string password);
        Task<UserInformation> GetUserInformation(string userName, string password);
        Task<bool> LogInSession(AddSession addSession);
        Task<bool> LogOutSession(Guid sessionId);
        Task<bool> CheckIfSessionExistOrActive(Guid sessionId);
        Task<bool> DeleteInActiveSessions();
        Task<Guid> GetSessionByUserId(Guid userId);
        Task<Guid> GetUserBySessionId(Guid sessionId);
    }
}
