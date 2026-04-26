using Courses.Repository;
using Courses.Services;
using MaktabDataContracts.Enums;
using MaktabDataContracts.Requests.Course;
using MaktabDataContracts.Requests.Institute;
using MaktabDataContracts.Responses.Institute;

namespace Courses.Implementation.Services
{
    public class InstitutePolicyService : IInstitutePolicyService
    {
        private readonly IInstitutePolicyRepository _repository;

        public InstitutePolicyService(IInstitutePolicyRepository repository)
        {
            _repository = repository;
        }

        public Task<InstitutePolicyResponse> AddPolicy(AddInstitutePolicy policy)
            => _repository.AddPolicy(policy);

        public Task<InstitutePolicyResponse> GetPolicy(Guid policyId)
            => _repository.GetPolicy(policyId);

        public Task<IEnumerable<InstitutePolicyResponse>> GetAllPolicies(Guid instituteId)
            => _repository.GetAllPolicies(instituteId);

        public Task<bool> UpdatePolicy(Guid policyId, AddInstitutePolicy policy)
            => _repository.UpdatePolicy(ToUpdate(policy, policyId));

        public Task<bool> DeletePolicy(Guid policyId, bool hardDelete = false)
            => _repository.DeletePolicy(policyId, hardDelete);

        private UpdateInstitutePolicy ToUpdate(AddInstitutePolicy addPolicy, Guid institutePolicyId)
        {
            return new UpdateInstitutePolicy
            {
                InstitutePolicyId = institutePolicyId, // the existing policy id
                Details = addPolicy.Details,
                InstutePolicy = addPolicy.InstutePolicy,
                IsActive = addPolicy.IsActive
            };
        }

        public Task<InstitutePolicyResponse> GetPolicyByType(PolicyType policyType)
            => _repository.GetPolicyByType(policyType);
        }
    }
}
