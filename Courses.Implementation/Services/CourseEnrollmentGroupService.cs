using Courses.Repository;
using Courses.Services;
using MaktabDataContracts.Requests.Course;
using MaktabDataContracts.Responses.Course;

namespace Courses.Implementation.Services
{
    public class CourseEnrollmentGroupService : ICourseEnrollmentGroupService
    {

        private readonly ICourseEnrollmentGroupRepository _repository;

        public CourseEnrollmentGroupService(ICourseEnrollmentGroupRepository repository)
        {
            _repository = repository;
        }

        public Task<CourseEnrollmentGroupResponse> AddCourseEnrollmentGroup(AddCourseEnrollmentGroup group)
         => _repository.AddGroup(group);

        public Task<CourseEnrollmentGroupResponse> GetGroup(Guid groupId)
            => _repository.GetGroup(groupId);

        public Task<IEnumerable<CourseEnrollmentGroupResponse>> GetAllGroups(Guid courseId)
            => _repository.GetAllGroups(courseId);

        public Task<bool> UpdateCourseEnrollmentGroup(Guid groupId, AddCourseEnrollmentGroup group)
            => _repository.UpdateGroup(groupId, group);

        public Task<bool> DeleteCourseEnrollmentGroup(Guid groupId, bool hardDelete = false)
            => _repository.DeleteGroup(groupId, hardDelete);

        public async Task<CourseEnrollmentGroupResponse> GetCourseGroup(Guid groupId)
        {
            return await _repository.GetGroup(groupId).ConfigureAwait(false);
        }

        public async Task<CourseEnrollmentGroupResponse> SetCourseGroupRegistrationStatus(Guid groupId, bool IfRegistrationOpen)
        {
            return await _repository.SetCourseGroupRegistrationStatus(groupId, IfRegistrationOpen).ConfigureAwait(false);
        }

        public async Task<IEnumerable<CourseEnrollmentGroupResponse>> GetAllCourseGroups(Guid courseId, bool isActive)
        {
            return await _repository.GetAllGroups(courseId, isActive).ConfigureAwait(false);
        }

        public async Task<CourseEnrollmentGroupResponse> SetCourseRegistrationStatus(Guid courseId, bool IfRegistrationOpen)
        {
            // it will set the status for all groups of the courses
            return await _repository.SetCourseGroupRegistrationStatus(courseId, IfRegistrationOpen).ConfigureAwait(false);

        }

    }
}