using MaktabDataContracts.Requests.Attendance;
using MaktabDataContracts.Responses.Attendance;

namespace Courses.Repository
{
    public interface IStudentCourseAttendanceRepository
    {
        Task<IReadOnlyList<StudentCourseAttendanceResponse>> GetCourseGroupAttendance(Guid courseEnrollmentGroupId, DateTime attendanceDate, Guid? childId = null);
        Task<IReadOnlyList<AttendanceRecordResponse>> GetAttendanceRecords(GetAttendanceRecordsRequest request, IReadOnlyCollection<Guid>? accessibleGroupIds = null);
        Task<CourseGroupAttendanceResponse> UpsertCourseGroupAttendance(UpsertCourseGroupAttendanceRequest request);
    }
}
