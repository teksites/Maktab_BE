using Courses.Repository.Implementation;
using Courses.Test.Infrastructure;
using MaktabDataContracts.Enums;
using System.Data;
using System.Data.Common;

namespace Courses.Test;

public class InstituteRepositoryTests
{
    [Fact]
    public async Task GetAllInstitutes_FiltersByInstituteType()
    {
        DbCommand? command = null;
        var database = new FakeDatabase(CreateEmptyReader, executedCommand => command = executedCommand);
        var repository = new InstituteRepository(database);

        await repository.GetAllInstitutes(onlyActive: true, InstituteType.Mosque);

        Assert.NotNull(command);
        Assert.Contains("InstituteType = @InstituteType", command!.CommandText);
        Assert.Equal((int)InstituteType.Mosque, Convert.ToInt32(command.Parameters["@InstituteType"].Value));
    }

    private static DbDataReader CreateEmptyReader()
    {
        var table = new DataTable();
        table.Columns.Add("InstituteId", typeof(byte[]));
        return table.CreateDataReader();
    }
}
