using MaktabDataContracts.Enums;
using MaktabDataContracts.Requests.Addresses;
using MaktabDataContracts.Responses.Addresses;

namespace Users.Services
{
    public interface IAddressService
    {
        Task<AddressResponse> AddAddress(AddAddress address, AddressType addressType);
        Task<AddressResponse> UpdateAddress(UpdateAddress address);
        Task<bool> DeleteAddress(Guid addressId, bool ifHardDelete);
        Task<AddressResponse> GetAddress(Guid addressId);
        Task<AddressResponse> GetAddressWithConnectedId(Guid connectedId);
        Task<bool> DeleteAddressByConnectedId(Guid userId, bool ifHardDelete);
    }
}