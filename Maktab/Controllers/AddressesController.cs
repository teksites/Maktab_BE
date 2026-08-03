using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Maktab.Attributes;
using MaktabDataContracts.Enums;
using MaktabDataContracts.Requests.Addresses;
using MaktabDataContracts.Responses.Addresses;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Users.Services;

namespace Maktab.Controllers
{

    [Route("api")]
    [ApiController]
    [ApiAuthorize()]
    public class AddressesController : ControllerBase
    {
        private readonly IAddressService _addressService;
        private readonly IDataAccessVerificationService _dataAccessVerificationService;
        private readonly IUserService _userService;
        private readonly IUserChildrenService _userChildrenService;
        private readonly IOtherContactsService _otherContactsService;
        private readonly ILogger<AddressesController> _logger;

        public AddressesController(
            IAddressService addressService,
            IDataAccessVerificationService dataAccessVerificationService,
            IUserService userService,
            IUserChildrenService userChildrenService,
            IOtherContactsService otherContactsService,
            ILogger<AddressesController> logger)
        {
            _addressService = addressService;
            _dataAccessVerificationService = dataAccessVerificationService;
            _userService = userService;
            _userChildrenService = userChildrenService;
            _otherContactsService = otherContactsService;
            _logger = logger;
        }

        [Authorize]
        [HttpGet("address/{addressId:guid}")]
        [EnableCors("corspolicy")]
        public async Task<ActionResult<AddressResponse>> GetAddress(Guid addressId, bool includeInactive = false)
        {
            var address = await _addressService.GetAddress(addressId, includeInactive).ConfigureAwait(false);
            if (address == null)
            {
                return NotFound();
            }

            if (!await HasAddressAccessAsync(address).ConfigureAwait(false))
            {
                return Forbid();
            }

            return Ok(address);
        }
        
        [Authorize]
        [HttpGet("connectedid/{id:guid}/address")]
        [EnableCors("corspolicy")]
        public async Task<IEnumerable<AddressResponse>> GetUserAddress(Guid id, bool includeInactive = false)
        {
            return await _addressService.GetAddressWithConnectedId(id, includeInactive).ConfigureAwait(false);
        }

        [Authorize]
        [HttpPost("user/address/add")]
        [EnableCors("corspolicy")]
        public async Task<AddressResponse> AddUserAddress(AddAddress address)
        {
            return await _addressService.AddAddress(address, AddressType.Parent).ConfigureAwait(false);
        }

        [Authorize]
        [HttpPost("child/address/add")]
        [EnableCors("corspolicy")]
        public async Task<AddressResponse> AddChildAddress(AddAddress address)
        {
            return await _addressService.AddAddress(address, AddressType.Other).ConfigureAwait(false);
        }

        [Authorize]
        [HttpPost("clientcard/address/add")]
        [EnableCors("corspolicy")]
        public async Task<AddressResponse> AddClientCardAddress(AddAddress address)
        {
            return await _addressService.AddAddress(address, AddressType.Billing).ConfigureAwait(false);
        }

        [ApiAuthorize(false, false, UserRoleType.Admin | UserRoleType.SuperUser | UserRoleType.SchoolAdmin | UserRoleType.SchoolSupervisor)]
        [HttpPost("institute/address/add")]
        [EnableCors("corspolicy")]
        public async Task<AddressResponse> AddClienInstituteAddress(AddAddress address)
        {
            return await _addressService.AddAddress(address, AddressType.Institute).ConfigureAwait(false);
        }


        [Authorize]
        [HttpPost("address/update")]
        [EnableCors("corspolicy")]
        public async Task<ActionResult<AddressResponse>> Update(UpdateAddress updateAddress)
        {
            var existingAddress = await _addressService.GetAddress(updateAddress.AddressId, true).ConfigureAwait(false);
            if (existingAddress == null)
            {
                return NotFound();
            }

            if (!await HasAddressAccessAsync(existingAddress).ConfigureAwait(false))
            {
                return Forbid();
            }

            return Ok(await _addressService.UpdateAddress(updateAddress).ConfigureAwait(false));
        }

        [Authorize]
        [HttpPost("address/{addressId:guid}/delete")]
        [EnableCors("corspolicy")]
        public async Task<ActionResult<bool>> DeleteAddress(Guid addressId, bool ifHardDelete = false)
        {
            var address = await _addressService.GetAddress(addressId, true).ConfigureAwait(false);
            if (address == null)
            {
                return NotFound();
            }

            if (!await HasAddressAccessAsync(address).ConfigureAwait(false))
            {
                return Forbid();
            }

            return Ok(await _addressService.DeleteAddress(addressId, ifHardDelete).ConfigureAwait(false));
        }

        [Authorize]
        [HttpPost("connectedids/{id:guid}/address/delete")]
        [EnableCors("corspolicy")]
        public async Task<bool> DeleteAdressByConnectId(Guid id, bool ifHardDelete = false)
        {
            return await _addressService.DeleteAddressByConnectedId(id, ifHardDelete).ConfigureAwait(false);
        }

        private async Task<bool> HasAddressAccessAsync(AddressResponse address)
        {
            var sessionContext = await GetRequiredSessionContext().ConfigureAwait(false);
            if (_dataAccessVerificationService.HasElevatedAccess(sessionContext.UserRoles))
            {
                return true;
            }

            switch (address.AddressType)
            {
                case AddressType.Parent:
                case AddressType.Billing:
                    var user = await _userService.GetUserInformation(address.ConnectedId).ConfigureAwait(false);
                    return user != null && user.FamilyId == sessionContext.FamilyId;
                case AddressType.Other:
                    var child = await _userChildrenService.GetChild(address.ConnectedId).ConfigureAwait(false);
                    return child?.Result != null && child.Result.FamilyId == sessionContext.FamilyId;
                case AddressType.OtherContact:
                    var otherContact = await _otherContactsService.GetOtherContact(address.ConnectedId).ConfigureAwait(false);
                    return otherContact != null && otherContact.FamilyId == sessionContext.FamilyId;
                default:
                    return false;
            }
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
