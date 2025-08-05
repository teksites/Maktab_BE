using Application.Users.Contracts;
using MaktabDataContracts.Requests.Users;
using MaktabDataContracts.Responses.Users;

namespace Users.Repository
{
    public interface IExtendedUserInformationRepository
    {
        Task<ExtendedUserInformationDetail> AddExtendedUserInformation(ExtendedUserInformationDetail userInformation);
        Task<ExtendedUserInformationDetail> UpdateExtendedUserInformation(ExtendedUserInformationDetail userInformation);
        Task<bool> DeleteExtendedUserInformation(Guid userId, bool ifHardDelete = false);
        Task<ExtendedUserInformationDetail> GetExtendedUserInformation(Guid userId);
        Task<bool> CheckIfExtendedUserInformationExisit(Guid userId);
    }
}
