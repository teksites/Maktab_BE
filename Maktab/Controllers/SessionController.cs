using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Maktab.Attributes;
using Maktab.Models;
using Quartz;
using System;
using System.Threading.Tasks;
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
            var authenticationResponse = await _loginService.Authenticate(credential.UserName, credential.Password, Request.Host.Value).ConfigureAwait(false);

            if (authenticationResponse == null)
                return Unauthorized();

            return Ok(authenticationResponse);
        }
  
        [Authorize]
        [HttpPut("users/session/{sessionId}/logout")]
        [ApiAuthorize(true)]

        public async Task<IActionResult> Logout(Guid sessionId)
        {
            var authenticationResponse = await _loginService.LogOutSession(sessionId).ConfigureAwait(false);

        
            return Ok(authenticationResponse);
        }

    }
}
