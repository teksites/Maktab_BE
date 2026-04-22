using System.Data;
using System.Data.Common;
using Courses.Repository.Implementation;
using Courses.Test.Infrastructure;
using Newtonsoft.Json;

namespace Courses.Test;

public class StudentCourseTransactionRepositoryTests
{
    [Fact]
    public async Task GetTransactionSimple_IncludesPastAndNextUpcomingInstallmentsInMinimumPayable()
    {
        var today = DateTime.UtcNow.Date;
        var feeInstallmentsJson = JsonConvert.SerializeObject(new[]
        {
            new { Description = "First", DueDate = today.AddDays(-10), Amount = 100m },
            new { Description = "Second", DueDate = today.AddDays(4), Amount = 50m },
            new { Description = "Third", DueDate = today.AddDays(10), Amount = 25m }
        });

        var database = new FakeDatabase(() => CreateSingleTransactionReader(feeInstallmentsJson, 120m));
        var repository = new StudentCourseTransactionRepository(database);

        var transaction = await repository.GetTransactionSimple(Guid.NewGuid());

        Assert.NotNull(transaction);
        Assert.Equal(30m, transaction.MinimumPayable);
    }

    [Fact]
    public async Task GetTransactionSimple_ReturnsNegativeMinimumPayableWhenFamilyIsAheadOfSchedule()
    {
        var today = DateTime.UtcNow.Date;
        var feeInstallmentsJson = JsonConvert.SerializeObject(new[]
        {
            new { Description = "First", DueDate = today.AddDays(-7), Amount = 100m },
            new { Description = "Second", DueDate = today.AddDays(3), Amount = 50m }
        });

        var database = new FakeDatabase(() => CreateSingleTransactionReader(feeInstallmentsJson, 200m));
        var repository = new StudentCourseTransactionRepository(database);

        var transaction = await repository.GetTransactionSimple(Guid.NewGuid());

        Assert.NotNull(transaction);
        Assert.Equal(-50m, transaction.MinimumPayable);
    }

    private static DbDataReader CreateSingleTransactionReader(string feeInstallmentsJson, decimal totalAmountPaid)
    {
        var table = new DataTable();
        table.Columns.Add("StudentCourseTransactionId", typeof(byte[]));
        table.Columns.Add("FamilyId", typeof(byte[]));
        table.Columns.Add("PayableFee", typeof(decimal));
        table.Columns.Add("DayCareFee", typeof(decimal));
        table.Columns.Add("DayCareDiscount", typeof(int));
        table.Columns.Add("FeeAmountDiscount", typeof(int));
        table.Columns.Add("TotalPayable", typeof(decimal));
        table.Columns.Add("TotalAmountPaid", typeof(decimal));
        table.Columns.Add("Comments", typeof(string));
        table.Columns.Add("FeeInstallmentsJson", typeof(string));
        table.Columns.Add("PaymentCode", typeof(string));
        table.Columns.Add("Status", typeof(int));
        table.Columns.Add("RegistrationStatus", typeof(int));
        table.Columns.Add("IsActive", typeof(bool));
        table.Columns.Add("IsCompletelyPaid", typeof(bool));
        table.Columns.Add("CreatedAt", typeof(DateTime));
        table.Columns.Add("UpdatedOn", typeof(DateTime));

        table.Rows.Add(
            Guid.NewGuid().ToByteArray(),
            Guid.NewGuid().ToByteArray(),
            150m,
            0m,
            0,
            0,
            150m,
            totalAmountPaid,
            "Test transaction",
            feeInstallmentsJson,
            "PAY001",
            0,
            0,
            true,
            false,
            DateTime.UtcNow.AddDays(-30),
            DateTime.UtcNow);

        return table.CreateDataReader();
    }
}
