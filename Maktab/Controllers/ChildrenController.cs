using Application.Users.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Maktab.Attributes;
using MaktabDataContracts.Enums;
using MaktabDataContracts.Models;
using MaktabDataContracts.Requests.Children;
using MaktabDataContracts.Responses.Children;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Users.Services;

namespace Maktab.Controllers
{

    [Route("api")]
    [ApiController]
    [ApiAuthorize()]
    public class ChildrenController : ControllerBase
    {
        private readonly IUserChildrenService _childrenService;
        private readonly IChildEducationalProfileService _childEducationalProfileService;
        private readonly IDataAccessVerificationService _dataAccessVerificationService;
        private readonly ILogger<ChildrenController> _logger;
        private readonly IUserService userService;

        public ChildrenController(
            IUserChildrenService childrenService,
            IChildEducationalProfileService childEducationalProfileService,
            IUserService service,
            IDataAccessVerificationService dataAccessVerificationService,
            ILogger<ChildrenController> logger)
        {
            _childrenService = childrenService;
            _childEducationalProfileService = childEducationalProfileService;
            _logger = logger;
            userService = service;
            _dataAccessVerificationService = dataAccessVerificationService;
        }

        [Authorize]
        [HttpGet("children/{childId:guid}")]
        [EnableCors("corspolicy")]
        public async Task<ActionResult<MaktabApiResult<ChildResponse>>> GetChild(Guid childId)
        {
            var session = await GetRequiredSessionContext().ConfigureAwait(false);
            var child = await _childrenService.GetChild(childId, session.UserId).ConfigureAwait(false);
            if (child?.Result == null)
            {
                return NotFound();
            }

            if (!await HasFamilyAccessAsync(child.Result.FamilyId).ConfigureAwait(false))
            {
                return Forbid();
            }

            return Ok(child);
        }
        
        [Authorize]
        [HttpGet("families/{familyId:guid}/children")]
        [EnableCors("corspolicy")]
        public async Task<ActionResult<IEnumerable<MaktabApiResult<ChildResponse>>>> GetUserChilds(Guid familyId, bool fetchAdults = false)
        {
            if (!await HasFamilyAccessAsync(familyId).ConfigureAwait(false))
            {
                return Forbid();
            }

            var session = await GetRequiredSessionContext().ConfigureAwait(false);
            return Ok(await _childrenService.GetUserChilds(familyId, fetchAdults, session.UserId).ConfigureAwait(false));
        }

        [Authorize]
        [HttpPost("families/{familyId:guid}/children/add")]
        [EnableCors("corspolicy")]
        public async Task<MaktabApiResult<ChildResponse>> AddUserChild(Guid familyId,AddChildRequest child)
        {
            return await _childrenService.AddChild(child).ConfigureAwait(false);
        }

        [Authorize]
        [HttpPut("children/{childId:guid}")]
        [EnableCors("corspolicy")]
        public async Task<ActionResult<MaktabApiResult<ChildResponse>>> UpdateChild(Guid childId, UpdateChildRequest child)
        {
            var existingChild = await _childrenService.GetChild(childId).ConfigureAwait(false);
            if (existingChild?.Result == null)
            {
                return NotFound();
            }

            if (!await HasFamilyAccessAsync(existingChild.Result.FamilyId).ConfigureAwait(false))
            {
                return Forbid();
            }

            child.ChildId = childId;
            return Ok(await _childrenService.UpdateChild(child).ConfigureAwait(false));
        }

        [Authorize]
        [HttpPost("children/{childId:guid}/delete")]
        [EnableCors("corspolicy")]
        public async Task<ActionResult<bool>> DeleteChild(Guid childId, bool ifHardDelete = false)
        {
            var existingChild = await _childrenService.GetChild(childId).ConfigureAwait(false);
            if (existingChild?.Result == null)
            {
                return NotFound();
            }

            if (!await HasFamilyAccessAsync(existingChild.Result.FamilyId).ConfigureAwait(false))
            {
                return Forbid();
            }

            return Ok(await _childrenService.DeleteChild(childId, ifHardDelete).ConfigureAwait(false));
        }

        [Authorize]
        [HttpPost("families/{familyId:guid}/children/delete")]
        [EnableCors("corspolicy")]
        public async Task<bool> DeleteChildByUserId(Guid familyId, bool ifHardDelete = false)
        {
            return await _childrenService.DeleteUserChilds(familyId, ifHardDelete).ConfigureAwait(false);
        }

        [Authorize]
        [HttpPost("children/check")]
        [EnableCors("corspolicy")]
        public async Task<bool> CheckIfUserChildExisit(UserChildToVerify clientchild)
        {

            return await _childrenService.CheckIfChildExisit(clientchild).ConfigureAwait(false);
        }

        [Authorize]
        [HttpGet("children/{childId:guid}/educational-profile")]
        [EnableCors("corspolicy")]
        public async Task<ActionResult<ChildEducationalProfileResponse>> GetChildEducationalProfile(Guid childId)
        {
            var session = await GetRequiredSessionContext().ConfigureAwait(false);
            var profile = await _childEducationalProfileService
                .GetChildEducationalProfile(session.UserId, session.UserRoles, childId)
                .ConfigureAwait(false);

            if (profile == null)
            {
                return NotFound();
            }

            return Ok(profile);
        }

        [ApiAuthorize(false, false, UserRoleType.Normal)]
        [HttpPut("children/{childId:guid}/educational-profile")]
        [EnableCors("corspolicy")]
        public async Task<ActionResult<ChildEducationalProfileResponse>> UpsertChildEducationalProfile(Guid childId, UpsertChildEducationalProfileRequest request)
        {
            var session = await GetRequiredSessionContext().ConfigureAwait(false);
            return Ok(await _childEducationalProfileService
                .UpsertChildEducationalProfile(session.UserId, session.UserRoles, childId, request)
                .ConfigureAwait(false));
        }

        private async Task<bool> HasFamilyAccessAsync(Guid familyId)
        {
            var sessionContext = await GetRequiredSessionContext().ConfigureAwait(false);
            if (_dataAccessVerificationService.HasElevatedAccess(sessionContext.UserRoles))
            {
                return true;
            }

            return familyId == sessionContext.FamilyId;
        }

        private async Task<SessionAccessContext> GetRequiredSessionContext()
        {
            if (!Request.Headers.TryGetValue("Session_Info", out var sessionHeader)
                || !Guid.TryParse(sessionHeader, out var sessionId)
                || sessionId == Guid.Empty)
            {
                throw new UnauthorizedAccessException("Session header not found or invalid.");
            }

            var sessionContext = await _dataAccessVerificationService.GetSessionAccessContext(sessionId).ConfigureAwait(false);
            if (sessionContext == null || sessionContext.UserId == Guid.Empty)
            {
                throw new UnauthorizedAccessException("No active session found.");
            }

            return sessionContext;
        }
    }
}
