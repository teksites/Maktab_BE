using Application.Users.Contracts;
using MaktabDataContracts.Requests.Addresses;

namespace Users.Repository
{
    public interface IAddressRepository
    {
        Task<AddAddressIntenal> AddAddress(AddAddressIntenal address);
        Task<AddAddressIntenal> UpdateAddress(UpdateAddress address, bool? isActive = null);
        Task<bool> DeleteAddress(Guid addressId, bool ifHardDelete = false);
        Task<bool> DeleteAddressByConnectedId(Guid id, bool ifHardDelete = false);

        // default value ensures old calls without parameter still compile
        Task<AddAddressIntenal> GetAddress(Guid addressId, bool includeInactive = false);
        Task<AddAddressIntenal> GetAddressWithConnectedId(Guid connectedId, bool includeInactive = false);
    }
}
