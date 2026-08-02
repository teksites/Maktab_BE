using MaktabDataContracts.Enums;
using MaktabDataContracts.Requests.Attendance;
using MaktabDataContracts.Responses.Attendance;

namespace Courses.Services
{
    public interface IStudentCourseAttendanceService
    {
        Task<CourseGroupAttendanceResponse> GetCourseGroupAttendance(Guid userId, UserRoleType userRoles, GetCourseGroupAttendanceRequest request);
        Task<CourseGroupAttendanceResponse> UpsertCourseGroupAttendance(Guid userId, UserRoleType userRoles, UpsertCourseGroupAttendanceRequest request);
        Task<IReadOnlyList<AttendanceRecordResponse>> GetFamilyAttendanceRecords(Guid familyId, GetAttendanceRecordsRequest request);
        Task<AttendanceReportResponse> GetFamilyAttendanceReport(Guid familyId, GetAttendanceReportRequest request);
        Task<IReadOnlyList<AttendanceRecordResponse>> GetStaffAttendanceRecords(Guid userId, UserRoleType userRoles, GetAttendanceRecordsRequest request);
        Task<AttendanceReportResponse> GetStaffAttendanceReport(Guid userId, UserRoleType userRoles, GetAttendanceReportRequest request);
    }
}
