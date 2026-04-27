using Application.Users.Contracts;
using MaktabDataContracts.Responses.Users;
using Users.Contracts;

namespace Users.Services
{
    public interface IExtendedUserInformationService
    {
        Task<ExtendedUserInformationResponse> AddExtendedUserInformation(AddExtendedUserInformationInternal userInformation);
        Task<ExtendedUserInformationResponse> UpdateExtendedUserInformation(AddExtendedUserInformationInternal userInformation);
        Task<bool> DeletExtendedUserInformation(Guid userId, bool ifHardDelete);
        Task<ExtendedUserInformationResponse> GetExtendedUserInformation(Guid userId);
        Task<bool> CheckIfExtendedUserInformationExisit(Guid userId);
        Task<bool> CheckIfExtendedFamilyInformationExisit(Guid familyId);
        Task<bool> CheckIfFamilySinExists(Guid familyId, string sin);
        Task<bool> DeleteFamilyExtendedUserInformation(Guid familyId, bool ifHardDelete = false);
        Task<ExtendedUserInformationResponse> GetFamilyExtendedUserInformation(Guid familyId);
    }
}
