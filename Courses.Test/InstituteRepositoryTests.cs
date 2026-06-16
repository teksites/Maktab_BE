using Courses.Repository.Implementation;
using Courses.Test.Infrastructure;
using System.Data;
using System.Data.Common;

namespace Courses.Test;

public class InstituteRepositoryTests
{
    [Fact]
    public async Task GetInstitute_MapsTerminalId()
    {
        var instituteId = Guid.NewGuid();
        var database = new FakeDatabase(() => CreateInstituteReader(instituteId));
        var repository = new InstituteRepository(database);

        var institute = await repository.GetInstitute(instituteId);

        Assert.NotNull(institute);
        Assert.Equal("54181", institute!.TerminalId);
    }

    private static DbDataReader CreateInstituteReader(Guid instituteId)
    {
        var table = new DataTable();
        table.Columns.Add("InstituteId", typeof(byte[]));
        table.Columns.Add("Name", typeof(string));
        table.Columns.Add("NameFr", typeof(string));
        table.Columns.Add("Description", typeof(string));
        table.Columns.Add("DescriptionFr", typeof(string));
        table.Columns.Add("Email", typeof(string));
        table.Columns.Add("Phone", typeof(string));
        table.Columns.Add("TerminalId", typeof(string));
        table.Columns.Add("IsActive", typeof(bool));
        table.Columns.Add("CreatedAt", typeof(DateTime));
        table.Columns.Add("UpdatedOn", typeof(DateTime));

        table.Rows.Add(
            instituteId.ToByteArray(),
            "Institute",
            "Institut",
            "Description",
            "Description fr",
            "school@example.com",
            "5551112222",
            "54181",
            true,
            DateTime.UtcNow.AddDays(-5),
            DateTime.UtcNow);

        return table.CreateDataReader();
    }
}
