using Courses.Repository;
using Courses.Services;
using MaktabDataContracts.Enums;
using MaktabDataContracts.Requests.Course;
using MaktabDataContracts.Responses.Course;
using Users.Services;

namespace Courses.Implementation.Services
{
    public class StudentCourseResultService : IStudentCourseResultService
    {
        private readonly IStudentCourseResultRepository _repository;
        private readonly IStudentCourseEnrollmentService _studentCourseEnrollmentService;
        private readonly IStudentCourseAttendanceRepository _studentCourseAttendanceRepository;
        private readonly ICourseStaffAssignmentService _courseStaffAssignmentService;
        private readonly ICourseService _courseService;
        private readonly IDataAccessVerificationService _dataAccessVerificationService;
        private readonly IUserService _userService;
        private readonly IUserChildrenService _userChildrenService;

        public StudentCourseResultService(
            IStudentCourseResultRepository repository,
            IStudentCourseEnrollmentService studentCourseEnrollmentService,
            IStudentCourseAttendanceRepository studentCourseAttendanceRepository,
            ICourseStaffAssignmentService courseStaffAssignmentService,
            ICourseService courseService,
            IDataAccessVerificationService dataAccessVerificationService,
            IUserService userService,
            IUserChildrenService userChildrenService)
        {
            _repository = repository;
            _studentCourseEnrollmentService = studentCourseEnrollmentService;
            _studentCourseAttendanceRepository = studentCourseAttendanceRepository;
            _courseStaffAssignmentService = courseStaffAssignmentService;
            _courseService = courseService;
            _dataAccessVerificationService = dataAccessVerificationService;
            _userService = userService;
            _userChildrenService = userChildrenService;
        }

        public async Task<IReadOnlyList<StudentCourseResultResponse>> GetFamilyResults(Guid userId, UserRoleType userRoles, Guid familyId, Guid? courseId = null)
        {
            await EnsureCanAccessFamilyResults(userId, userRoles, familyId).ConfigureAwait(false);

            if (courseId.HasValue && courseId.Value != Guid.Empty)
            {
                return await BuildFamilyCourseResults(familyId, courseId.Value).ConfigureAwait(false);
            }

            return await _repository.GetByFamilyId(familyId).ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<StudentCourseResultResponse>> GetChildResults(Guid userId, UserRoleType userRoles, Guid childId, Guid? courseId = null)
        {
            var childResult = await _userChildrenService.GetChild(childId).ConfigureAwait(false);
            var child = childResult?.Result ?? throw new InvalidOperationException("Child was not found.");

            if (courseId.HasValue && courseId.Value != Guid.Empty)
            {
                var result = await GetCourseChildResult(userId, userRoles, courseId.Value, childId).ConfigureAwait(false);
                return result == null
                    ? Array.Empty<StudentCourseResultResponse>()
                    : new[] { result };
            }

            if (HasStaffAssignmentRole(userRoles) && !_dataAccessVerificationService.HasElevatedAccess(userRoles))
            {
                throw new InvalidOperationException("CourseId is required when staff request child course results.");
            }

            await EnsureCanAccessFamilyResults(userId, userRoles, child.FamilyId).ConfigureAwait(false);
            return await _repository.GetByChildId(childId).ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<StudentCourseResultResponse>> GetCourseResults(Guid userId, UserRoleType userRoles, Guid courseId)
        {
            await EnsureCanAccessCourse(userId, userRoles, courseId).ConfigureAwait(false);
            return await BuildCourseResults(courseId).ConfigureAwait(false);
        }

        public async Task<StudentCourseResultResponse?> GetCourseChildResult(Guid userId, UserRoleType userRoles, Guid courseId, Guid childId)
        {
            var childResult = await _userChildrenService.GetChild(childId).ConfigureAwait(false);
            var child = childResult?.Result ?? throw new InvalidOperationException("Child was not found.");

            await EnsureCanAccessCourseChild(userId, userRoles, courseId, childId, child.FamilyId).ConfigureAwait(false);

            var courseResults = await BuildCourseResults(courseId).ConfigureAwait(false);
            return courseResults.FirstOrDefault(result => result.ChildId == childId);
        }

        public async Task<StudentCourseResultResponse> UpsertCourseChildResult(Guid userId, UserRoleType userRoles, Guid courseId, Guid childId, UpsertStudentCourseResultRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            var childResult = await _userChildrenService.GetChild(childId).ConfigureAwait(false);
            var child = childResult?.Result ?? throw new InvalidOperationException("Child was not found.");

            await EnsureCanAccessCourseChild(userId, userRoles, courseId, childId, child.FamilyId).ConfigureAwait(false);

            var course = await _courseService.GetCourse(courseId).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Course was not found.");

            var candidateEnrollments = await GetCourseCandidateEnrollments(courseId).ConfigureAwait(false);
            if (!candidateEnrollments.Any(enrollment => enrollment.ChildId == childId))
            {
                throw new InvalidOperationException("The selected child is not an enrolled candidate for this course.");
            }

            var attendanceSummary = await _studentCourseAttendanceRepository.GetAttendanceSummary(courseId, childId).ConfigureAwait(false);
            var attendancePercentage = attendanceSummary.TotalRecords == 0
                ? (decimal?)null
                : Math.Round(attendanceSummary.PresentCount * 100m / attendanceSummary.TotalRecords, 2, MidpointRounding.AwayFromZero);

            return await _repository.Upsert(
                childId,
                child.FamilyId,
                courseId,
                course.InstituteId,
                attendancePercentage,
                request.ResultStatus,
                request.Remarks,
                userId,
                request.IsActive).ConfigureAwait(false);
        }

        private async Task<IReadOnlyList<StudentCourseResultResponse>> BuildFamilyCourseResults(Guid familyId, Guid courseId)
        {
            var courseResults = await BuildCourseResults(courseId).ConfigureAwait(false);
            return courseResults
                .Where(result => result.FamilyId == familyId)
                .OrderBy(result => result.ChildName)
                .ToList();
        }

        private async Task<IReadOnlyList<StudentCourseResultResponse>> BuildCourseResults(Guid courseId)
        {
            var course = await _courseService.GetCourse(courseId).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Course was not found.");

            var candidateEnrollments = await GetCourseCandidateEnrollments(courseId).ConfigureAwait(false);
            var enrollmentsByChild = candidateEnrollments
                .GroupBy(enrollment => enrollment.ChildId)
                .ToDictionary(group => group.Key, group => group.OrderBy(enrollment => enrollment.EnrollmentIndex).ThenBy(enrollment => enrollment.CreatedAt).First());

            var existingResults = await _repository.GetByCourseId(courseId).ConfigureAwait(false);
            var resultByChild = existingResults.ToDictionary(result => result.ChildId, result => result);

            var attendanceSummaries = await _studentCourseAttendanceRepository.GetAttendanceSummariesByCourse(courseId).ConfigureAwait(false);

            var childIds = enrollmentsByChild.Keys
                .Union(resultByChild.Keys)
                .Distinct()
                .ToList();

            var results = new List<StudentCourseResultResponse>(childIds.Count);
            foreach (var childId in childIds)
            {
                enrollmentsByChild.TryGetValue(childId, out var candidateEnrollment);
                resultByChild.TryGetValue(childId, out var existingResult);
                attendanceSummaries.TryGetValue(childId, out var attendanceSummary);

                var hasAttendanceRecords = attendanceSummary.TotalRecords > 0;
                var attendancePercentage = hasAttendanceRecords
                    ? Math.Round(attendanceSummary.PresentCount * 100m / attendanceSummary.TotalRecords, 2, MidpointRounding.AwayFromZero)
                    : (decimal?)null;

                results.Add(new StudentCourseResultResponse
                {
                    StudentCourseResultId = existingResult?.StudentCourseResultId,
                    ChildId = childId,
                    FamilyId = existingResult?.FamilyId ?? candidateEnrollment?.FamilyId ?? Guid.Empty,
                    CourseId = courseId,
                    InstituteId = existingResult?.InstituteId ?? course.InstituteId,
                    CourseName = existingResult?.CourseName ?? course.Name,
                    ChildName = existingResult?.ChildName ?? candidateEnrollment?.ChildName ?? string.Empty,
                    ArabicName = existingResult?.ArabicName ?? candidateEnrollment?.ArabicName ?? string.Empty,
                    RegistrationNumber = existingResult?.RegistrationNumber ?? candidateEnrollment?.RegistrationNumber ?? string.Empty,
                    AttendancePercentage = attendancePercentage,
                    HasAttendanceRecords = hasAttendanceRecords,
                    HasResult = existingResult != null,
                    ResultStatus = existingResult?.ResultStatus ?? StudentCourseResultStatus.Unknown,
                    Remarks = existingResult?.Remarks ?? string.Empty,
                    RecordedByUserId = existingResult?.RecordedByUserId,
                    IsActive = existingResult?.IsActive ?? true,
                    CreatedAt = existingResult?.CreatedAt,
                    UpdatedOn = existingResult?.UpdatedOn
                });
            }

            return results
                .OrderBy(result => result.ChildName)
                .ThenBy(result => result.RegistrationNumber)
                .ToList();
        }

        private async Task<List<StudentCourseEnrollmentResponse>> GetCourseCandidateEnrollments(Guid courseId)
        {
            return (await _studentCourseEnrollmentService.GetAllEnrollments(courseId).ConfigureAwait(false))
                .Where(enrollment => IsCourseResultCandidateStatus(enrollment.EnrollmentStatus))
                .ToList();
        }

        private async Task EnsureCanAccessFamilyResults(Guid userId, UserRoleType userRoles, Guid familyId)
        {
            if (_dataAccessVerificationService.HasElevatedAccess(userRoles))
            {
                return;
            }

            var user = await _userService.GetUserInformation(userId).ConfigureAwait(false)
                ?? throw new UnauthorizedAccessException("User information was not found.");

            if (user.FamilyId != familyId)
            {
                throw new UnauthorizedAccessException("The selected family does not belong to your account.");
            }
        }

        private async Task EnsureCanAccessCourse(Guid userId, UserRoleType userRoles, Guid courseId)
        {
            if (_dataAccessVerificationService.HasElevatedAccess(userRoles))
            {
                return;
            }

            if (!HasStaffAssignmentRole(userRoles))
            {
                throw new UnauthorizedAccessException("Only assigned staff or elevated users can access course-wide results.");
            }

            var assignedGroups = await _courseStaffAssignmentService.GetAssignedCourseGroups(userId, onlyActive: true).ConfigureAwait(false);
            if (!assignedGroups.Any(group => group.CourseId == courseId))
            {
                throw new UnauthorizedAccessException("You are not assigned to the selected course.");
            }
        }

        private async Task EnsureCanAccessCourseChild(Guid userId, UserRoleType userRoles, Guid courseId, Guid childId, Guid familyId)
        {
            if (_dataAccessVerificationService.HasElevatedAccess(userRoles))
            {
                return;
            }

            if (HasStaffAssignmentRole(userRoles))
            {
                var assignedGroups = await _courseStaffAssignmentService.GetAssignedCourseGroups(userId, onlyActive: true).ConfigureAwait(false);
                var assignedCourseGroupIds = assignedGroups
                    .Where(group => group.CourseId == courseId)
                    .Select(group => group.CourseEnrollmentGroupId)
                    .Distinct()
                    .ToList();

                if (!assignedCourseGroupIds.Any())
                {
                    throw new UnauthorizedAccessException("You are not assigned to the selected course.");
                }

                foreach (var groupId in assignedCourseGroupIds)
                {
                    var enrollments = await _studentCourseEnrollmentService.GetEnrollmentsByGroup(groupId).ConfigureAwait(false);
                    if (enrollments.Any(enrollment => enrollment.ChildId == childId && IsCourseResultCandidateStatus(enrollment.EnrollmentStatus)))
                    {
                        return;
                    }
                }

                throw new UnauthorizedAccessException("You are not assigned to a course group for the selected child in this course.");
            }

            var user = await _userService.GetUserInformation(userId).ConfigureAwait(false)
                ?? throw new UnauthorizedAccessException("User information was not found.");

            if (user.FamilyId != familyId)
            {
                throw new UnauthorizedAccessException("The selected child does not belong to your family.");
            }
        }

        private static bool HasStaffAssignmentRole(UserRoleType userRoles)
        {
            return userRoles.HasFlag(UserRoleType.Assistant)
                || userRoles.HasFlag(UserRoleType.SchoolTeacher);
        }

        private static bool IsCourseResultCandidateStatus(EnrollmentStatus status)
        {
            return status == EnrollmentStatus.Enrolled
                || status == EnrollmentStatus.Registered;
        }
    }
}
