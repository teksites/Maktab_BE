using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Maktab.Attributes;
using Users.Utils;
using MaktabDataContracts.Enums;

namespace Maktab.Controllers
{
    [Route("api")]
    [ApiController]

    public class AppStateController : ControllerBase
    {
        private readonly IAppState _appState;
        public AppStateController(IAppState appState)
        {
            _appState = appState;
        }

        [Authorize]
        [ApiAuthorize(false, false, UserRoleType.Admin|UserRoleType.SuperUser|UserRoleType.SchoolAdmin|UserRoleType.SchoolSupervoiser)]
        [HttpPost("appstate/set")]
        public IActionResult Set(string key, string value)
        {
            _appState.Set(key, value);
            return Ok("Value set");
        }

        [Authorize]
        [ApiAuthorize(false, false, UserRoleType.Admin | UserRoleType.SuperUser | UserRoleType.SchoolAdmin | UserRoleType.SchoolSupervoiser)]
        [HttpGet("appstate/get")]
        public IActionResult Get(string key)
        {
            var value = _appState.Get(key);
            return value != null ? Ok(value) : NotFound();
        }

        [Authorize]
        [ApiAuthorize(false, false, UserRoleType.Admin | UserRoleType.SuperUser | UserRoleType.SchoolAdmin | UserRoleType.SchoolSupervoiser)]
        [HttpGet("appstate/all")]
        public IActionResult GetAll()
        {
            return Ok(_appState.GetAll());
        }
    }
}
