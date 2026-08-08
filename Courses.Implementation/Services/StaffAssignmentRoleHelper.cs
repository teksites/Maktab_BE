using MaktabDataContracts.Enums;

namespace Courses.Implementation.Services
{
    internal static class StaffAssignmentRoleHelper
    {
        internal const UserRoleType AllowedInstituteStaffRoles =
            UserRoleType.Assistant |
            UserRoleType.SchoolTeacher |
            UserRoleType.SchoolSupervisor |
            UserRoleType.SchoolAdmin |
            UserRoleType.Manager;

        internal const UserRoleType AllowedCourseAssignmentRoles =
            UserRoleType.Assistant |
            UserRoleType.SchoolTeacher;

        internal const UserRoleType EligibleGlobalStaffCandidateRoles =
            UserRoleType.Assistant |
            UserRoleType.SchoolTeacher |
            UserRoleType.SchoolSupervisor |
            UserRoleType.SchoolAdmin |
            UserRoleType.SuperUser |
            UserRoleType.Manager |
            UserRoleType.Admin;

        internal const UserRoleType ElevatedAccessRoles =
            UserRoleType.SchoolSupervisor |
            UserRoleType.SchoolAdmin |
            UserRoleType.SuperUser |
            UserRoleType.Manager |
            UserRoleType.Admin;

        internal static bool IsValidInstituteStaffRoleMask(UserRoleType roles)
            => roles != UserRoleType.None && (roles & ~AllowedInstituteStaffRoles) == UserRoleType.None;

        internal static bool IsValidCourseAssignmentRoleMask(UserRoleType roles)
            => roles != UserRoleType.None && (roles & ~AllowedCourseAssignmentRoles) == UserRoleType.None;

        internal static bool IsEligibleCandidate(UserRoleType globalRoles)
            => (globalRoles & EligibleGlobalStaffCandidateRoles) != UserRoleType.None;

        internal static bool HasElevatedAccess(UserRoleType roles)
            => (roles & ElevatedAccessRoles) != UserRoleType.None;

        internal static bool CanCoverRequestedRoles(UserRoleType availableRoles, UserRoleType requestedRoles)
        {
            if (requestedRoles == UserRoleType.None)
            {
                return true;
            }

            return GetHighestRoleLevel(availableRoles) >= GetHighestRoleLevel(requestedRoles);
        }

        internal static UserRoleType CombineRoles(IEnumerable<UserRoleType> roles)
        {
            var combined = UserRoleType.None;
            foreach (var role in roles)
            {
                combined |= role;
            }

            return combined;
        }

        internal static bool RangesOverlap(DateTime start1, DateTime? end1, DateTime start2, DateTime? end2)
        {
            var normalizedEnd1 = end1 ?? DateTime.MaxValue;
            var normalizedEnd2 = end2 ?? DateTime.MaxValue;
            return start1 <= normalizedEnd2 && start2 <= normalizedEnd1;
        }

        internal static DateTime NormalizeUtc(DateTime value)
            => value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);

        private static int GetHighestRoleLevel(UserRoleType roles)
        {
            var highest = 0;
            foreach (UserRoleType role in Enum.GetValues(typeof(UserRoleType)))
            {
                if (role == UserRoleType.None || !roles.HasFlag(role))
                {
                    continue;
                }

                highest = Math.Max(highest, GetRoleLevel(role));
            }

            return highest;
        }

        private static int GetRoleLevel(UserRoleType role) => role switch
        {
            UserRoleType.Normal => 1,
            UserRoleType.Assistant => 2,
            UserRoleType.SchoolTeacher => 3,
            UserRoleType.SchoolSupervisor => 4,
            UserRoleType.SchoolAdmin => 5,
            UserRoleType.SuperUser => 6,
            UserRoleType.Manager => 7,
            UserRoleType.Admin => 8,
            _ => 0
        };
    }
}
