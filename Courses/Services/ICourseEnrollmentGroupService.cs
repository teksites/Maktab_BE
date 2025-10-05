using MaktabDataContracts.Requests.Course;
using MaktabDataContracts.Responses.Course;

namespace Courses.Services
{
    public interface ICourseEnrollmentGroupService
    {
        Task<CourseEnrollmentGroupResponse> AddCourseEnrollmentGroup(AddCourseEnrollmentGroup group);
        Task<bool> UpdateCourseEnrollmentGroup(Guid groupId, bool hardDelete = false);
        Task<IEnumerable<CourseEnrollmentGroupResponse>> GetAllCourseGroups(Guid courseId, bool isActive);
        Task<IEnumerable<CourseEnrollmentGroupResponse>> GetAllGroups(Guid courseId);
        Task<CourseEnrollmentGroupResponse> GetCourseGroup(Guid groupId);
        Task<CourseEnrollmentGroupResponse> GetGroup(Guid groupId);
        Task<CourseEnrollmentGroupResponse> SetCourseGroupRegistrationStatus(Guid groupId, bool IfRegistrationOpen);
        Task<CourseEnrollmentGroupResponse> SetCourseRegistrationStatus(Guid courseId, bool IfRegistrationOpen);
        Task<bool> UpdateCourseEnrollmentGroup(Guid groupId, AddCourseEnrollmentGroup group);

    }
}
