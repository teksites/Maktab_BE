using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Maktab.Attributes;
using Maktab.Models;
using MaktabDataContracts.Requests.Authentication;
using MaktabDataContracts.Responses.Authentication;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Claims;
using Users.Services;

namespace Maktab.Controllers
{
    [Route("api")]
    [ApiController]
    public class SessionController : ControllerBase
    {
        private readonly IUserLoginService _loginService;

        public SessionController(IUserLoginService loginService)
        {
            _loginService = loginService;
        }

        [AllowAnonymous]
        [ApiAuthorize(true, true)]
        [HttpPost("users/session/login")]
        public async Task<IActionResult> Authenticate([FromBody] UserCredential credential)
        {
            var authenticationResponse = await _loginService
                .Authenticate(credential.UserName, credential.Password, Request.Host.Value)
                .ConfigureAwait(false);

            if (authenticationResponse == null)
            {
                return Unauthorized();
            }

            return Ok(authenticationResponse);
        }

        [Authorize]
        [HttpPut("users/session/{sessionId}/logout")]
        [ApiAuthorize(true, false, MaktabDataContracts.Enums.UserRoleType.Normal, true)]
        public async Task<IActionResult> Logout(Guid sessionId)
        {
            var authenticationResponse = await _loginService.LogOutSession(sessionId).ConfigureAwait(false);
            return Ok(authenticationResponse);
        }

        [Authorize]
        [HttpPost("users/session/{sessionId:guid}/verify-2fa")]
        public async Task<ActionResult<TwoFactorLoginVerificationResponse>> VerifyTwoFactorLogin(
            Guid sessionId,
            [FromBody] VerifyTwoFactorLoginRequest request)
        {
            var userName = GetAuthenticatedUserName();
            if (string.IsNullOrWhiteSpace(userName))
            {
                return Unauthorized();
            }

            var response = await _loginService.VerifyTwoFactorLogin(sessionId, userName, request).ConfigureAwait(false);
            if (!response.Success)
            {
                return BadRequest(response);
            }

            return Ok(response);
        }

        [Authorize]
        [HttpPost("users/session/{sessionId:guid}/resend-2fa")]
        public async Task<ActionResult<TwoFactorLoginVerificationResponse>> ResendTwoFactorLogin(Guid sessionId)
        {
            var userName = GetAuthenticatedUserName();
            if (string.IsNullOrWhiteSpace(userName))
            {
                return Unauthorized();
            }

            var response = await _loginService.ResendTwoFactorLogin(sessionId, userName).ConfigureAwait(false);
            if (!response.Success)
            {
                return BadRequest(response);
            }

            return Ok(response);
        }

        private string GetAuthenticatedUserName()
        {
            return User?.Claims?.FirstOrDefault(claim => claim.Type == ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        }
    }
}
