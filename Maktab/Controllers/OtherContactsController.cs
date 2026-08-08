using Application.Users.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Maktab.Attributes;
using MaktabDataContracts.Requests.OtherContacts;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MaktabDataContracts.Models;
using Users.Services;
using MaktabDataContracts.Responses.OtherContacts;
using MaktabDataContracts.Enums;

namespace Maktab.Controllers
{
    [Route("api")]
    [ApiController]
    [ApiAuthorize()]

    public class OtherContactsController : ControllerBase
    {
        private readonly IOtherContactsService _otherContactsService;
        private readonly IDataAccessVerificationService _dataAccessVerificationService;
        private readonly ILogger<OtherContactsController> _logger;

        public OtherContactsController(
            IOtherContactsService otherContactsService,
            IDataAccessVerificationService dataAccessVerificationService,
            ILogger<OtherContactsController> logger)
        {
            _otherContactsService = otherContactsService;
            _dataAccessVerificationService = dataAccessVerificationService;
            _logger = logger;
        }

        [Authorize]
        [HttpGet("otherContacts/{otherContactId:guid}")]
        [EnableCors("corspolicy")]
        public async Task<ActionResult<OtherContactResponse>> GetOtherContacts(Guid otherContactId)
        {
            var otherContact = await _otherContactsService.GetOtherContact(otherContactId).ConfigureAwait(false);
            if (otherContact == null)
            {
                return NotFound();
            }

            if (!await HasFamilyAccessAsync(otherContact.FamilyId).ConfigureAwait(false))
            {
                return Forbid();
            }

            return Ok(otherContact);
        }
        
        [Authorize]
        [HttpGet("families/{familyId:guid}/otherContacts")]
        [EnableCors("corspolicy")]
        public async Task<IEnumerable<OtherContactResponse>> GetUserOtherContactss(Guid familyId, [FromQuery] List<ContactType> contactTypes)
        {
            return await _otherContactsService.GetFamilyOtherContacts(familyId, contactTypes);
        }

        [Authorize]
        [HttpPost("families/{familyId:guid}/otherContacts/add")]
        [EnableCors("corspolicy")]
        public async Task<ActionResult<OtherContactResponse>> AddUserAddress(Guid familyId, AddOtherContact otherContact)
        {
            try
            {
                return Ok(await _otherContactsService.AddOtherContact(otherContact).ConfigureAwait(false));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResult.Error(ex.Message));
            }
        }

        [Authorize]
        [HttpPost("otherContacts/{otherContactId:guid}/delete")]
        [EnableCors("corspolicy")]
        public async Task<ActionResult<bool>> DeleteOtherContact(Guid otherContactId, bool ifHardDelete = false)
        {
            var otherContact = await _otherContactsService.GetOtherContact(otherContactId).ConfigureAwait(false);
            if (otherContact == null)
            {
                return NotFound();
            }

            if (!await HasFamilyAccessAsync(otherContact.FamilyId).ConfigureAwait(false))
            {
                return Forbid();
            }

            return Ok(await _otherContactsService.DeleteOtherContact(otherContactId, ifHardDelete).ConfigureAwait(false));
        }

        [Authorize]
        [HttpPost("otherContacts/update")]
        [EnableCors("corspolicy")]
        public async Task<ActionResult<OtherContactResponse>> UpdateOtherContact(UpdateOtherContact otherContact)
        {
            var existingContact = await _otherContactsService.GetOtherContact(otherContact.ContactId).ConfigureAwait(false);
            if (existingContact == null)
            {
                return NotFound();
            }

            if (!await HasFamilyAccessAsync(existingContact.FamilyId).ConfigureAwait(false))
            {
                return Forbid();
            }

            try
            {
                return Ok(await _otherContactsService.UpdateOtherContact(otherContact).ConfigureAwait(false));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResult.Error(ex.Message));
            }
        }

        [Authorize]
        [HttpPost("families/{familyId:guid}/otherContacts/delete")]
        [EnableCors("corspolicy")]
        public async Task<bool> DeleteOtherContactByFamilyId(Guid familyId, bool ifHardDelete = false)
        {
            return await _otherContactsService.DeleteFamilyOtherContact(familyId, ifHardDelete).ConfigureAwait(false);
        }

        [Authorize]
        [HttpPost("families/{familyId:guid}/otherContacts/check")]
        [EnableCors("corspolicy")]
        public async Task<bool> CheckIfOtherContactExisit(Guid familyId, String phone)
        {
            return await _otherContactsService.CheckIfOtherContactExisit(familyId, phone).ConfigureAwait(false);
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
