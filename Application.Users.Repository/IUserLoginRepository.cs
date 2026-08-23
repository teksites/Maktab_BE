using Application.Users.Contracts;
using AddSession = InternalContracts.AddSession;
using AddSessionTwoFactorCode = InternalContracts.AddSessionTwoFactorCode;
using SessionTwoFactorCode = InternalContracts.SessionTwoFactorCode;

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
        Task<SessionAuthenticationState> GetSessionAuthenticationState(Guid sessionId);
        Task<bool> AddSessionTwoFactorCode(AddSessionTwoFactorCode addSessionTwoFactorCode);
        Task<SessionTwoFactorCode> GetActiveSessionTwoFactorCode(Guid sessionId);
        Task<bool> DeactivateSessionTwoFactorCodes(Guid sessionId);
        Task<bool> IncrementSessionTwoFactorAttemptCount(Guid sessionTwoFactorCodeId);
        Task<bool> MarkSessionTwoFactorCodeVerified(Guid sessionTwoFactorCodeId, DateTime verifiedOn);
        Task<bool> MarkSessionTwoFactorVerified(Guid sessionId, DateTime verifiedOn);
    }
}
