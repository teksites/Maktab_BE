using MaktabDataContracts.Requests.Course;
using MaktabDataContracts.Responses.Course;

namespace Courses.Services
{
    public interface ICourseService
    {
        Task<CourseResponseDetailed> AddCourse(AddCourse course);
        Task<CourseResponseDetailed> UpdateCourse(Guid courseId, AddCourse course);
        Task<CourseResponseDetailed> SetCourseRegistrationStatus(Guid courseId, bool ifRegistrationOpen);
        Task<bool> DeleteCourse(Guid courseId, bool ifHardDelete);
        Task<CourseResponseDetailed> GetCourse(Guid courseId);

        // Use GetCourseOptions which already has IsActive
        Task<IEnumerable<CourseResponseDetailed>> GetAllCourses(GetCourseOptions options);

        Task<CourseEnrollmentGroupResponse> AddCourseEnrollmentGroup(AddCourseEnrollmentGroup courseEnrollmentGroup);
        Task<CourseEnrollmentGroupResponse> UpdateCourseEnrollmentGroup(UpdateCourseEnrollmentGroup courseEnrollmentGroup);
        Task<CourseEnrollmentGroupResponse> SetCourseGroupRegistrationStatus(Guid groupId, bool ifRegistrationOpen);
        Task<bool> DeleteCourseGroup(Guid courseGroupId);
        Task<CourseEnrollmentGroupResponse> GetCourseGroup(Guid groupId);

        // Removed separate isActive boolean, handled by GetCourseOptions
        Task<IEnumerable<CourseEnrollmentGroupResponse>> GetAllCourseGroups(Guid courseId, GetCourseOptions options);
        Task<bool> SetCourseRegistrationOpenStatus(Guid courseId, bool ifRegistrationOpen);
    }
}
