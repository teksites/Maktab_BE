using MaktabDataContracts.Enums;
using MaktabDataContracts.Responses.Course;

namespace Courses.Repository
{
    public interface IStudentCourseResultRepository
    {
        Task<StudentCourseResultResponse?> GetByEnrollmentId(Guid studentCourseEnrollmentId);
        Task<IReadOnlyList<StudentCourseResultResponse>> GetByFamilyId(Guid familyId, Guid? courseId = null, Guid? courseEnrollmentGroupId = null);
        Task<IReadOnlyList<StudentCourseResultResponse>> GetByChildId(Guid childId, Guid? courseId = null, Guid? courseEnrollmentGroupId = null);
        Task<StudentCourseResultResponse> Upsert(
            Guid studentCourseEnrollmentId,
            Guid childId,
            Guid familyId,
            Guid courseId,
            Guid courseEnrollmentGroupId,
            Guid instituteId,
            decimal attendancePercentage,
            StudentCourseResultStatus resultStatus,
            string remarks,
            Guid recordedByUserId,
            bool isActive);
    }
}
