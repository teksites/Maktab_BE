using AddCourseGroupPreRequisite = MaktabDataContracts.Requests.Course.AddCourseGroupPreRequisite;
using CourseGroupPreRequisiteResponse = MaktabDataContracts.Responses.Course.CourseGroupPreRequisiteResponse;

namespace Courses.Repository
{
    public interface ICourseGroupPreRequisiteRepository
    {
        Task<CourseGroupPreRequisiteResponse> Add(AddCourseGroupPreRequisite preRequisite);
        Task<CourseGroupPreRequisiteResponse?> Get(Guid courseGroupPreRequisiteId);
        Task<IEnumerable<CourseGroupPreRequisiteResponse>> GetByCourseGroup(Guid courseGroupId, bool onlyActive = true);
        Task<IEnumerable<CourseGroupPreRequisiteResponse>> GetByEnrollment(Guid enrollmentId, bool onlyActive = true);
        Task<bool> Update(Guid courseGroupPreRequisiteId, AddCourseGroupPreRequisite preRequisite);
        Task<bool> Delete(Guid courseGroupPreRequisiteId, bool hardDelete = false);
    }
}
