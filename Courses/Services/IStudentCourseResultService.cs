using MaktabDataContracts.Enums;
using MaktabDataContracts.Requests.Course;
using MaktabDataContracts.Responses.Course;

namespace Courses.Services
{
    public interface IStudentCourseResultService
    {
        Task<IReadOnlyList<StudentCourseResultResponse>> GetFamilyResults(Guid userId, UserRoleType userRoles, Guid familyId, Guid? courseId = null, Guid? courseEnrollmentGroupId = null);
        Task<IReadOnlyList<StudentCourseResultResponse>> GetChildResults(Guid userId, UserRoleType userRoles, Guid childId, Guid? courseId = null, Guid? courseEnrollmentGroupId = null);
        Task<StudentCourseResultResponse?> GetEnrollmentResult(Guid userId, UserRoleType userRoles, Guid studentCourseEnrollmentId);
        Task<StudentCourseResultResponse> UpsertEnrollmentResult(Guid userId, UserRoleType userRoles, Guid studentCourseEnrollmentId, UpsertStudentCourseResultRequest request);
    }
}
