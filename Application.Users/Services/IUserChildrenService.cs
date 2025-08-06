using Application.Users.Contracts;
using MaktabDataContracts.Models;
using MaktabDataContracts.Requests.Children;
using MaktabDataContracts.Responses.Children;

namespace Users.Services
{
    public interface IUserChildrenService
    {
        Task<MaktabApiResult<ChildResponse>> AddChild(AddChildRequest child);
        Task<MaktabApiResult<ChildResponse>> UpdateChild(UpdateChildRequest child);
        Task<bool> DeleteChild(Guid childId, bool ifHardDelete);
        Task<MaktabApiResult<ChildResponse>> GetChild(Guid childId);
        Task<IEnumerable<MaktabApiResult<ChildResponse>>> GetUserChilds(Guid userId);
        Task<bool> CheckIfChildExisit(UserChildToVerify child);
        Task<bool> DeleteUserChilds(Guid userId, bool ifHardDelete);
    }
}