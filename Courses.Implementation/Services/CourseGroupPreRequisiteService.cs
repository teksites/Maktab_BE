using Courses.Repository;
using Courses.Services;
using AddCourseGroupPreRequisite = MaktabDataContracts.Requests.Course.AddCourseGroupPreRequisite;
using CourseGroupPreRequisiteResponse = MaktabDataContracts.Responses.Course.CourseGroupPreRequisiteResponse;

namespace Courses.Implementation.Services
{
    public class CourseGroupPreRequisiteService : ICourseGroupPreRequisiteService
    {
        private readonly ICourseGroupPreRequisiteRepository _repository;

        public CourseGroupPreRequisiteService(ICourseGroupPreRequisiteRepository repository)
        {
            _repository = repository;
        }

        public Task<CourseGroupPreRequisiteResponse> Add(AddCourseGroupPreRequisite preRequisite)
            => _repository.Add(preRequisite);

        public Task<CourseGroupPreRequisiteResponse?> Get(Guid courseGroupPreRequisiteId)
            => _repository.Get(courseGroupPreRequisiteId);

        public Task<IEnumerable<CourseGroupPreRequisiteResponse>> GetByCourseGroup(Guid courseGroupId, bool onlyActive = true)
            => _repository.GetByCourseGroup(courseGroupId, onlyActive);

        public Task<IEnumerable<CourseGroupPreRequisiteResponse>> GetByEnrollment(Guid enrollmentId, bool onlyActive = true)
            => _repository.GetByEnrollment(enrollmentId, onlyActive);

        public Task<bool> Update(Guid courseGroupPreRequisiteId, AddCourseGroupPreRequisite preRequisite)
            => _repository.Update(courseGroupPreRequisiteId, preRequisite);

        public Task<bool> Delete(Guid courseGroupPreRequisiteId, bool hardDelete = false)
            => _repository.Delete(courseGroupPreRequisiteId, hardDelete);
    }
}
