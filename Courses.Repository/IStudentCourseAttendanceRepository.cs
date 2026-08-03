using MaktabDataContracts.Requests.Attendance;
using MaktabDataContracts.Responses.Attendance;

namespace Courses.Repository
{
    public interface IStudentCourseAttendanceRepository
    {
        Task<IReadOnlyList<StudentCourseAttendanceResponse>> GetCourseGroupAttendance(Guid courseEnrollmentGroupId, DateTime attendanceDate, Guid? childId = null);
        Task<IReadOnlyList<AttendanceRecordResponse>> GetAttendanceRecords(GetAttendanceRecordsRequest request, IReadOnlyCollection<Guid>? accessibleGroupIds = null);
        Task<(int TotalRecords, int PresentCount)> GetAttendanceSummary(Guid studentCourseEnrollmentId);
        Task<(int TotalRecords, int PresentCount)> GetAttendanceSummary(Guid courseId, Guid childId);
        Task<IReadOnlyDictionary<Guid, (int TotalRecords, int PresentCount)>> GetAttendanceSummariesByCourse(Guid courseId);
        Task<CourseGroupAttendanceResponse> UpsertCourseGroupAttendance(UpsertCourseGroupAttendanceRequest request);
    }
}
