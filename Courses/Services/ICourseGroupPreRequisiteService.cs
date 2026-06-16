using AddCourseGroupPreRequisite = MaktabDataContracts.Requests.Course.AddCourseGroupPreRequisite;
using CourseGroupPreRequisiteResponse = MaktabDataContracts.Responses.Course.CourseGroupPreRequisiteResponse;

namespace Courses.Services
{
    public interface ICourseGroupPreRequisiteService
    {
        Task<CourseGroupPreRequisiteResponse> Add(AddCourseGroupPreRequisite preRequisite);
        Task<CourseGroupPreRequisiteResponse?> Get(Guid courseGroupPreRequisiteId);
        Task<IEnumerable<CourseGroupPreRequisiteResponse>> GetByCourseGroup(Guid courseGroupId, bool onlyActive = true);
        Task<bool> Delete(Guid courseGroupPreRequisiteId, bool hardDelete = false);
    }
}
