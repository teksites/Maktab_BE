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

            var completedSurahs = (request.CompletedSurahs ?? new List<QuranSurahSelectionRequest>())
                .Select(item => item?.Surah ?? 0)
                .Where(surah => Enum.IsDefined(typeof(QuranSurah), surah))
                .Distinct()
                .OrderBy(surah => (int)surah)
                .ToList();

            return await _userChildrenRepository
                .UpsertChildEducationalProfile(childId, child.FamilyId, completedSurahs)
                .ConfigureAwait(false);
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
