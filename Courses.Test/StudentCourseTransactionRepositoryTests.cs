using System.Data;
using System.Data.Common;
using Courses.Repository.Implementation;
using Courses.Test.Infrastructure;
using MaktabDataContracts.Enums;
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

    [Fact]
    public async Task GetCourseEnrollmentGroupInformation_MapsGroupedStatusesForSpecificGroup()
    {
        var groupId = Guid.Parse("c517c470-6478-4caa-8f02-d8886371277d");
        byte[]? parameterBytes = null;

        var database = new FakeDatabase(
            () => CreateEnrollmentGroupInformationReader(groupId),
            command =>
            {
                parameterBytes = (byte[])((DbParameter)command.Parameters["@CourseGroupId"]).Value!;
            });

        var repository = new StudentCourseEnrollmentRepository(database);

        var result = await repository.GetCourseEnrollmentGroupInformation(groupId);

        Assert.NotNull(result);
        Assert.Equal(groupId, result!.CourseEnrollmentGroupId);
        Assert.Equal(2, result.EnrollmentStatusCount[EnrollmentStatus.Enrolled]);
        Assert.Equal(1, result.EnrollmentStatusCount[EnrollmentStatus.Cancelled]);
        Assert.Equal(groupId.ToByteArray(), parameterBytes);
    }

    [Fact]
    public async Task GetAllEnrollmentsByCourse_CastsEnrollmentStatusToInteger()
    {
        string? commandText = null;

        var database = new FakeDatabase(
            CreateEnrollmentListReader,
            command => commandText = command.CommandText);

        var repository = new StudentCourseEnrollmentRepository(database);

        var result = await repository.GetAllEnrollmentsByCourse(Guid.NewGuid());

        Assert.Single(result);
        Assert.NotNull(commandText);
        Assert.Contains("CAST(sce.EnrollmentStatus AS SIGNED) AS EnrollmentStatus", commandText);
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

    private static DbDataReader CreateEnrollmentGroupInformationReader(Guid groupId)
    {
        var table = new DataTable();
        table.Columns.Add("CourseEnrollmentGroupId", typeof(byte[]));
        table.Columns.Add("CourseId", typeof(byte[]));
        table.Columns.Add("MaxStudents", typeof(int));
        table.Columns.Add("IfRegistrationOpen", typeof(bool));
        table.Columns.Add("UnknownCount", typeof(int));
        table.Columns.Add("EnrolledCount", typeof(int));
        table.Columns.Add("AwaitingCount", typeof(int));
        table.Columns.Add("RegisteredCount", typeof(int));
        table.Columns.Add("CancelledCount", typeof(int));
        table.Columns.Add("RefundedCount", typeof(int));

        var courseId = Guid.NewGuid().ToByteArray();
        var groupBytes = groupId.ToByteArray();

        table.Rows.Add(groupBytes, courseId, 3, true, 0, 2, 0, 0, 1, 0);

        return table.CreateDataReader();
    }

    private static DbDataReader CreateEnrollmentListReader()
    {
        var table = new DataTable();
        table.Columns.Add("StudentCourseEnrollmentId", typeof(byte[]));
        table.Columns.Add("CourseEnrollmentGroupId", typeof(byte[]));
        table.Columns.Add("CourseId", typeof(byte[]));
        table.Columns.Add("ChildId", typeof(byte[]));
        table.Columns.Add("FamilyId", typeof(byte[]));
        table.Columns.Add("WillUseDayCare", typeof(bool));
        table.Columns.Add("DayCareDays", typeof(int));
        table.Columns.Add("IsActive", typeof(bool));
        table.Columns.Add("CreatedAt", typeof(DateTime));
        table.Columns.Add("UpdatedOn", typeof(DateTime));
        table.Columns.Add("EnrollmentIndex", typeof(int));
        table.Columns.Add("EnrollmentStatus", typeof(int));
        table.Columns.Add("ChildFirstName", typeof(string));
        table.Columns.Add("ChildLastName", typeof(string));
        table.Columns.Add("UserId", typeof(byte[]));
        table.Columns.Add("UserFirstName", typeof(string));
        table.Columns.Add("UserLastName", typeof(string));
        table.Columns.Add("Email", typeof(string));
        table.Columns.Add("Phone", typeof(string));
        table.Columns.Add("Relationship", typeof(int));

        table.Rows.Add(
            Guid.NewGuid().ToByteArray(),
            Guid.NewGuid().ToByteArray(),
            Guid.NewGuid().ToByteArray(),
            Guid.NewGuid().ToByteArray(),
            Guid.NewGuid().ToByteArray(),
            false,
            0,
            true,
            DateTime.UtcNow,
            DateTime.UtcNow,
            1,
            (int)EnrollmentStatus.Cancelled,
            "Child",
            "One",
            Guid.NewGuid().ToByteArray(),
            "Parent",
            "One",
            "parent@example.com",
            "5551112222",
            1);

        return table.CreateDataReader();
    }
}
