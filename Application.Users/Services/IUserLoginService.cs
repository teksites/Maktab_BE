
using MaktabDataContracts.Requests.Users;
using MaktabDataContracts.Responses.Authentication;
using Users.Contracts;

namespace Users.Services
{
    public interface IUserLoginService
    {
        Task<bool> ValidateUser(string username, string password);
        Task<AuthenticationResponse> Authenticate(string userName, string password, string ipAddress);
        Task<bool> LogOutSession(Guid sessionId);
        Task<bool> CheckIfSessionExistOrActive(Guid sessionId);
        Task<Guid> GetSessionByUserId(Guid userId);
        Task<Guid> GetUserBySessionId(Guid sessionId);
        Task<bool> ForgotPassword(string email);
        Task<bool> ResetUserPassword(UpdateUserPassword updateUserPassword);
    }
}
