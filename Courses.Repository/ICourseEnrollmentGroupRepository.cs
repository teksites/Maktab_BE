using MaktabDataContracts.Requests.Course;
using MaktabDataContracts.Responses.Course;

namespace Courses.Repository
{
    public interface ICourseEnrollmentGroupRepository
    {
        Task<CourseEnrollmentGroupResponse> AddGroup(AddCourseEnrollmentGroup group);

        Task<CourseEnrollmentGroupResponse> GetGroup(Guid groupId);
        Task<IEnumerable<CourseEnrollmentGroupResponse>> GetAllGroups(Guid courseId, bool isActive = true);

        Task<bool> UpdateGroup(Guid groupId, AddCourseEnrollmentGroup group);
        Task<bool> DeleteGroup(Guid groupId, bool hardDelete = false);

        Task<IEnumerable<CourseEnrollmentGroupResponse>> GetAllCourseGroups(Guid courseId, bool isActive);
        Task<CourseEnrollmentGroupResponse> GetCourseGroup(Guid groupId);
        Task<CourseEnrollmentGroupResponse> SetCourseGroupRegistrationStatus(Guid groupId, bool ifRegistrationOpen);
    }

}
