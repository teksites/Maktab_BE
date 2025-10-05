using MaktabDataContracts.Requests.Course;
using MaktabDataContracts.Responses.Course;

namespace Courses.Repository
{
    public interface ICourseEnrollmentGroupRepository
    {
        public Task<CourseEnrollmentGroupResponse> AddGroup(AddCourseEnrollmentGroup group);

        public  Task<CourseEnrollmentGroupResponse> GetGroup(Guid groupId);
        public Task<IEnumerable<CourseEnrollmentGroupResponse>> GetAllGroups(Guid courseId, bool isActive = true);

        public Task<bool> UpdateGroup(Guid groupId, AddCourseEnrollmentGroup group);
        public Task<bool> DeleteGroup(Guid groupId, bool hardDelete = false);
        public Task<IEnumerable<CourseEnrollmentGroupResponse>> GetAllCourseGroups(Guid courseId, bool isActive);
        public Task<CourseEnrollmentGroupResponse> GetCourseGroup(Guid groupId);
        public Task<CourseEnrollmentGroupResponse> SetCourseGroupRegistrationStatus(Guid groupId, bool ifRegistrationOpen);
    }
}
