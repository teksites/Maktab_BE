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

    public class ApiAuthorizeAttribute : Attribute, IAuthorizationFilter
    {
        //private readonly string[] _roles;
        private readonly bool _allowTempUser;
        private readonly bool _ignoreHeaderCheck;
        private readonly bool _checkAdmin;

        public ApiAuthorizeAttribute(bool allowTempUser = false, bool ignoreHeaderCheck = false, bool checkAdmin = false)
        {
            _allowTempUser = allowTempUser;
            _ignoreHeaderCheck = ignoreHeaderCheck;
            _checkAdmin = checkAdmin;
        }

        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var request = context.HttpContext.Request;

            //var id = context.HttpContext.Request.Query["id"]

            if (!request.Headers.TryGetValue("Session_Info", out var sessionValue) && !_ignoreHeaderCheck)
            {
                // Set the response status code to 403 (Forbidden)
                context.HttpContext.Response.StatusCode = 401;
                context.HttpContext.Response.ContentType = "text/plain";

                // Write the custom forbidden message to the response body
                context.HttpContext.Response.WriteAsync("Session header not found").Wait();

                // Return a blank result (to prevent further processing)
                context.Result = new EmptyResult();
                return;
            }

            if (!Guid.TryParse(sessionValue, out var sessionId) && !_ignoreHeaderCheck && sessionId == Guid.Empty)
            {
                // Set the response status code to 403 (Forbidden)
                context.HttpContext.Response.StatusCode = 401;
                context.HttpContext.Response.ContentType = "text/plain";

                // Write the custom forbidden message to the response body
                context.HttpContext.Response.WriteAsync("Invalid session id").Wait();

                // Return a blank result (to prevent further processing)
                context.Result = new EmptyResult();
                return;
            }

            var loginService = context.HttpContext.RequestServices.GetService<IUserLoginService>();

            var ifSessionExist = sessionId != Guid.Empty && Task.Run(async () => await loginService.CheckIfSessionExistOrActive(sessionId).ConfigureAwait(false)).Result;

            if (!ifSessionExist && !_ignoreHeaderCheck)
            {
                // Set the response status code to 403 (Forbidden)
                context.HttpContext.Response.StatusCode = 401;
                context.HttpContext.Response.ContentType = "text/plain";

                // Write the custom forbidden message to the response body
                context.HttpContext.Response.WriteAsync("No active session found").Wait();

                // Return a blank result (to prevent further processing)
                context.Result = new EmptyResult();
                return;
            }

            if (_allowTempUser)
            {
                return;
            }

            var user = context.HttpContext.User;

            var userService = context.HttpContext.RequestServices.GetService<IUserService>();

            if (userService == null)
            {
                context.Result = new StatusCodeResult(500); // Internal Server Error if the service is not found
                return;
            }

            if (!user.Identity.IsAuthenticated)
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            var requiredclaim = user.Claims.ToList().FirstOrDefault(x => x.Type.Equals("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier"));

            if (requiredclaim != null)
            {
                var ifExist = Task.Run(async () => await userService.CheckIfTempUser(requiredclaim.Value).ConfigureAwait(false)).Result;

                if (!ifExist)
                {
                    // Set the response status code to 403 (Forbidden)
                    context.HttpContext.Response.StatusCode = 403;
                    context.HttpContext.Response.ContentType = "text/plain";

                    // Write the custom forbidden message to the response body
                    context.HttpContext.Response.WriteAsync("Access Denied: Your account is not permitted to access this resource. Please activate your user").Wait();

                    // Return a blank result (to prevent further processing)
                    context.Result = new EmptyResult();
                }
            }
            else
            {
                // Set the response status code to 403 (Forbidden)
                context.HttpContext.Response.StatusCode = 403;
                context.HttpContext.Response.ContentType = "text/plain";

                // Write the custom forbidden message to the response body
                context.HttpContext.Response.WriteAsync("Access Denied: Your account is not permitted to access this resource. Please activate your user").Wait();

                // Return a blank result (to prevent further processing)
                context.Result = new EmptyResult();
            }

            if (_checkAdmin)
            {
                var userId = Task.Run(async () => await loginService.GetUserBySessionId(sessionId).ConfigureAwait(false)).Result;

                if (userId != Guid.Empty)
                {
                    var ifExist = Task.Run(async () => await userService.CheckIfUserIsAdmin(userId).ConfigureAwait(false)).Result;

                    if (!ifExist)
                    {
                        // Set the response status code to 403 (Forbidden)
                        context.HttpContext.Response.StatusCode = 403;
                        context.HttpContext.Response.ContentType = "text/plain";

                        // Write the custom forbidden message to the response body
                        context.HttpContext.Response.WriteAsync("Access Denied: Your account is not permitted to access this resource. You need admin previlages").Wait();

                        // Return a blank result (to prevent further processing)
                        context.Result = new EmptyResult();
                    }
                }
                else
                {
                    // Set the response status code to 403 (Forbidden)
                    context.HttpContext.Response.StatusCode = 403;
                    context.HttpContext.Response.ContentType = "text/plain";

                    // Write the custom forbidden message to the response body
                    context.HttpContext.Response.WriteAsync("Access Denied: Your account is not permitted to access this resource. You need admin previlages").Wait();

                    // Return a blank result (to prevent further processing)
                    context.Result = new EmptyResult();
                }

            }
            return;            // If user is authenticated, check for roles
        }
    }
}
