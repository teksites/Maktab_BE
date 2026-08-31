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
    public async Task GetTransactionSimple_IncludesPastAndNextUpcomingInstallmentsInMinimumPayableAfterDueDatePasses()
    {
        var scheduleToday = GetScheduleToday();
        var feeInstallmentsJson = JsonConvert.SerializeObject(new[]
        {
            new { Description = "First", DueDate = GetScheduleDateUtc(scheduleToday.AddDays(-10)), Amount = 100m },
            new { Description = "Second", DueDate = GetScheduleDateUtc(scheduleToday.AddDays(4)), Amount = 50m },
            new { Description = "Third", DueDate = GetScheduleDateUtc(scheduleToday.AddDays(10)), Amount = 25m }
        });

        var database = new FakeDatabase(() => CreateSingleTransactionReader(feeInstallmentsJson, 120m));
        var repository = new StudentCourseTransactionRepository(database);

        var transaction = await repository.GetTransactionSimple(Guid.NewGuid());

        Assert.NotNull(transaction);
        Assert.Equal(30m, transaction.MinimumPayable);
        Assert.Equal(12.5d, transaction.Surcharge);
    }

    [Fact]
    public async Task GetTransactionSimple_OnInstallmentDueDate_DoesNotIncludeNextUpcomingInstallment()
    {
        var scheduleToday = GetScheduleToday();
        var feeInstallmentsJson = JsonConvert.SerializeObject(new[]
        {
            new { Description = "First", DueDate = GetScheduleDateUtc(scheduleToday), Amount = 100m },
            new { Description = "Second", DueDate = GetScheduleDateUtc(scheduleToday.AddDays(8)), Amount = 100m }
        });

        var database = new FakeDatabase(() => CreateSingleTransactionReader(feeInstallmentsJson, 0m));
        var repository = new StudentCourseTransactionRepository(database);

        var transaction = await repository.GetTransactionSimple(Guid.NewGuid());

        Assert.NotNull(transaction);
        Assert.Equal(100m, transaction.MinimumPayable);
    }

    [Fact]
    public async Task GetTransactionSimple_ReturnsNegativeMinimumPayableWhenFamilyIsAheadOfSchedule()
    {
        var scheduleToday = GetScheduleToday();
        var feeInstallmentsJson = JsonConvert.SerializeObject(new[]
        {
            new { Description = "First", DueDate = GetScheduleDateUtc(scheduleToday.AddDays(-7)), Amount = 100m },
            new { Description = "Second", DueDate = GetScheduleDateUtc(scheduleToday.AddDays(3)), Amount = 50m }
        });

        var database = new FakeDatabase(() => CreateSingleTransactionReader(feeInstallmentsJson, 200m));
        var repository = new StudentCourseTransactionRepository(database);

        var transaction = await repository.GetTransactionSimple(Guid.NewGuid());

        Assert.NotNull(transaction);
        Assert.Equal(-50m, transaction.MinimumPayable);
    }

    [Fact]
    public async Task GetAllTransactionsByCourse_PrefersRegisteredParentSourcesWhileKeepingGuardianOtherContacts()
    {
        string? commandText = null;

        var database = new FakeDatabase(
            CreateEmptyReader,
            command => commandText = command.CommandText);

        var repository = new StudentCourseTransactionRepository(database);

        var result = await repository.GetAllTransactionsByCourse(Guid.NewGuid());

        Assert.Empty(result);
        Assert.NotNull(commandText);
        Assert.DoesNotContain("{FamilyInformationJoinSql}", commandText);
        Assert.Contains("tui.Relationship NOT IN (1, 2, 3)", commandText);
        Assert.Contains("NOT EXISTS", commandText);
        Assert.Contains("oci.Relationship NOT IN (1, 2)", commandText);
        Assert.DoesNotContain("oci.Relationship NOT IN (1, 2, 3)", commandText);
    }

    [Fact]
    public async Task GetAllTransactionsByCourse_MapsChildProfileAndHealthFieldsOntoEnrollments()
    {
        var expectedDateOfBirth = new DateTime(2018, 4, 15, 0, 0, 0, DateTimeKind.Utc);
        var expectedRamqExpiry = new DateTime(2027, 12, 31, 0, 0, 0, DateTimeKind.Utc);

        var database = new FakeDatabase(() => CreateCourseTransactionEnrollmentReader(
            expectedDateOfBirth,
            Gender.Female,
            "RAMQ-12345",
            expectedRamqExpiry,
            "Peanuts",
            "Asthma"));

        var repository = new StudentCourseTransactionRepository(database);

        var result = (await repository.GetAllTransactionsByCourse(Guid.NewGuid())).ToList();

        var transaction = Assert.Single(result);
        var enrollment = Assert.Single(transaction.Enrollments);
        Assert.Equal(expectedDateOfBirth, enrollment.DateOfBirth);
        Assert.Equal(Gender.Female, enrollment.Gender);
        Assert.Equal("RAMQ-12345", enrollment.RAMQNumber);
        Assert.Equal(expectedRamqExpiry, enrollment.RAMQExpiry);
        Assert.Equal("Peanuts", enrollment.Allergies);
        Assert.Equal("Asthma", enrollment.OtherHealthConditions);
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
            () => CreateEnrollmentListReader(),
            command => commandText = command.CommandText);

        var repository = new StudentCourseEnrollmentRepository(database);

        var result = await repository.GetAllEnrollmentsByCourse(Guid.NewGuid());

        Assert.Single(result);
        Assert.NotNull(commandText);
        Assert.Contains("CAST(sce.EnrollmentStatus AS SIGNED) AS EnrollmentStatus", commandText);
    }

    [Fact]
    public async Task GetAllEnrollmentsByCourse_PrefersRegisteredFamilyMembersOverTempDuplicates()
    {
        string? commandText = null;

        var database = new FakeDatabase(
            () => CreateEnrollmentListReader(),
            command => commandText = command.CommandText);

        var repository = new StudentCourseEnrollmentRepository(database);

        var result = await repository.GetAllEnrollmentsByCourse(Guid.NewGuid());

        Assert.Single(result);
        Assert.NotNull(commandText);
        Assert.Contains("FROM temp_user_info tui", commandText);
        Assert.Contains("tui.Relationship NOT IN (1, 2, 3)", commandText);
        Assert.Contains("NOT EXISTS", commandText);
    }

    [Fact]
    public async Task GetAllEnrollmentsByCourse_MapsChildProfileFieldsWhenAvailable()
    {
        var expectedDateOfBirth = new DateTime(2017, 6, 10, 0, 0, 0, DateTimeKind.Utc);
        var expectedRamqExpiry = new DateTime(2028, 1, 31, 0, 0, 0, DateTimeKind.Utc);

        var database = new FakeDatabase(() => CreateEnrollmentListReader(
            expectedDateOfBirth,
            Gender.Male,
            "RAMQ-ENROLL-01",
            expectedRamqExpiry,
            "Pollen",
            "Diabetes"));

        var repository = new StudentCourseEnrollmentRepository(database);

        var result = (await repository.GetAllEnrollmentsByCourse(Guid.NewGuid())).ToList();

        var enrollment = Assert.Single(result);
        Assert.Equal(expectedDateOfBirth, enrollment.DateOfBirth);
        Assert.Equal(Gender.Male, enrollment.Gender);
        Assert.Equal("RAMQ-ENROLL-01", enrollment.RAMQNumber);
        Assert.Equal(expectedRamqExpiry, enrollment.RAMQExpiry);
        Assert.Equal("Pollen", enrollment.Allergies);
        Assert.Equal("Diabetes", enrollment.OtherHealthConditions);
    }

    [Fact]
    public async Task GetAllEnrollmentsByFamily_MapsInstituteCourseAndEnrollmentGroupNames()
    {
        var database = new FakeDatabase(() => CreateEnrollmentListReader());
        var repository = new StudentCourseEnrollmentRepository(database);

        var result = (await repository.GetAllEnrollmentsByFamily(Guid.NewGuid())).ToList();

        var enrollment = Assert.Single(result);
        Assert.Equal("ICC Brossard", enrollment.InstituteName);
        Assert.Equal("Evening Quran", enrollment.CourseName);
        Assert.Equal("Coran du soir", enrollment.CourseNameFr);
        Assert.Equal("Group A", enrollment.CourseEnrollmentGroupName);
        Assert.Equal("Groupe A", enrollment.CourseEnrollmentGroupNameFr);
        Assert.Equal("Group A", enrollment.GroupTitle);
        Assert.Equal("Groupe A", enrollment.GroupTitleFr);
    }

    [Fact]
    public async Task GetEnrollmentsForTransaction_MapsChildProfileFieldsWhenAvailable()
    {
        var expectedDateOfBirth = new DateTime(2019, 2, 20, 0, 0, 0, DateTimeKind.Utc);
        var expectedRamqExpiry = new DateTime(2029, 3, 31, 0, 0, 0, DateTimeKind.Utc);

        var database = new FakeDatabase(() => CreateTransactionEnrollmentListReader(
            expectedDateOfBirth,
            Gender.Female,
            "RAMQ-TX-01",
            expectedRamqExpiry,
            "Sesame",
            "Asthma"));

        var repository = new StudentCourseTransactionRepository(database);

        var result = (await repository.GetEnrollmentsForTransaction(Guid.NewGuid())).ToList();

        var enrollment = Assert.Single(result);
        Assert.Equal("Child One", enrollment.ChildName);
        Assert.Equal(expectedDateOfBirth, enrollment.DateOfBirth);
        Assert.Equal(Gender.Female, enrollment.Gender);
        Assert.Equal("RAMQ-TX-01", enrollment.RAMQNumber);
        Assert.Equal(expectedRamqExpiry, enrollment.RAMQExpiry);
        Assert.Equal("Sesame", enrollment.Allergies);
        Assert.Equal("Asthma", enrollment.OtherHealthConditions);
        Assert.Equal("ICC Brossard", enrollment.InstituteName);
        Assert.Equal("Evening Quran", enrollment.CourseName);
        Assert.Equal("Coran du soir", enrollment.CourseNameFr);
        Assert.Equal("Group A", enrollment.CourseEnrollmentGroupName);
        Assert.Equal("Groupe A", enrollment.CourseEnrollmentGroupNameFr);
    }

    [Fact]
    public async Task GetAllTransactionsByCourse_MapsInstituteCourseAndEnrollmentGroupNamesOntoEnrollments()
    {
        var database = new FakeDatabase(() => CreateCourseTransactionEnrollmentReader(
            new DateTime(2018, 4, 15, 0, 0, 0, DateTimeKind.Utc),
            Gender.Female,
            "RAMQ-12345",
            new DateTime(2027, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            "Peanuts",
            "Asthma"));

        var repository = new StudentCourseTransactionRepository(database);

        var result = (await repository.GetAllTransactionsByCourse(Guid.NewGuid())).ToList();

        var transaction = Assert.Single(result);
        var enrollment = Assert.Single(transaction.Enrollments);
        Assert.Equal("ICC Brossard", enrollment.InstituteName);
        Assert.Equal("Evening Quran", enrollment.CourseName);
        Assert.Equal("Coran du soir", enrollment.CourseNameFr);
        Assert.Equal("Morning Group", enrollment.CourseEnrollmentGroupName);
        Assert.Equal("Groupe du matin", enrollment.CourseEnrollmentGroupNameFr);
        Assert.Equal("Morning Group", enrollment.GroupTitle);
        Assert.Equal("Groupe du matin", enrollment.GroupTitleFr);
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
        table.Columns.Add("Surcharge", typeof(double));
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
            12.5d,
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

    private static DateTime GetScheduleToday()
    {
        var timeZone = GetScheduleTimeZone();
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone).Date;
    }

    private static DateTime GetScheduleDateUtc(DateTime scheduleDate)
    {
        var timeZone = GetScheduleTimeZone();
        var localMidnight = DateTime.SpecifyKind(scheduleDate.Date, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(localMidnight, timeZone);
    }

    private static TimeZoneInfo GetScheduleTimeZone()
    {
        foreach (var timeZoneId in new[] { "Eastern Standard Time", "America/Toronto" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return TimeZoneInfo.Utc;
    }

    private static DbDataReader CreateEnrollmentGroupInformationReader(Guid groupId)
    {
        var table = new DataTable();
        table.Columns.Add("CourseEnrollmentGroupId", typeof(byte[]));
        table.Columns.Add("CourseId", typeof(byte[]));
        table.Columns.Add("GroupIndex", typeof(int));
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

        table.Rows.Add(groupBytes, courseId, 1, 3, true, 0, 2, 0, 0, 1, 0);

        return table.CreateDataReader();
    }

    private static DbDataReader CreateEnrollmentListReader(
        DateTime? dateOfBirth = null,
        Gender gender = Gender.Unknown,
        string ramqNumber = "",
        DateTime? ramqExpiry = null,
        string allergies = "",
        string otherHealthConditions = "")
    {
        var table = new DataTable();
        table.Columns.Add("StudentCourseEnrollmentId", typeof(byte[]));
        table.Columns.Add("CourseEnrollmentGroupId", typeof(byte[]));
        table.Columns.Add("CourseId", typeof(byte[]));
        table.Columns.Add("ChildId", typeof(byte[]));
        table.Columns.Add("FamilyId", typeof(byte[]));
        table.Columns.Add("InstituteName", typeof(string));
        table.Columns.Add("CourseName", typeof(string));
        table.Columns.Add("CourseNameFr", typeof(string));
        table.Columns.Add("GroupTitle", typeof(string));
        table.Columns.Add("GroupTitleFr", typeof(string));
        table.Columns.Add("WillUseDayCare", typeof(bool));
        table.Columns.Add("DayCareDays", typeof(int));
        table.Columns.Add("IsActive", typeof(bool));
        table.Columns.Add("CreatedAt", typeof(DateTime));
        table.Columns.Add("UpdatedOn", typeof(DateTime));
        table.Columns.Add("EnrollmentIndex", typeof(int));
        table.Columns.Add("EnrollmentStatus", typeof(int));
        table.Columns.Add("GroupIndex", typeof(int));
        table.Columns.Add("ChildFirstName", typeof(string));
        table.Columns.Add("ChildLastName", typeof(string));
        table.Columns.Add("ChildRegistrationNumber", typeof(string));
        table.Columns.Add("ChildDateOfBirth", typeof(DateTime));
        table.Columns.Add("ChildGender", typeof(int));
        table.Columns.Add("ChildRamqNumber", typeof(string));
        table.Columns.Add("ChildRamqExpiry", typeof(DateTime));
        table.Columns.Add("ChildAllergies", typeof(string));
        table.Columns.Add("ChildOtherHealthConditions", typeof(string));
        table.Columns.Add("ChildConsent", typeof(string));
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
            "ICC Brossard",
            "Evening Quran",
            "Coran du soir",
            "Group A",
            "Groupe A",
            false,
            0,
            true,
            DateTime.UtcNow,
            DateTime.UtcNow,
            1,
            (int)EnrollmentStatus.Cancelled,
            1,
            "Child",
            "One",
            "REG-CHILD-01",
            dateOfBirth ?? DateTime.MinValue,
            (int)gender,
            ramqNumber,
            ramqExpiry ?? DateTime.MinValue,
            allergies,
            otherHealthConditions,
            string.Empty,
            Guid.NewGuid().ToByteArray(),
            "Parent",
            "One",
            "parent@example.com",
            "5551112222",
            1);

        return table.CreateDataReader();
    }

    private static DbDataReader CreateTransactionEnrollmentListReader(
        DateTime dateOfBirth,
        Gender gender,
        string ramqNumber,
        DateTime ramqExpiry,
        string allergies,
        string otherHealthConditions)
    {
        var table = new DataTable();
        table.Columns.Add("StudentCourseEnrollmentId", typeof(byte[]));
        table.Columns.Add("CourseEnrollmentGroupId", typeof(byte[]));
        table.Columns.Add("CourseId", typeof(byte[]));
        table.Columns.Add("FamilyId", typeof(byte[]));
        table.Columns.Add("InstituteName", typeof(string));
        table.Columns.Add("CourseName", typeof(string));
        table.Columns.Add("CourseNameFr", typeof(string));
        table.Columns.Add("GroupTitle", typeof(string));
        table.Columns.Add("GroupTitleFr", typeof(string));
        table.Columns.Add("ChildId", typeof(byte[]));
        table.Columns.Add("IsActive", typeof(bool));
        table.Columns.Add("WillUseDayCare", typeof(bool));
        table.Columns.Add("DayCareDays", typeof(int));
        table.Columns.Add("CreatedAt", typeof(DateTime));
        table.Columns.Add("UpdatedOn", typeof(DateTime));
        table.Columns.Add("GroupIndex", typeof(int));
        table.Columns.Add("EnrollmentIndex", typeof(int));
        table.Columns.Add("EnrollmentStatus", typeof(int));
        table.Columns.Add("ChildFirstName", typeof(string));
        table.Columns.Add("ChildLastName", typeof(string));
        table.Columns.Add("ChildRegistrationNumber", typeof(string));
        table.Columns.Add("ChildDateOfBirth", typeof(DateTime));
        table.Columns.Add("ChildGender", typeof(int));
        table.Columns.Add("ChildRamqNumber", typeof(string));
        table.Columns.Add("ChildRamqExpiry", typeof(DateTime));
        table.Columns.Add("ChildAllergies", typeof(string));
        table.Columns.Add("ChildOtherHealthConditions", typeof(string));
        table.Columns.Add("ChildConsent", typeof(string));

        table.Rows.Add(
            Guid.NewGuid().ToByteArray(),
            Guid.NewGuid().ToByteArray(),
            Guid.NewGuid().ToByteArray(),
            Guid.NewGuid().ToByteArray(),
            "ICC Brossard",
            "Evening Quran",
            "Coran du soir",
            "Group A",
            "Groupe A",
            Guid.NewGuid().ToByteArray(),
            true,
            false,
            0,
            DateTime.UtcNow.AddDays(-1),
            DateTime.UtcNow,
            1,
            1,
            (int)EnrollmentStatus.Enrolled,
            "Child",
            "One",
            "REG-TX-01",
            dateOfBirth,
            (int)gender,
            ramqNumber,
            ramqExpiry,
            allergies,
            otherHealthConditions,
            "Consent");

        return table.CreateDataReader();
    }

    private static DbDataReader CreateCourseTransactionEnrollmentReader(
        DateTime dateOfBirth,
        Gender gender,
        string ramqNumber,
        DateTime ramqExpiry,
        string allergies,
        string otherHealthConditions)
    {
        var table = new DataTable();
        table.Columns.Add("StudentCourseTransactionId", typeof(byte[]));
        table.Columns.Add("FamilyId", typeof(byte[]));
        table.Columns.Add("PayableFee", typeof(decimal));
        table.Columns.Add("DayCareFee", typeof(decimal));
        table.Columns.Add("DayCareDiscount", typeof(int));
        table.Columns.Add("FeeAmountDiscount", typeof(int));
        table.Columns.Add("Surcharge", typeof(double));
        table.Columns.Add("TotalPayable", typeof(decimal));
        table.Columns.Add("Comments", typeof(string));
        table.Columns.Add("FeeInstallmentsJson", typeof(string));
        table.Columns.Add("TransactionStatus", typeof(int));
        table.Columns.Add("RegistrationStatus", typeof(int));
        table.Columns.Add("PaymentCode", typeof(string));
        table.Columns.Add("IsActive", typeof(bool));
        table.Columns.Add("TotalAmountPaid", typeof(decimal));
        table.Columns.Add("IsCompletelyPaid", typeof(bool));
        table.Columns.Add("CreatedAt", typeof(DateTime));
        table.Columns.Add("UpdatedOn", typeof(DateTime));
        table.Columns.Add("StudentCourseEnrollmentId", typeof(byte[]));
        table.Columns.Add("CourseEnrollmentGroupId", typeof(byte[]));
        table.Columns.Add("CourseId", typeof(byte[]));
        table.Columns.Add("InstituteName", typeof(string));
        table.Columns.Add("CourseName", typeof(string));
        table.Columns.Add("CourseNameFr", typeof(string));
        table.Columns.Add("EnrollmentFamilyId", typeof(byte[]));
        table.Columns.Add("ChildId", typeof(byte[]));
        table.Columns.Add("EnrollmentIsActive", typeof(bool));
        table.Columns.Add("WillUseDayCare", typeof(bool));
        table.Columns.Add("DayCareDays", typeof(int));
        table.Columns.Add("EnrollmentCreatedAt", typeof(DateTime));
        table.Columns.Add("EnrollmentUpdatedOn", typeof(DateTime));
        table.Columns.Add("EnrollmentIndex", typeof(int));
        table.Columns.Add("EnrollmentStatus", typeof(int));
        table.Columns.Add("GroupTitle", typeof(string));
        table.Columns.Add("GroupTitleFr", typeof(string));
        table.Columns.Add("GroupIndex", typeof(int));
        table.Columns.Add("ChildFirstName", typeof(string));
        table.Columns.Add("ChildLastName", typeof(string));
        table.Columns.Add("ChildRegistrationNumber", typeof(string));
        table.Columns.Add("ChildDateOfBirth", typeof(DateTime));
        table.Columns.Add("ChildGender", typeof(int));
        table.Columns.Add("ChildRamqNumber", typeof(string));
        table.Columns.Add("ChildRamqExpiry", typeof(DateTime));
        table.Columns.Add("ChildAllergies", typeof(string));
        table.Columns.Add("ChildOtherHealthConditions", typeof(string));
        table.Columns.Add("ParentUserId", typeof(byte[]));
        table.Columns.Add("ParentFirstName", typeof(string));
        table.Columns.Add("ParentLastName", typeof(string));
        table.Columns.Add("ParentEmail", typeof(string));
        table.Columns.Add("ParentPhone", typeof(string));
        table.Columns.Add("ParentRelationship", typeof(int));
        table.Columns.Add("ParentContactType", typeof(int));
        table.Columns.Add("CourseRegistrationFee", typeof(int));

        table.Rows.Add(
            Guid.NewGuid().ToByteArray(),
            Guid.NewGuid().ToByteArray(),
            500m,
            25m,
            0,
            0,
            10d,
            535m,
            "Transaction",
            "[]",
            (int)TransactionStatus.FullyPaid,
            (int)RegistrationStatus.Completed,
            "PAY-COURSE-001",
            true,
            100m,
            false,
            DateTime.UtcNow.AddDays(-2),
            DateTime.UtcNow,
            Guid.NewGuid().ToByteArray(),
            Guid.NewGuid().ToByteArray(),
            Guid.NewGuid().ToByteArray(),
            "ICC Brossard",
            "Evening Quran",
            "Coran du soir",
            Guid.NewGuid().ToByteArray(),
            Guid.NewGuid().ToByteArray(),
            true,
            false,
            0,
            DateTime.UtcNow.AddDays(-2),
            DateTime.UtcNow,
            1,
            (int)EnrollmentStatus.Enrolled,
            "Morning Group",
            "Groupe du matin",
            1,
            "Sara",
            "Ali",
            "REG-001",
            dateOfBirth,
            (int)gender,
            ramqNumber,
            ramqExpiry,
            allergies,
            otherHealthConditions,
            Guid.NewGuid().ToByteArray(),
            "Parent",
            "Ali",
            "parent@example.com",
            "5551234567",
            (int)Relationship.Mother,
            (int)ContactType.Unknown,
            50);

        return table.CreateDataReader();
    }

    private static DbDataReader CreateEmptyReader()
    {
        var table = new DataTable();
        return table.CreateDataReader();
    }
}
