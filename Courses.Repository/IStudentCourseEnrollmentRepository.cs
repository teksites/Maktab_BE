
using MaktabDataContracts.Requests.Course;
using MaktabDataContracts.Responses.Course;

namespace Courses.Repository
{
    public interface IStudentCourseEnrollmentRepository
    {
        Task<StudentCourseEnrollmentResponse> AddEnrollment(AddStudentCourseEnrollment enrollment);
        Task<bool> DeleteEnrollment(Guid enrollmentId, bool hardDelete = false);
        Task<StudentCourseEnrollmentResponse?> GetEnrollment(Guid enrollmentId);
        Task<IEnumerable<StudentCourseEnrollmentResponse>> GetAllEnrollmentsByCourse(Guid courseId);
        Task<IEnumerable<StudentCourseEnrollmentResponse>> GetAllEnrollmentsByFamily(Guid familyId);
        Task<bool> UpdateEnrollment(Guid enrollmentId, AddStudentCourseEnrollment enrollment);
        Task<StudentCourseEnrollmentResponse> GetStudentCourseEnrollment(Guid childId, Guid courseId);
        Task<IEnumerable<CourseEnrollmentGroupInformationResponse>> GetCourseEnrollmentGroupsInformation(Guid courseId);
        Task<CourseEnrollmentGroupInformationResponse?> GetCourseEnrollmentGroupInformation(Guid courseGroupId);
    }
}
