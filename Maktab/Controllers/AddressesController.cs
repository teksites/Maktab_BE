using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Maktab.Attributes;
using MaktabDataContracts.Enums;
using MaktabDataContracts.Requests.Addresses;
using MaktabDataContracts.Responses.Addresses;
using System;
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

        private readonly ILogger<AddressesController> _logger;

        public AddressesController(IAddressService addressService, ILogger<AddressesController> logger)
        {
            _addressService = addressService;
            _logger = logger;
        }

        [Authorize]
        [HttpGet("address/{addressId:guid}")]
        [EnableCors("corspolicy")]
        public async Task<AddressResponse> GetAddress(Guid addressId)
        {
            return await _addressService.GetAddress(addressId).ConfigureAwait(false);
        }
        
        [Authorize]
        [HttpGet("connectedid/{id:guid}/address")]
        [EnableCors("corspolicy")]
        public async Task<AddressResponse> GetUserAddress(Guid id)
        {
            return await _addressService.GetAddressWithConnectedId(id).ConfigureAwait(false);
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


        [Authorize]
        [HttpPost("address/update")]
        [EnableCors("corspolicy")]
        public async Task<AddressResponse> Update(UpdateAddress updateAddress)
        {
            return await _addressService.UpdateAddress(updateAddress).ConfigureAwait(false);
        }

        [Authorize]
        [HttpPost("address/{addressId:guid}/delete")]
        [EnableCors("corspolicy")]
        public async Task<bool> DeleteAddress(Guid addressId, bool ifHardDelete = false)
        {
            return await _addressService.DeleteAddress(addressId, ifHardDelete).ConfigureAwait(false);
        }

        [Authorize]
        [HttpPost("connectedids/{id:guid}/address/delete")]
        [EnableCors("corspolicy")]
        public async Task<bool> DeleteAdressByConnectId(Guid id, bool ifHardDelete = false)
        {
            return await _addressService.DeleteAddressByConnectedId(id, ifHardDelete).ConfigureAwait(false);
        }
    }
}
