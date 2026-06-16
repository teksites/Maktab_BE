using Courses.Repository.Implementation;
using Courses.Test.Infrastructure;
using System.Data;
using System.Data.Common;

namespace Courses.Test;

public class CourseGroupPreRequisiteRepositoryTests
{
    [Fact]
    public async Task GetByCourseGroup_MapsResponses()
    {
        var courseGroupId = Guid.NewGuid();
        var preRequisiteId = Guid.NewGuid();
        var requiredGroupId = Guid.NewGuid();
        var database = new FakeDatabase(() => CreateReader(preRequisiteId, courseGroupId, requiredGroupId));
        var repository = new CourseGroupPreRequisiteRepository(database);

        var results = (await repository.GetByCourseGroup(courseGroupId)).ToList();

        var result = Assert.Single(results);
        Assert.Equal(preRequisiteId, result.CourseGroupPreRequisiteId);
        Assert.Equal(courseGroupId, result.CourseGroupId);
        Assert.Equal(requiredGroupId, result.PreRequisiteCourseGroupId);
        Assert.True(result.IsActive);
    }

    private static DbDataReader CreateReader(Guid preRequisiteId, Guid courseGroupId, Guid requiredGroupId)
    {
        var table = new DataTable();
        table.Columns.Add("CourseGroupPreRequisiteId", typeof(byte[]));
        table.Columns.Add("CourseGroupId", typeof(byte[]));
        table.Columns.Add("PreRequisiteCourseGroupId", typeof(byte[]));
        table.Columns.Add("IsActive", typeof(bool));
        table.Columns.Add("CreatedAt", typeof(DateTime));
        table.Columns.Add("UpdatedOn", typeof(DateTime));

        table.Rows.Add(
            preRequisiteId.ToByteArray(),
            courseGroupId.ToByteArray(),
            requiredGroupId.ToByteArray(),
            true,
            DateTime.UtcNow.AddDays(-2),
            DateTime.UtcNow);

        return table.CreateDataReader();
    }
}
