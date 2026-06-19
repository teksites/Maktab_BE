using Courses.Repository.Implementation;
using Courses.Test.Infrastructure;
using System.Data;
using System.Data.Common;

namespace Courses.Test;

public class CourseEnrollmentGroupRepositoryTests
{
    [Fact]
    public async Task GetGroup_MapsAgeAndPrerequisiteFields()
    {
        var groupId = Guid.NewGuid();
        var database = new FakeDatabase(() => CreateGroupReader(groupId));
        var repository = new CourseEnrollmentGroupRepository(database);

        var group = await repository.GetGroup(groupId);

        Assert.NotNull(group);
        Assert.Equal(5, group!.MinAge);
        Assert.Equal(12, group.MaxAge);
        Assert.True(group.IsCourseGroupHasPrequisite);
    }

    private static DbDataReader CreateGroupReader(Guid groupId)
    {
        var table = new DataTable();
        table.Columns.Add("CourseEnrollmentGroupId", typeof(byte[]));
        table.Columns.Add("CourseId", typeof(byte[]));
        table.Columns.Add("InstituteId", typeof(byte[]));
        table.Columns.Add("GroupTitle", typeof(string));
        table.Columns.Add("GroupTitleFr", typeof(string));
        table.Columns.Add("Details", typeof(string));
        table.Columns.Add("DetailsFr", typeof(string));
        table.Columns.Add("IsActive", typeof(bool));
        table.Columns.Add("CreatedAt", typeof(DateTime));
        table.Columns.Add("UpdatedOn", typeof(DateTime));
        table.Columns.Add("MaxStudents", typeof(int));
        table.Columns.Add("AcedemicGroup", typeof(int));
        table.Columns.Add("Fee", typeof(int));
        table.Columns.Add("DayCareFee", typeof(int));
        table.Columns.Add("IfRegistrationOpen", typeof(bool));
        table.Columns.Add("GroupIndex", typeof(int));
        table.Columns.Add("MinAge", typeof(int));
        table.Columns.Add("MaxAge", typeof(int));
        table.Columns.Add("IsCourseGroupHasPrequisite", typeof(bool));

        table.Rows.Add(
            groupId.ToByteArray(),
            Guid.NewGuid().ToByteArray(),
            Guid.NewGuid().ToByteArray(),
            "Group",
            "Groupe",
            "Details",
            "Details fr",
            true,
            DateTime.UtcNow.AddDays(-3),
            DateTime.UtcNow,
            20,
            0,
            250,
            50,
            true,
            1,
            5,
            12,
            true);

        return table.CreateDataReader();
    }
}
