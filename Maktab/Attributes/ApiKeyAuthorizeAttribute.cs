using Microsoft.AspNetCore.Authorization;
using System;

namespace Maktab.Attributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
    public sealed class ApiKeyAuthorizeAttribute : AuthorizeAttribute
    {
        public ApiKeyAuthorizeAttribute()
        {
            AuthenticationSchemes = "ApiKey";
        }
    }
}
