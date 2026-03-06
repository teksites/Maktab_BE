using System;
using MaktabDataContracts.Enums;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Users.Services;

namespace Application.Users.Implementation
{
    public class DataAccessVerificationService : IDataAccessVerificationService
    {
        private readonly IUserLoginService _userLoginService;
        private readonly IUserService _userService;

        public DataAccessVerificationService(IUserLoginService userLoginService, IUserService userService)
        {
            _userLoginService = userLoginService;
            _userService = userService;
        }

        public async Task<SessionAccessContext> GetSessionAccessContext(Guid sessionId)
        {
            var userId = await _userLoginService.GetUserBySessionId(sessionId).ConfigureAwait(false);
            if (userId == Guid.Empty)
            {
                return null;
            }

            var userInfo = await _userService.GetUserInformation(userId).ConfigureAwait(false);
            if (userInfo == null)
            {
                return null;
            }

            return new SessionAccessContext
            {
                SessionId = sessionId,
                UserId = userId,
                FamilyId = userInfo.FamilyId,
                UserRoles = await _userService.GetUserRoles(userId).ConfigureAwait(false)
            };
        }

        public bool HasElevatedAccess(UserRoleType userRoles)
        {
            return userRoles.HasFlag(UserRoleType.Admin)
                || userRoles.HasFlag(UserRoleType.SuperUser)
                || userRoles.HasFlag(UserRoleType.SchoolAdmin)
                || userRoles.HasFlag(UserRoleType.SchoolSupervisor);
        }

        public DataAccessVerificationResult VerifyOwnership(SessionAccessContext sessionContext, IDictionary<string, object> actionArguments)
        {
            if (sessionContext == null)
            {
                return DataAccessVerificationResult.Deny("Access Denied: session context not found");
            }

            if (HasElevatedAccess(sessionContext.UserRoles))
            {
                return DataAccessVerificationResult.Allow();
            }

            var routeUserIds = new HashSet<Guid>();
            var routeFamilyIds = new HashSet<Guid>();

            foreach (var actionArgument in actionArguments)
            {
                CollectIdentifiers(actionArgument.Key, actionArgument.Value, routeUserIds, routeFamilyIds, 0);
            }

            if (routeUserIds.Any(userId => userId != sessionContext.UserId))
            {
                return DataAccessVerificationResult.Deny("Access Denied: route user does not match session user");
            }

            if (routeFamilyIds.Any(familyId => familyId != sessionContext.FamilyId))
            {
                return DataAccessVerificationResult.Deny("Access Denied: route family does not match session family");
            }

            return DataAccessVerificationResult.Allow();
        }

        private static void CollectIdentifiers(string name, object value, ISet<Guid> userIds, ISet<Guid> familyIds, int depth)
        {
            if (value == null || depth > 2)
            {
                return;
            }

            if (value is Guid guidValue)
            {
                if (guidValue == Guid.Empty)
                {
                    return;
                }

                if (name.Equals("userId", StringComparison.OrdinalIgnoreCase))
                {
                    userIds.Add(guidValue);
                }
                else if (name.Equals("familyId", StringComparison.OrdinalIgnoreCase))
                {
                    familyIds.Add(guidValue);
                }

                return;
            }

            if (value is string || value.GetType().IsPrimitive || value is DateTime || value is decimal)
            {
                return;
            }

            if (value is IEnumerable enumerable)
            {
                foreach (var item in enumerable)
                {
                    CollectIdentifiers(name, item, userIds, familyIds, depth + 1);
                }

                return;
            }

            foreach (var property in value.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!property.CanRead || property.GetIndexParameters().Length > 0)
                {
                    continue;
                }

                CollectIdentifiers(property.Name, property.GetValue(value), userIds, familyIds, depth + 1);
            }
        }
    }
}
