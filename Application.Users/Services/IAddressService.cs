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
        Task<AddressResponse> GetAddress(Guid addressId, bool includeInactive);
        Task<IEnumerable<AddressResponse>> GetAddressWithConnectedId(Guid connectedId, bool includeInactive);
        Task<bool> DeleteAddressByConnectedId(Guid userId, bool ifHardDelete);
    }
}
