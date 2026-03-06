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
    public class ApiAuthorizeAttribute : Attribute, IAsyncAuthorizationFilter, IAsyncActionFilter
    {
        private const string SessionAccessContextKey = "ApiAuthorize.SessionAccessContext";
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
            var dataAccessVerificationService = context.HttpContext.RequestServices.GetService<IDataAccessVerificationService>();
            if (userService == null || dataAccessVerificationService == null)
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

            var sessionAccessContext = await dataAccessVerificationService.GetSessionAccessContext(sessionId).ConfigureAwait(false);
            if (sessionAccessContext == null || sessionAccessContext.UserId == Guid.Empty)
            {
                context.Result = CreateForbiddenResult("Access Denied: invalid session");
                return;
            }

            // Check role hierarchy
            if (!HasRequiredRole(sessionAccessContext.UserRoles, _requiredRole))
            {
                context.Result = CreateForbiddenResult("Access Denied: insufficient privileges");
                return;
            }

            context.HttpContext.Items[SessionAccessContextKey] = sessionAccessContext;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            if (_ignoreHeaderCheck || _allowTempUser)
            {
                await next().ConfigureAwait(false);
                return;
            }

            var dataAccessVerificationService = context.HttpContext.RequestServices.GetService<IDataAccessVerificationService>();
            if (dataAccessVerificationService == null)
            {
                context.Result = new StatusCodeResult(500);
                return;
            }

            if (!context.HttpContext.Items.TryGetValue(SessionAccessContextKey, out var sessionAccessContextObject)
                || sessionAccessContextObject is not SessionAccessContext sessionAccessContext)
            {
                context.Result = CreateForbiddenResult("Access Denied: session context not found");
                return;
            }

            var verificationResult = dataAccessVerificationService.VerifyOwnership(
                sessionAccessContext,
                context.ActionArguments.ToDictionary(entry => entry.Key, entry => entry.Value));

            if (!verificationResult.IsAllowed)
            {
                context.Result = CreateForbiddenResult(verificationResult.FailureMessage);
                return;
            }

            await next().ConfigureAwait(false);
        }

        private bool HasRequiredRole(UserRoleType userRoles, UserRoleType requiredRole)
        {
            // Hierarchy: Normal < SchoolSupervoiser < SchoolAdmin < SuperUser < Admin
            // Only allow access if user role >= required role in hierarchy
            int roleHierarchy(UserRoleType role) => role switch
            {
                UserRoleType.Normal => 1,
                UserRoleType.SchoolSupervisor => 2,
                UserRoleType.SchoolAdmin => 3,
                UserRoleType.SuperUser => 4,
                UserRoleType.Admin => 5,
                _ => 0
            };

            int maxUserRoleLevel = Enum.GetValues(typeof(UserRoleType))
                .Cast<UserRoleType>()
                .Where(r => r != UserRoleType.None && userRoles.HasFlag(r))
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
