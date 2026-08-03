using MaktabDataContracts.Enums;
using MaktabDataContracts.Requests.Children;
using MaktabDataContracts.Responses.Children;

namespace Users.Services
{
    public interface IChildEducationalProfileService
    {
        IReadOnlyList<QuranSurahOptionResponse> GetQuranSurahOptions();
        Task<ChildEducationalProfileResponse?> GetChildEducationalProfile(Guid userId, UserRoleType userRoles, Guid childId);
        Task<ChildEducationalProfileResponse> UpsertChildEducationalProfile(Guid userId, UserRoleType userRoles, Guid childId, UpsertChildEducationalProfileRequest request);
    }
}
