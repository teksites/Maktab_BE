using MaktabDataContracts.Requests.Institute;
using MaktabDataContracts.Responses.Institute;

namespace Courses.Services
{
    public interface IInstitutePolicyService
    {
        Task<InstitutePolicyResponse> AddPolicy(AddInstitutePolicy policy);
        Task<bool> DeletePolicy(Guid policyId, bool hardDelete = false);
        Task<IEnumerable<InstitutePolicyResponse>> GetAllPolicies(Guid instituteId);
        Task<InstitutePolicyResponse> GetPolicy(Guid policyId);
        Task<bool> UpdatePolicy(Guid policyId, AddInstitutePolicy policy);
    }
}
