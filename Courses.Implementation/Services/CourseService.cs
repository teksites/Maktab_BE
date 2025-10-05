using Courses.Repository;
using Courses.Services;
using MaktabDataContracts.Requests.Course;
using MaktabDataContracts.Responses.Course;

namespace Courses.Implementation.Services
{
    public class CourseService : ICourseService
    {
        private readonly ICourseRepository _repository;
        private readonly ICourseEnrollmentGroupService _courseEnrollmentGroupService;

        public CourseService(ICourseRepository repository, ICourseEnrollmentGroupService courseEnrollmentGroupService)
        {
            _repository = repository;
            _courseEnrollmentGroupService = courseEnrollmentGroupService;
        }

        public Task<CourseResponseDetailed> AddCourse(AddCourse course)
            => _repository.AddCourse(course);

        public Task<CourseResponseDetailed> GetCourse(Guid courseId)
            => _repository.GetCourse(courseId);

        public async Task<IEnumerable<CourseResponseDetailed>> GetAllCourses(GetCourseOptions options)
        {
            var courses = await _repository.GetAllCourses(options).ConfigureAwait(false);

            foreach (var course in courses)
            {
                var groups = await _courseEnrollmentGroupService.GetAllGroups(course.CourseId).ConfigureAwait(false);
                course.CourseEnrollmentGroups = groups.ToList();
                course.AcedemicGroups = groups.SelectMany(g => g.AcedemicGroups).Distinct().ToList();
            }

            return courses;
        }

        public Task<CourseResponseDetailed> SetCourseRegistrationStatus(Guid courseId, bool ifRegistrationOpen)
            => _repository.SetCourseRegistrationStatus(courseId, ifRegistrationOpen);

        public Task<bool> DeleteCourse(Guid courseId, bool ifHardDelete)
            => _repository.DeleteCourse(courseId, ifHardDelete);

        public async Task<CourseResponseDetailed> UpdateCourse(Guid courseId, AddCourse course)
        {
            if(await _repository.UpdateCourse(courseId, course).ConfigureAwait(false))
            {
                return await _repository.GetCourse(courseId).ConfigureAwait(false);
            }
            return null;
        }

        public Task<CourseEnrollmentGroupResponse> AddCourseEnrollmentGroup(AddCourseEnrollmentGroup courseEnrollmentGroup)
            => _courseEnrollmentGroupService.AddCourseEnrollmentGroup(courseEnrollmentGroup);

        public async Task<CourseEnrollmentGroupResponse> UpdateCourseEnrollmentGroup(UpdateCourseEnrollmentGroup courseEnrollmentGroup)
        {
            if (await _courseEnrollmentGroupService.UpdateCourseEnrollmentGroup(courseEnrollmentGroup.CourseEnrollmentGroupId, MapToAdd(courseEnrollmentGroup)))
            {
                return await _courseEnrollmentGroupService.GetGroup(courseEnrollmentGroup.CourseEnrollmentGroupId);
            }

            return null;
        }

        public Task<bool> DeleteCourseGroup(Guid courseGroupId)
            => _courseEnrollmentGroupService.UpdateCourseEnrollmentGroup(courseGroupId, false);

        public Task<CourseEnrollmentGroupResponse> GetCourseGroup(Guid groupId)
            => _courseEnrollmentGroupService.GetGroup(groupId);

        public Task<CourseEnrollmentGroupResponse> SetCourseGroupRegistrationStatus(Guid groupId, bool ifRegistrationOpen)
            => _courseEnrollmentGroupService.SetCourseGroupRegistrationStatus(groupId, ifRegistrationOpen);

        public async Task<IEnumerable<CourseEnrollmentGroupResponse>> GetAllCourseGroups(Guid courseId, GetCourseOptions options)
        {
            var groups = await _courseEnrollmentGroupService.GetAllGroups(courseId);
            if (options.IsActive.HasValue)
            {
                groups = groups.Where(g => g.IsActive == options.IsActive.Value).ToList();
            }
            return groups;
        }

        public async Task<bool> SetCourseRegistrationOpenStatus(Guid courseId, bool ifRegistrationOpen)
        {
            // Update all groups registration status
            var groups = await _courseEnrollmentGroupService.GetAllGroups(courseId);
            foreach (var group in groups)
            {
                await _courseEnrollmentGroupService.SetCourseGroupRegistrationStatus(group.CourseEnrollmentGroupId, ifRegistrationOpen);
            }

            // Optional: also update some course-level flag if you track registration at course level
            var course = await _repository.GetCourse(courseId);
            if (course != null)
            {
                course.IsRegistrationOpened = ifRegistrationOpen;
                await _repository.UpdateCourse(courseId, new AddCourse
                {
                    Name = course.Name,
                    NameFr = course.NameFr,
                    Description = course.Description,
                    DescriptionFr = course.DescriptionFr,
                    Details = course.Details,
                    DetailsFr = course.DetailsFr,
                    StartDate = course.StartDate,
                    EndDate = course.EndDate,
                    CanSelectMultipleEnrollmentGroups = course.CanSelectMultipleEnrollmentGroups,
                    PolicyHyperLink = course.PolicyHyperLink,
                    IsCourseCompleted = course.IsCourseCompleted
                });
            }

            return true;
        }


        private AddCourseEnrollmentGroup MapToAdd(UpdateCourseEnrollmentGroup update)
        {
            return new AddCourseEnrollmentGroup
            {
                CourseId = update.CourseId,
                InstituteId = update.InstituteId,
                GroupTitle = update.GroupTitle,
                GroupTitleFr = update.GroupTitleFr,
                Details = update.Details,
                DetailsFr = update.DetailsFr,
                IsActive = update.IsActive,
                MaxStudents = update.MaxStudents,
                Fee = update.Fee,
                IfRegistrationOpen = update.IfRegistrationOpen,
                AcedemicGroups = update.AcedemicGroups?.Select(g => g.ToString()).ToList() ?? new List<string>()
            };
        }
    }
}
