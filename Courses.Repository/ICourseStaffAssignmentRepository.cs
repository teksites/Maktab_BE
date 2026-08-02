using MaktabDataContracts.Enums;
using MaktabDataContracts.Requests.Course;
using MaktabDataContracts.Responses.Attendance;
using MaktabDataContracts.Responses.Course;

namespace Courses.Repository
{
    public interface ICourseStaffAssignmentRepository
    {
        Task<IReadOnlyList<CourseStaffAssignmentResponse>> GetCourseAssignments(Guid courseId, bool onlyActive = false);
        Task<IReadOnlyList<CourseGroupStaffAssignmentResponse>> GetCourseGroupAssignments(Guid courseEnrollmentGroupId, bool onlyActive = false);
        Task<bool> HasActiveDirectGroupAssignments(Guid courseId);
        Task<CourseStaffAssignmentsResponse> ReplaceCourseAssignments(SetCourseStaffAssignmentsRequest request);
        Task<CourseGroupStaffAssignmentsResponse> ReplaceCourseGroupAssignments(SetCourseGroupStaffAssignmentsRequest request);
        Task<IReadOnlyList<TeacherAssignedCourseGroupResponse>> GetAssignedCourseGroups(Guid userId, bool onlyActive = true);
    }
}
