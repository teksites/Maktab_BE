using Courses.Repository;
using Courses.Repository.Implementation;
using Courses.Test.Infrastructure;
using Moq;
using System.Data;
using System.Data.Common;

namespace Courses.Test;

public class CourseRepositoryTests
{
    [Fact]
    public async Task GetCourse_MapsManualEnrollmentAndPrerequisiteFlags()
    {
        var courseId = Guid.NewGuid();
        var database = new FakeDatabase(() => CreateCourseReader(courseId, "54181"));
        var groupRepository = new Mock<ICourseEnrollmentGroupRepository>();
        groupRepository
            .Setup(repo => repo.GetAllGroups(courseId, true))
            .ReturnsAsync(Array.Empty<MaktabDataContracts.Responses.Course.CourseEnrollmentGroupResponse>());

        var repository = new CourseRepository(database, groupRepository.Object);

        var course = await repository.GetCourse(courseId);

        Assert.NotNull(course);
        Assert.True(course!.IsManualEnrollment);
        Assert.True(course.IsCourseHasPrequisite);
    }

    [Fact]
    public async Task GetHelcimTerminalId_ParsesInstituteTerminalId()
    {
        var database = new FakeDatabase(CreateTerminalReader);
        var repository = new CourseRepository(database, Mock.Of<ICourseEnrollmentGroupRepository>());

        var terminalId = await repository.GetHelcimTerminalId(Guid.NewGuid());

        Assert.Equal(54181, terminalId);
    }

    private static DbDataReader CreateCourseReader(Guid courseId, string terminalId)
    {
        var table = new DataTable();
        table.Columns.Add("CourseId", typeof(byte[]));
        table.Columns.Add("InstituteId", typeof(byte[]));
        table.Columns.Add("Name", typeof(string));
        table.Columns.Add("NameFr", typeof(string));
        table.Columns.Add("Description", typeof(string));
        table.Columns.Add("DescriptionFr", typeof(string));
        table.Columns.Add("Details", typeof(string));
        table.Columns.Add("DetailsFr", typeof(string));
        table.Columns.Add("StartDate", typeof(DateTime));
        table.Columns.Add("EndDate", typeof(DateTime));
        table.Columns.Add("IsActive", typeof(bool));
        table.Columns.Add("CreatedAt", typeof(DateTime));
        table.Columns.Add("UpdatedOn", typeof(DateTime));
        table.Columns.Add("CanSelectMultipleEnrollmentGroups", typeof(bool));
        table.Columns.Add("PolicyHyperLink", typeof(string));
        table.Columns.Add("IsCourseCompleted", typeof(bool));
        table.Columns.Add("IsCourseHasPrequisite", typeof(bool));
        table.Columns.Add("IsManualEnrollment", typeof(bool));
        table.Columns.Add("IsRegistrationOpened", typeof(bool));
        table.Columns.Add("RegistrationStartDate", typeof(DateTime));
        table.Columns.Add("RegistrationEndDate", typeof(DateTime));
        table.Columns.Add("CourseSession", typeof(byte));
        table.Columns.Add("RegistrationFee", typeof(int));
        table.Columns.Add("OfferDaycare", typeof(bool));
        table.Columns.Add("TerminalId", typeof(string));

        table.Rows.Add(
            courseId.ToByteArray(),
            Guid.NewGuid().ToByteArray(),
            "Course",
            "Cours",
            "Description",
            "Description fr",
            "Details",
            "Details fr",
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(10),
            true,
            DateTime.UtcNow.AddDays(-2),
            DateTime.UtcNow,
            false,
            string.Empty,
            false,
            true,
            true,
            true,
            DateTime.UtcNow.AddDays(-1),
            DateTime.UtcNow.AddDays(5),
            (byte)0,
            100,
            true,
            terminalId);

        return table.CreateDataReader();
    }

    private static DbDataReader CreateTerminalReader()
    {
        var table = new DataTable();
        table.Columns.Add("TerminalId", typeof(string));
        table.Rows.Add("54181");
        return table.CreateDataReader();
    }
}
