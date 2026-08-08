using Application.Users.Contracts;
using MaktabDataContracts.Enums;
using MaktabDataContracts.Requests.Children;
using MaktabDataContracts.Responses.Children;

namespace Users.Repository
{
    public interface IUserChildrenRepository
    {
        Task<Child> AddChild(Child child);
        Task<Child> UpdateChild(UpdateChildRequest child);
        Task<bool> DeleteChild(Guid childId, bool ifHardDelete = false);
        Task<bool> DeleteFamilyChildren(Guid familyId, bool ifHardDelete = false);
        Task<Child> GetChild(Guid childId);
        Task<IEnumerable<Child>> GetFamilyChildren(Guid familyId);
        Task<bool> CheckIfChildExist(UserChildToVerify child);
        Task<ChildEducationalProfileResponse?> GetChildEducationalProfile(Guid childId);
        Task<ChildEducationalProfileResponse> UpsertChildEducationalProfile(Guid childId, Guid familyId, IReadOnlyCollection<QuranSurah> completedSurahs);
    }
}
