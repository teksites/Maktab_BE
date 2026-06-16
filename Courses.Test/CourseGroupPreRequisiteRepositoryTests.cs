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

    [Fact]
    public async Task GetByEnrollment_ReturnsEmptyListWhenNoPrerequisitesExist()
    {
        var database = new FakeDatabase(CreateEmptyReader);
        var repository = new CourseGroupPreRequisiteRepository(database);

        var results = (await repository.GetByEnrollment(Guid.NewGuid())).ToList();

        Assert.Empty(results);
    }

    [Fact]
    public async Task GetByEnrollment_MapsResponses()
    {
        var enrollmentId = Guid.NewGuid();
        var courseGroupId = Guid.NewGuid();
        var preRequisiteId = Guid.NewGuid();
        var requiredGroupId = Guid.NewGuid();
        DbCommand? executedCommand = null;
        var database = new FakeDatabase(
            () => CreateReader(preRequisiteId, courseGroupId, requiredGroupId),
            command => executedCommand = command);
        var repository = new CourseGroupPreRequisiteRepository(database);

        var results = (await repository.GetByEnrollment(enrollmentId)).ToList();

        var result = Assert.Single(results);
        Assert.NotNull(executedCommand);
        Assert.Contains("student_course_enrollment", executedCommand!.CommandText, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(preRequisiteId, result.CourseGroupPreRequisiteId);
        Assert.Equal(courseGroupId, result.CourseGroupId);
        Assert.Equal(requiredGroupId, result.PreRequisiteCourseGroupId);
        Assert.True(result.IsActive);
    }

    [Fact]
    public async Task Update_ReturnsTrueWhenRowIsAffected()
    {
        var prerequisiteId = Guid.NewGuid();
        var request = new MaktabDataContracts.Requests.Course.AddCourseGroupPreRequisite
        {
            CourseGroupPreRequisiteId = prerequisiteId,
            CourseGroupId = Guid.NewGuid(),
            PreRequisiteCourseGroupId = Guid.NewGuid()
        };
        DbCommand? executedCommand = null;
        var database = new FakeDatabase(CreateEmptyReader, command => executedCommand = command, executeNonQueryResult: 1);
        var repository = new CourseGroupPreRequisiteRepository(database);

        var result = await repository.Update(prerequisiteId, request);

        Assert.True(result);
        Assert.NotNull(executedCommand);
        Assert.Contains("UPDATE course_group_prerequisites", executedCommand!.CommandText, StringComparison.OrdinalIgnoreCase);
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

    private static DbDataReader CreateEmptyReader()
    {
        var table = new DataTable();
        table.Columns.Add("CourseGroupPreRequisiteId", typeof(byte[]));
        table.Columns.Add("CourseGroupId", typeof(byte[]));
        table.Columns.Add("PreRequisiteCourseGroupId", typeof(byte[]));
        table.Columns.Add("IsActive", typeof(bool));
        table.Columns.Add("CreatedAt", typeof(DateTime));
        table.Columns.Add("UpdatedOn", typeof(DateTime));
        return table.CreateDataReader();
    }
}
