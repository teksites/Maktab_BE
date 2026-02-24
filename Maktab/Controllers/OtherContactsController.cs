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

        private readonly ILogger<OtherContactsController> _logger;

        public OtherContactsController(IOtherContactsService otherContactsService, ILogger<OtherContactsController> logger)
        {
            _otherContactsService = otherContactsService;
            _logger = logger;
        }

        [Authorize]
        [HttpGet("otherContacts/{otherContactId:guid}")]
        [EnableCors("corspolicy")]
        public async Task<OtherContactResponse> GetOtherContacts(Guid otherContactId)
        {
            return await _otherContactsService.GetOtherContact(otherContactId).ConfigureAwait(false);
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
        public async Task<OtherContactResponse> AddUserAddress(Guid familyId, AddOtherContact otherContact)
        {
            return await _otherContactsService.AddOtherContact(otherContact).ConfigureAwait(false);
        }

        [Authorize]
        [HttpPost("otherContacts/{otherContactId:guid}/delete")]
        [EnableCors("corspolicy")]
        public async Task<bool> DeleteOtherContact(Guid otherContactId, bool ifHardDelete = false)
        {
            return await _otherContactsService.DeleteOtherContact(otherContactId, ifHardDelete).ConfigureAwait(false);
        }

        [Authorize]
        [HttpPost("otherContacts/update")]
        [EnableCors("corspolicy")]
        public async Task<OtherContactResponse> UpdateOtherContact(UpdateOtherContact otherContact)
        {
            return await _otherContactsService.UpdateOtherContact(otherContact).ConfigureAwait(false);
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
    }
}
