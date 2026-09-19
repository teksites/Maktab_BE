using MaktabDataContracts.Enums;
using MaktabDataContracts.Helpers;

namespace Courses.Test;

public class UserRoleTypeTests
{
    [Fact]
    public void AssistantFlag_DoesNotOverlapExistingRoles()
    {
        var roles = UserRoleType.Assistant | UserRoleType.SchoolTeacher | UserRoleType.Admin;

        Assert.True(UserRoleHelper.HasRole(roles, UserRoleType.Assistant));
        Assert.True(UserRoleHelper.HasRole(roles, UserRoleType.SchoolTeacher));
        Assert.True(UserRoleHelper.HasRole(roles, UserRoleType.Admin));
        Assert.False(UserRoleHelper.HasRole(roles, UserRoleType.SchoolSupervisor));
    }

    [Fact]
    public void UserRoleValues_AreStableLongBitFlags()
    {
        Assert.Equal(1L, (long)UserRoleType.Normal);
        Assert.Equal(2L, (long)UserRoleType.Assistant);
        Assert.Equal(4L, (long)UserRoleType.SchoolTeacher);
        Assert.Equal(8L, (long)UserRoleType.SchoolSupervisor);
        Assert.Equal(16L, (long)UserRoleType.SchoolAdmin);
        Assert.Equal(32L, (long)UserRoleType.Admin);
        Assert.Equal(64L, (long)UserRoleType.SuperUser);
        Assert.False(Enum.IsDefined(typeof(UserRoleType), "Manager"));
    }

    [Fact]
    public void LegacyRoleMaskMigration_PreservesCombinedRoles()
    {
        const long legacyNormalTeacherAdmin = 1 | 2 | 64;
        var migrated = (legacyNormalTeacherAdmin & 1) | ((legacyNormalTeacherAdmin & 126) << 1);

        Assert.Equal(133L, migrated);
    }

    [Fact]
    public void SuperUserHierarchyMigration_ReclassifiesLegacyElevatedRoles()
    {
        const long previousSuperUser = 32;
        const long previousManager = 64;
        const long previousAdmin = 128;

        long Migrate(long roles) =>
            (roles & 31)
            | ((roles & (previousSuperUser | previousManager)) != 0 ? 16 : 0)
            | ((roles & previousAdmin) != 0 ? 64 : 0);

        Assert.Equal(UserRoleType.SuperUser, (UserRoleType)Migrate(previousAdmin));
        Assert.Equal(UserRoleType.SchoolAdmin, (UserRoleType)Migrate(previousSuperUser));
        Assert.Equal(UserRoleType.SchoolAdmin, (UserRoleType)Migrate(previousManager));
    }
}
