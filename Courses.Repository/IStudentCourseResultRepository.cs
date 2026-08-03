using MaktabDataContracts.Enums;
using MaktabDataContracts.Responses.Course;

namespace Courses.Repository
{
    public interface IStudentCourseResultRepository
    {
        Task<StudentCourseResultResponse?> GetByChildAndCourse(Guid childId, Guid courseId);
        Task<IReadOnlyList<StudentCourseResultResponse>> GetByFamilyId(Guid familyId, Guid? courseId = null);
        Task<IReadOnlyList<StudentCourseResultResponse>> GetByChildId(Guid childId, Guid? courseId = null);
        Task<IReadOnlyList<StudentCourseResultResponse>> GetByCourseId(Guid courseId);
        Task<StudentCourseResultResponse> Upsert(
            Guid childId,
            Guid familyId,
            Guid courseId,
            Guid instituteId,
            decimal? attendancePercentage,
            StudentCourseResultStatus resultStatus,
            string remarks,
            Guid recordedByUserId,
            bool isActive);
    }
}
