using MaktabDataContracts.Requests.Course;
using MaktabDataContracts.Responses.Course;

namespace Courses.Repository
{
    public interface ICourseRepository
    {
        public Task<CourseResponseDetailed> AddCourse(AddCourse course);
        public Task<CourseResponseDetailed> GetCourse(Guid courseId);
        public Task<IEnumerable<CourseResponseDetailed>> GetAllCourses(GetCourseOptions options);

        public Task<IEnumerable<CourseResponseDetailed>> GetAllCourses(bool onlyActive = true);
        public Task<bool> UpdateCourse(Guid courseId, AddCourse course);
        public  Task<bool> DeleteCourse(Guid courseId, bool hardDelete = false);

        public Task<CourseResponseDetailed> SetCourseRegistrationStatus(Guid courseId, bool ifRegistrationOpen);
        
    }
}
