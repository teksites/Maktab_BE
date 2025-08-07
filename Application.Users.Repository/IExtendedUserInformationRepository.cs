using Application.Users.Contracts;

namespace Users.Repository
{
    public interface IExtendedUserInformationRepository
    {
        Task<ExtendedUserInformationDetail> AddExtendedUserInformation(ExtendedUserInformationDetail userInformation);
        Task<ExtendedUserInformationDetail> UpdateExtendedUserInformation(ExtendedUserInformationDetail userInformation);
        Task<bool> DeleteExtendedUserInformation(Guid userId, bool ifHardDelete = false);
        Task<bool> DeleteFamilyExtendedUserInformation(Guid familyId, bool ifHardDelete = false);
        Task<ExtendedUserInformationDetail> GetExtendedUserInformation(Guid userId);
        Task<ExtendedUserInformationDetail> GetFamilyExtendedUserInformation(Guid familyId);
        Task<bool> CheckIfExtendedUserInformationExisit(Guid userId);
        Task<bool> CheckIfExtendedFamilyInformationExisit(Guid familyId);

    }
}
