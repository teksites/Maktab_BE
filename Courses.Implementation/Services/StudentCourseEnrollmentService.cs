using Courses.Repository;
using Courses.Services;
using MaktabDataContracts.Requests.Course;
using MaktabDataContracts.Responses.Course;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Courses.Implementation.Services
{
    public class StudentCourseEnrollmentService : IStudentCourseEnrollmentService
    {
        private readonly IStudentCourseEnrollmentRepository _repository;

        public StudentCourseEnrollmentService(IStudentCourseEnrollmentRepository repository)
        {
            _repository = repository;
        }

        public Task<StudentCourseEnrollmentResponse> AddEnrollment(AddStudentCourseEnrollment enrollment)
            => _repository.AddEnrollment(enrollment);

        public Task<StudentCourseEnrollmentResponse> GetEnrollment(Guid enrollmentId)
            => _repository.GetEnrollment(enrollmentId);

        public Task<IEnumerable<StudentCourseEnrollmentResponse>> GetAllEnrollments(Guid courseId)
            => _repository.GetAllEnrollments(courseId);

        public Task<bool> UpdateEnrollment(Guid enrollmentId, AddStudentCourseEnrollment enrollment)
            => _repository.UpdateEnrollment(enrollmentId, enrollment);

        public Task<bool> DeleteEnrollment(Guid enrollmentId, bool hardDelete = false)
            => _repository.DeleteEnrollment(enrollmentId, hardDelete);
    }
}
