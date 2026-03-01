using System;
using System.Collections.Generic;
using MaktabDataContracts.Enums;
using System.Threading.Tasks;

namespace Users.Services
{
    public interface IDataAccessVerificationService
    {
        Task<SessionAccessContext> GetSessionAccessContext(Guid sessionId);
        bool HasElevatedAccess(UserRoleType userRoles);
        DataAccessVerificationResult VerifyOwnership(SessionAccessContext sessionContext, IDictionary<string, object> actionArguments);
    }

    public class SessionAccessContext
    {
        public Guid SessionId { get; set; }
        public Guid UserId { get; set; }
        public Guid FamilyId { get; set; }
        public UserRoleType UserRoles { get; set; } = UserRoleType.Unknown;
    }

    public class DataAccessVerificationResult
    {
        public bool IsAllowed { get; set; }
        public string FailureMessage { get; set; } = string.Empty;

        public static DataAccessVerificationResult Allow()
        {
            return new DataAccessVerificationResult
            {
                IsAllowed = true
            };
        }

        public static DataAccessVerificationResult Deny(string failureMessage)
        {
            return new DataAccessVerificationResult
            {
                IsAllowed = false,
                FailureMessage = failureMessage
            };
        }
    }
}
