using MaktabDataContracts.Enums;
using MaktabDataContracts.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Threading.Tasks;
using Users.Services;

namespace Maktab.Attributes
{
    public class ApiAuthorizeAttribute : Attribute, IAsyncAuthorizationFilter
    {
        private readonly bool _allowTempUser;
        private readonly bool _ignoreHeaderCheck;
        private readonly UserRoleType _requiredRole;

        public ApiAuthorizeAttribute(bool allowTempUser = false, bool ignoreHeaderCheck = false, UserRoleType requiredRole = UserRoleType.Normal)
        {
            _allowTempUser = allowTempUser;
            _ignoreHeaderCheck = ignoreHeaderCheck;
            _requiredRole = requiredRole;
        }

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var request = context.HttpContext.Request;
            string sessionValue = null;
            Guid sessionId = Guid.Empty;

            if (!_ignoreHeaderCheck)
            {
                if (!request.Headers.TryGetValue("Session_Info", out var headerValue))
                {
                    context.Result = CreateUnauthorizedResult("Session header not found");
                    return;
                }

                sessionValue = headerValue;
                if (!Guid.TryParse(sessionValue, out sessionId) || sessionId == Guid.Empty)
                {
                    context.Result = CreateUnauthorizedResult("Invalid session id");
                    return;
                }

                var loginService = context.HttpContext.RequestServices.GetService<IUserLoginService>();
                if (loginService == null)
                {
                    context.Result = new StatusCodeResult(500);
                    return;
                }

                bool sessionActive = await loginService.CheckIfSessionExistOrActive(sessionId);
                if (!sessionActive)
                {
                    context.Result = CreateUnauthorizedResult("No active session found");
                    return;
                }
            }

            if (_allowTempUser)
                return; // temp users allowed, no further checks

            var user = context.HttpContext.User;
            if (!user.Identity.IsAuthenticated)
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            var requiredClaim = user.Claims.FirstOrDefault(x =>
                x.Type.Equals("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier"));

            if (requiredClaim == null)
            {
                context.Result = CreateForbiddenResult("Access Denied: user claim not found");
                return;
            }

            var userService = context.HttpContext.RequestServices.GetService<IUserService>();
            var loginSvc = context.HttpContext.RequestServices.GetService<IUserLoginService>();
            if (userService == null || loginSvc == null)
            {
                context.Result = new StatusCodeResult(500);
                return;
            }

            bool isTempUser = await userService.CheckIfTempUser(requiredClaim.Value);
            if (!isTempUser)
            {
                context.Result = CreateForbiddenResult("Access Denied: please activate your account");
                return;
            }

            // Get userId and roles
            var userId = await loginSvc.GetUserBySessionId(sessionId);
            if (userId == Guid.Empty)
            {
                context.Result = CreateForbiddenResult("Access Denied: invalid session");
                return;
            }

            var userRoles = await userService.GetUserRoles(userId);

            // Check role hierarchy
            if (!HasRequiredRole(userRoles, _requiredRole))
            {
                context.Result = CreateForbiddenResult("Access Denied: insufficient privileges");
                return;
            }

            // If all checks pass, allow access
        }

        private bool HasRequiredRole(UserRoleType userRoles, UserRoleType requiredRole)
        {
            // Hierarchy: Normal < SchoolSupervoiser < SchoolAdmin < SuperUser < Admin
            // Only allow access if user role >= required role in hierarchy
            int roleHierarchy(UserRoleType role) => role switch
            {
                UserRoleType.Normal => 1,
                UserRoleType.SchoolSupervoiser => 2,
                UserRoleType.SchoolAdmin => 3,
                UserRoleType.SuperUser => 4,
                UserRoleType.Admin => 5,
                _ => 0
            };

            int maxUserRoleLevel = Enum.GetValues(typeof(UserRoleType))
                .Cast<UserRoleType>()
                .Where(r => r != UserRoleType.Unknown && userRoles.HasFlag(r))
                .Select(r => roleHierarchy(r))
                .DefaultIfEmpty(0)
                .Max();

            int requiredRoleLevel = roleHierarchy(requiredRole);

            return maxUserRoleLevel >= requiredRoleLevel;
        }

        private IActionResult CreateUnauthorizedResult(string message)
        {
            return new ContentResult
            {
                StatusCode = StatusCodes.Status401Unauthorized,
                ContentType = "text/plain",
                Content = message
            };
        }

        private IActionResult CreateForbiddenResult(string message)
        {
            return new ContentResult
            {
                StatusCode = StatusCodes.Status403Forbidden,
                ContentType = "text/plain",
                Content = message
            };
        }
    }
}
