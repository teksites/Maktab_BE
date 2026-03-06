using Application.Users.Contracts;
using Microsoft.Extensions.Configuration;
using MaktabDataContracts.Enums;
using MaktabDataContracts.Models;
using MaktabDataContracts.Requests.Addresses;
using MaktabDataContracts.Responses.Addresses;
using Users.Contracts;
using Users.Repository;
using Users.Services;

namespace Application.Users.Implementation
{
    public class AddressService : IAddressService
    {
        private readonly IConfiguration _configuration;
        private readonly IAddressRepository _repository;

        public AddressService(IConfiguration configuration, IAddressRepository repository) 
        {
            _configuration = configuration;
            _repository = repository;
        }

        public async Task<AddressResponse> AddAddress(AddAddress address, AddressType addressType)
        {
            return MapToAddressResponse(await _repository.AddAddress(MapToAddAddressIntenal(address, addressType)).ConfigureAwait(false));
        }

        public async Task<bool> DeleteAddress(Guid addressId, bool ifHardDelete)
        {
            return await _repository.DeleteAddress(addressId, ifHardDelete).ConfigureAwait(false);
        }

        public async Task<bool> DeleteAddressByConnectedId(Guid userId, bool ifHardDelete)
        {
            return await _repository.DeleteAddressByConnectedId(userId, ifHardDelete).ConfigureAwait(false);
        }

        public async Task<AddressResponse> GetAddress(Guid addressId, bool includeInactive)
        {
            return MapToAddressResponse(await _repository.GetAddress(addressId, includeInactive).ConfigureAwait(false));
        }

        public async Task<IEnumerable<AddressResponse>> GetAddressWithConnectedId(Guid connectedId, bool includeInactive)
        {
            var addresses = await _repository.GetAddressWithConnectedId(connectedId, includeInactive).ConfigureAwait(false);
            return addresses.Select(MapToAddressResponse).Where(a => a != null).ToList();
        }

        public async Task<AddressResponse> UpdateAddress(UpdateAddress address)
        {
            return MapToAddressResponse(await _repository.UpdateAddress(address).ConfigureAwait(false));
        }

        private AddAddressIntenal MapToAddAddressIntenal(AddAddress address, AddressType addressType)
        {
            if (address == null)
            {
                return null;
            }

            return new AddAddressIntenal
            {
                AddressId = Guid.NewGuid(),
                ConnectedId = address.ConnectedId,
                AddressLine1 = address.AddressLine1,
                AddressLine2 = address.AddressLine2,
                ApartmentNo = address.ApartmentNo,
                City = address.City,
                Country = address.Country,
                PostalCode = address.PostalCode,
                UnitNo = address.UnitNo,
                IsActive = true,
                CreatedAt = DateTime.Now,
                UpdatedOn = DateTime.Now,
                AddressType = addressType,
                HomeAddress = address.HomeAddress,
                Province = address.Province
            };
        }

        private AddressResponse MapToAddressResponse(AddAddressIntenal address)
        {
            if (address == null)
            {
                return null;
            }
            return new AddressResponse
            {
                AddressId = address.AddressId,
                ConnectedId = address.ConnectedId,
                AddressLine1 = address.AddressLine1,
                AddressLine2 = address.AddressLine2,
                ApartmentNo = address.ApartmentNo,
                City = address.City,
                Province = address.Province,
                Country = address.Country,
                PostalCode = address.PostalCode,
                UnitNo = address.UnitNo,
                AddressType = address.AddressType,
                HomeAddress = address.HomeAddress,
                IsActive = true,
                CreatedAt = address.CreatedAt,
                UpdatedOn = address.UpdatedOn,
            };
        }
    }
}
