using MaktabDataContracts.Requests.Course;
using MaktabDataContracts.Responses.Course;

namespace Courses.Services
{
    public interface IStudentCourseEnrollmentService
    {
        Task<StudentCourseEnrollmentResponse> AddEnrollment(AddStudentCourseEnrollment enrollment, bool ifAddedByAdmin = false);
        Task<bool> DeleteEnrollment(Guid enrollmentId, bool hardDelete = false, bool ifDeletedByAdmin = false);
        Task<IEnumerable<StudentCourseEnrollmentResponse>> GetAllEnrollments(Guid courseId);
        Task<StudentCourseEnrollmentResponse> GetEnrollment(Guid enrollmentId);
        Task<StudentCourseEnrollmentResponse> GetStudentCourseEnrollment(Guid childId, Guid courseId);
        Task<IEnumerable<StudentCourseEnrollmentResponse>> GetEnrollmentByFamily(Guid familyId);
        Task<bool> UpdateEnrollment(Guid enrollmentId, AddStudentCourseEnrollment enrollment, bool ifUpdatedByAdmin = false);
        Task<bool> RecalculateCourseFee(Guid courseId, Guid familyId);
    }
}
