using Application.Users.Contracts;
using MaktabDataContracts.Requests.Users;
using MaktabDataContracts.Responses.Users;
using Users.Contracts;

namespace Users.Services
{
    public interface IExtendedUserInformationService
    {
        Task<ExtendedUserInformationResponse> AddExtendedUserInformation(AddExtendedUserInformation userInformation);
        Task<ExtendedUserInformationResponse> UpdateExtendedUserInformation(AddExtendedUserInformation userInformation);
        Task<bool> DeletExtendedUserInformation(Guid userId, bool ifHardDelete);
        Task<ExtendedUserInformationResponse> GetExtendedUserInformation(Guid userId);
        Task<bool> CheckIfExtendedUserInformationExisit(Guid userId);
    }
}