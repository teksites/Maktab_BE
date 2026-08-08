using MaktabDataContracts.Enums;
using MaktabDataContracts.Requests.Course;
using MaktabDataContracts.Responses.Course;

namespace Courses.Services
{
    public interface IStudentCourseResultService
    {
        Task<IReadOnlyList<StudentCourseResultResponse>> GetFamilyResults(Guid userId, UserRoleType userRoles, Guid familyId, Guid? courseId = null);
        Task<IReadOnlyList<StudentCourseResultResponse>> GetChildResults(Guid userId, UserRoleType userRoles, Guid childId, Guid? courseId = null);
        Task<IReadOnlyList<StudentCourseResultResponse>> GetCourseResults(Guid userId, UserRoleType userRoles, Guid courseId);
        Task<StudentCourseResultResponse?> GetCourseChildResult(Guid userId, UserRoleType userRoles, Guid courseId, Guid childId);
        Task<StudentCourseResultResponse> UpsertCourseChildResult(Guid userId, UserRoleType userRoles, Guid courseId, Guid childId, UpsertStudentCourseResultRequest request);
    }
}
