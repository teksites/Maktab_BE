using MaktabDataContracts.Enums;
using MaktabDataContracts.Requests.InstituteStaff;
using MaktabDataContracts.Responses.InstituteStaff;

namespace Courses.Services
{
    public interface IInstituteStaffAssignmentService
    {
        Task<InstituteStaffAssignmentResponse> AddAssignment(AddInstituteStaffAssignmentRequest request);
        Task<InstituteStaffAssignmentResponse> UpdateAssignment(Guid assignmentId, UpdateInstituteStaffAssignmentRequest request);
        Task<bool> DeleteAssignment(Guid assignmentId, bool hardDelete = false);
        Task<IEnumerable<InstituteStaffAssignmentResponse>> GetAssignments(GetInstituteStaffRequest request);
        Task<IEnumerable<InstituteStaffCandidateResponse>> GetCandidates(GetInstituteStaffCandidatesRequest request);
        Task<bool> HasEligibleInstituteStaffAssignment(Guid instituteId, Guid userId, UserRoleType requiredRoles, DateTime effectiveOn);
    }
}
