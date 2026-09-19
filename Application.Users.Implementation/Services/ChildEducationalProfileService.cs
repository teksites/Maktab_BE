using Courses.Services;
using MaktabDataContracts.Enums;
using MaktabDataContracts.Helpers;
using MaktabDataContracts.Requests.Children;
using MaktabDataContracts.Responses.Children;
using Users.Repository;
using Users.Services;

namespace Application.Users.Implementation
{
    public class ChildEducationalProfileService : IChildEducationalProfileService
    {
        private readonly IUserChildrenRepository _userChildrenRepository;
        private readonly IUserService _userService;
        private readonly IDataAccessVerificationService _dataAccessVerificationService;
        private readonly ICourseStaffAssignmentService _courseStaffAssignmentService;
        private readonly IStudentCourseEnrollmentService _studentCourseEnrollmentService;

        public ChildEducationalProfileService(
            IUserChildrenRepository userChildrenRepository,
            IUserService userService,
            IDataAccessVerificationService dataAccessVerificationService,
            ICourseStaffAssignmentService courseStaffAssignmentService,
            IStudentCourseEnrollmentService studentCourseEnrollmentService)
        {
            _userChildrenRepository = userChildrenRepository;
            _userService = userService;
            _dataAccessVerificationService = dataAccessVerificationService;
            _courseStaffAssignmentService = courseStaffAssignmentService;
            _studentCourseEnrollmentService = studentCourseEnrollmentService;
        }

        public IReadOnlyList<QuranSurahOptionResponse> GetQuranSurahOptions()
        {
            return QuranSurahCatalog.GetAllOptions();
        }

        public async Task<ChildEducationalProfileResponse?> GetChildEducationalProfile(Guid userId, UserRoleType userRoles, Guid childId)
        {
            var child = await _userChildrenRepository.GetChild(childId).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Child was not found.");

            await EnsureCanAccessChildProfile(userId, userRoles, child.ChildId, child.FamilyId).ConfigureAwait(false);
            return await _userChildrenRepository.GetChildEducationalProfile(childId).ConfigureAwait(false);
        }

        public async Task<ChildEducationalProfileResponse> UpsertChildEducationalProfile(Guid userId, UserRoleType userRoles, Guid childId, UpsertChildEducationalProfileRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            var child = await _userChildrenRepository.GetChild(childId).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Child was not found.");

            if (request.FamilyId != Guid.Empty && request.FamilyId != child.FamilyId)
            {
                throw new InvalidOperationException("FamilyId does not match the selected child.");
            }

            await EnsureCanAccessChildProfile(userId, userRoles, child.ChildId, child.FamilyId).ConfigureAwait(false);

            if (!_dataAccessVerificationService.HasElevatedAccess(userRoles)
                && !HasStaffAssignmentRole(userRoles)
                && child.HasSurahCatalogBeenProvided)
            {
                throw new UnauthorizedAccessException("The surah catalog was already provided and can only be updated by school staff.");
            }

            var surahAssessments = NormalizeSurahAssessments(request.SurahAssessments);

            return await _userChildrenRepository
                .UpsertChildEducationalProfile(
                    childId,
                    child.FamilyId,
                    surahAssessments)
                .ConfigureAwait(false);
        }

        private static IReadOnlyCollection<QuranSurahAssessmentRequest> NormalizeSurahAssessments(
            IEnumerable<QuranSurahAssessmentRequest>? assessments)
        {
            var normalized = (assessments ?? Enumerable.Empty<QuranSurahAssessmentRequest>())
                .Where(item => item != null)
                .Select(item => new QuranSurahAssessmentRequest
                {
                    Surah = item.Surah,
                    CompletionStatus = item.CompletionStatus,
                    Remarks = (item.Remarks ?? string.Empty).Trim()
                })
                .OrderBy(item => (int)item.Surah)
                .ToList();

            if (normalized.Any(item => !Enum.IsDefined(typeof(QuranSurah), item.Surah)))
            {
                throw new ArgumentException("Each assessment must contain a valid surah.", nameof(assessments));
            }

            if (normalized.GroupBy(item => item.Surah).Any(group => group.Count() > 1))
            {
                throw new ArgumentException("A surah can only be assessed once per profile.", nameof(assessments));
            }

            if (normalized.Any(item => !Enum.IsDefined(typeof(SurahCompletionStatus), item.CompletionStatus)))
            {
                throw new ArgumentException("Surah completion status is invalid.", nameof(assessments));
            }

            if (normalized.Any(item => item.Remarks.Length > 500))
            {
                throw new ArgumentException("Surah remarks cannot exceed 500 characters per surah.", nameof(assessments));
            }

            return normalized;
        }

        private async Task EnsureCanAccessChildProfile(Guid userId, UserRoleType userRoles, Guid childId, Guid familyId)
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

        private static bool HasStaffAssignmentRole(UserRoleType userRoles)
        {
            return userRoles.HasFlag(UserRoleType.Assistant)
                || userRoles.HasFlag(UserRoleType.SchoolTeacher);
        }
    }
}
