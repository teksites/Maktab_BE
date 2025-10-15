using MaktabDataContracts.Requests.Course;
using MaktabDataContracts.Responses.Course;

namespace Courses.Repository
{
    public interface ICourseRepository
    {
        /// <summary>
        /// Adds a new course.
        /// </summary>
        Task<CourseResponseDetailed> AddCourse(AddCourse course);

        /// <summary>
        /// Retrieves a course by its ID. Returns null if not found.
        /// </summary>
        Task<CourseResponseDetailed?> GetCourse(Guid courseId);

        /// <summary>
        /// Retrieves all courses with filtering options.
        /// </summary>
        Task<IEnumerable<CourseResponseDetailed>> GetAllCourses(GetCourseOptions options);

        /// <summary>
        /// Retrieves all courses. Optionally return only active courses.
        /// </summary>
        Task<IEnumerable<CourseResponseDetailed>> GetAllCourses(bool onlyActive = true);

        /// <summary>
        /// Updates an existing course.
        /// </summary>
        Task<bool> UpdateCourse(Guid courseId, AddCourse course);

        /// <summary>
        /// Deletes a course. Can perform soft or hard delete.
        /// </summary>
        Task<bool> DeleteCourse(Guid courseId, bool hardDelete = false);

        /// <summary>
        /// Updates the registration status of a course. Returns null if course not found.
        /// </summary>
        Task<CourseResponseDetailed?> SetCourseRegistrationStatus(Guid courseId, bool ifRegistrationOpen);

        Task<IEnumerable<CourseSessionInfoResponse>> GetFamilyCourseSessionInfo(Guid familyId, Guid? instituteId);


    }
}
