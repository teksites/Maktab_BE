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
        private readonly ICourseEnrollmentGroupService _courseEnrollmentGroupService;
        private readonly IDataAccessVerificationService _dataAccessVerificationService;
        private readonly IUserService _userService;
        private readonly IUserChildrenService _userChildrenService;

        public StudentCourseResultService(
            IStudentCourseResultRepository repository,
            IStudentCourseEnrollmentService studentCourseEnrollmentService,
            IStudentCourseAttendanceRepository studentCourseAttendanceRepository,
            ICourseStaffAssignmentService courseStaffAssignmentService,
            ICourseEnrollmentGroupService courseEnrollmentGroupService,
            IDataAccessVerificationService dataAccessVerificationService,
            IUserService userService,
            IUserChildrenService userChildrenService)
        {
            _repository = repository;
            _studentCourseEnrollmentService = studentCourseEnrollmentService;
            _studentCourseAttendanceRepository = studentCourseAttendanceRepository;
            _courseStaffAssignmentService = courseStaffAssignmentService;
            _courseEnrollmentGroupService = courseEnrollmentGroupService;
            _dataAccessVerificationService = dataAccessVerificationService;
            _userService = userService;
            _userChildrenService = userChildrenService;
        }

        public async Task<IReadOnlyList<StudentCourseResultResponse>> GetFamilyResults(Guid userId, UserRoleType userRoles, Guid familyId, Guid? courseId = null, Guid? courseEnrollmentGroupId = null)
        {
            await EnsureCanAccessFamilyResults(userId, userRoles, familyId).ConfigureAwait(false);
            return await _repository.GetByFamilyId(familyId, courseId, courseEnrollmentGroupId).ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<StudentCourseResultResponse>> GetChildResults(Guid userId, UserRoleType userRoles, Guid childId, Guid? courseId = null, Guid? courseEnrollmentGroupId = null)
        {
            var childResult = await _userChildrenService.GetChild(childId).ConfigureAwait(false);
            var child = childResult?.Result ?? throw new InvalidOperationException("Child was not found.");

            await EnsureCanAccessChildResults(userId, userRoles, child.ChildId, child.FamilyId).ConfigureAwait(false);
            return await _repository.GetByChildId(childId, courseId, courseEnrollmentGroupId).ConfigureAwait(false);
        }

        public async Task<StudentCourseResultResponse?> GetEnrollmentResult(Guid userId, UserRoleType userRoles, Guid studentCourseEnrollmentId)
        {
            var enrollment = await _studentCourseEnrollmentService.GetEnrollment(studentCourseEnrollmentId).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Enrollment was not found.");

            await EnsureCanAccessEnrollment(userId, userRoles, enrollment).ConfigureAwait(false);
            return await _repository.GetByEnrollmentId(studentCourseEnrollmentId).ConfigureAwait(false);
        }

        public async Task<StudentCourseResultResponse> UpsertEnrollmentResult(Guid userId, UserRoleType userRoles, Guid studentCourseEnrollmentId, UpsertStudentCourseResultRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            var enrollment = await _studentCourseEnrollmentService.GetEnrollment(studentCourseEnrollmentId).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Enrollment was not found.");

            await EnsureCanAccessEnrollment(userId, userRoles, enrollment).ConfigureAwait(false);

            var attendanceSummary = await _studentCourseAttendanceRepository.GetAttendanceSummary(studentCourseEnrollmentId).ConfigureAwait(false);
            var attendancePercentage = attendanceSummary.TotalRecords == 0
                ? 0m
                : Math.Round(attendanceSummary.PresentCount * 100m / attendanceSummary.TotalRecords, 2, MidpointRounding.AwayFromZero);
            var group = await _courseEnrollmentGroupService.GetGroup(enrollment.CourseEnrollmentGroupId).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Course enrollment group was not found.");

            return await _repository.Upsert(
                studentCourseEnrollmentId,
                enrollment.ChildId,
                enrollment.FamilyId,
                enrollment.CourseId,
                enrollment.CourseEnrollmentGroupId,
                group.InstituteId,
                attendancePercentage,
                request.ResultStatus,
                request.Remarks ?? string.Empty,
                userId,
                request.IsActive).ConfigureAwait(false);
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

        private async Task EnsureCanAccessChildResults(Guid userId, UserRoleType userRoles, Guid childId, Guid familyId)
        {
            if (_dataAccessVerificationService.HasElevatedAccess(userRoles))
            {
                return;
            }

            if (HasStaffAssignmentRole(userRoles))
            {
                var assignedGroups = await _courseStaffAssignmentService.GetAssignedCourseGroups(userId, onlyActive: true).ConfigureAwait(false);
                foreach (var group in assignedGroups)
                {
                    var enrollments = await _studentCourseEnrollmentService.GetEnrollmentsByGroup(group.CourseEnrollmentGroupId).ConfigureAwait(false);
                    if (enrollments.Any(enrollment => enrollment.ChildId == childId))
                    {
                        return;
                    }
                }

                throw new UnauthorizedAccessException("You are not assigned to a course group for the selected child.");
            }

            var user = await _userService.GetUserInformation(userId).ConfigureAwait(false)
                ?? throw new UnauthorizedAccessException("User information was not found.");

            if (user.FamilyId != familyId)
            {
                throw new UnauthorizedAccessException("The selected child does not belong to your family.");
            }
        }

        private async Task EnsureCanAccessEnrollment(Guid userId, UserRoleType userRoles, StudentCourseEnrollmentResponse enrollment)
        {
            if (_dataAccessVerificationService.HasElevatedAccess(userRoles))
            {
                return;
            }

            if (HasStaffAssignmentRole(userRoles))
            {
                await _courseStaffAssignmentService
                    .GetAssignedCourseGroupRoster(userId, userRoles, enrollment.CourseEnrollmentGroupId)
                    .ConfigureAwait(false);
                return;
            }

            var user = await _userService.GetUserInformation(userId).ConfigureAwait(false)
                ?? throw new UnauthorizedAccessException("User information was not found.");

            if (user.FamilyId != enrollment.FamilyId)
            {
                throw new UnauthorizedAccessException("The selected enrollment does not belong to your family.");
            }
        }

        private static bool HasStaffAssignmentRole(UserRoleType userRoles)
        {
            return userRoles.HasFlag(UserRoleType.Assistant)
                || userRoles.HasFlag(UserRoleType.SchoolTeacher);
        }
    }
}
