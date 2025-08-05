using Application.Users.Contracts;
using MaktabDataContracts.Requests.Addresses;

namespace Users.Repository
{
    public interface IAddressRepository
    {
        Task<AddAddressIntenal> AddAddress(AddAddressIntenal address);
        Task<AddAddressIntenal> UpdateAddress(UpdateAddress address);
        Task<bool> DeleteAddress(Guid addressId, bool ifHardDelete = false);
        Task<bool> DeleteAddressByConnectedId(Guid id, bool ifHardDelete = false);
        Task<AddAddressIntenal> GetAddress(Guid addressId);
        Task<AddAddressIntenal> GetAddressWithConnectedId(Guid connectedId);
    }
}
