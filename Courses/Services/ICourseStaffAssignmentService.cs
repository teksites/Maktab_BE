using MaktabDataContracts.Enums;
using MaktabDataContracts.Requests.Course;
using MaktabDataContracts.Responses.Attendance;
using MaktabDataContracts.Responses.Course;

namespace Courses.Services
{
    public interface ICourseStaffAssignmentService
    {
        Task<CourseStaffAssignmentsResponse> GetCourseAssignments(Guid courseId, bool onlyActive = true);
        Task<CourseStaffAssignmentsResponse> SetCourseAssignments(SetCourseStaffAssignmentsRequest request);
        Task<CourseGroupStaffAssignmentsResponse> GetCourseGroupAssignments(Guid courseEnrollmentGroupId, bool onlyActive = true);
        Task<CourseGroupStaffAssignmentsResponse> SetCourseGroupAssignments(SetCourseGroupStaffAssignmentsRequest request);
        Task<IEnumerable<TeacherAssignedCourseGroupResponse>> GetAssignedCourseGroups(Guid userId, bool onlyActive = true);
        Task<TeacherCourseGroupRosterResponse> GetAssignedCourseGroupRoster(Guid userId, UserRoleType userRoles, Guid courseEnrollmentGroupId);
    }
}
