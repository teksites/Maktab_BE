using MaktabDataContracts.Requests.Institute;
using MaktabDataContracts.Responses.Institute;

namespace Courses.Repository
{
    public interface IInstitutePolicyRepository
    {
        /// <summary>
        /// Adds a new policy for an institute.
        /// </summary>
        Task<InstitutePolicyResponse> AddPolicy(AddInstitutePolicy policy);

        /// <summary>
        /// Retrieves a single policy by its ID.
        /// </summary>
        Task<InstitutePolicyResponse> GetPolicy(Guid policyId);

        /// <summary>
        /// Returns all policies for an institute.
        /// If isActiveFilter is provided, filters on IsActive; otherwise returns all.
        /// </summary>
        Task<IEnumerable<InstitutePolicyResponse>> GetAllPolicies(Guid instituteId, bool? isActiveFilter = null);

        /// <summary>
        /// Updates an existing policy.
        /// </summary>
        Task<bool> UpdatePolicy(UpdateInstitutePolicy policy);

        /// <summary>
        /// Deletes a policy. 
        /// If hardDelete is true, deletes the row; otherwise marks it inactive.
        /// </summary>
        Task<bool> DeletePolicy(Guid policyId, bool hardDelete = false);
    }
}
