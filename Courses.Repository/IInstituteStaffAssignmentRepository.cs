using MaktabDataContracts.Requests.InstituteStaff;
using MaktabDataContracts.Responses.InstituteStaff;

namespace Courses.Repository
{
    public interface IInstituteStaffAssignmentRepository
    {
        Task<InstituteStaffAssignmentResponse?> AddAssignment(AddInstituteStaffAssignmentRequest request);
        Task<InstituteStaffAssignmentResponse?> UpdateAssignment(UpdateInstituteStaffAssignmentRequest request);
        Task<bool> DeleteAssignment(Guid assignmentId, bool hardDelete = false);
        Task<InstituteStaffAssignmentResponse?> GetAssignment(Guid assignmentId);
        Task<IReadOnlyList<InstituteStaffAssignmentResponse>> GetAssignments(GetInstituteStaffRequest request);
        Task<IReadOnlyList<InstituteStaffAssignmentResponse>> GetAssignments(Guid instituteId, Guid? userId = null, bool onlyActive = false);
        Task<IReadOnlyList<InstituteStaffUserSummaryResponse>> GetCandidateUsers(GetInstituteStaffCandidatesRequest request);
        Task<InstituteStaffUserSummaryResponse?> GetUserSummary(Guid userId);
    }
}
