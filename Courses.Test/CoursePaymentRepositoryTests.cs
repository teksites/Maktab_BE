using Courses.Repository.Implementation;
using Courses.Test.Infrastructure;
using MaktabDataContracts.Enums;
using System.Data;
using System.Data.Common;

namespace Courses.Test;

public class CoursePaymentRepositoryTests
{
    [Fact]
    public async Task GetPayment_MapsPaymentType()
    {
        var paymentId = Guid.NewGuid();
        var database = new FakeDatabase(() => CreatePaymentReader(paymentId));
        var repository = new CoursePaymentRepository(database);

        var payment = await repository.GetPayment(paymentId);

        Assert.NotNull(payment);
        Assert.Equal(PaymentType.Refund, payment!.PaymentType);
        Assert.Equal(PaymentMode.Helcim, payment.PaymentMode);
    }

    [Fact]
    public async Task GetPayment_DefaultsPaymentTypeToCreditForExistingData()
    {
        var paymentId = Guid.NewGuid();
        var database = new FakeDatabase(() => CreatePaymentReader(paymentId, DBNull.Value));
        var repository = new CoursePaymentRepository(database);

        var payment = await repository.GetPayment(paymentId);

        Assert.NotNull(payment);
        Assert.Equal(PaymentType.Credit, payment!.PaymentType);
    }

    private static DbDataReader CreatePaymentReader(Guid paymentId, object? paymentType = null)
    {
        var table = new DataTable();
        table.Columns.Add("CoursePaymentId", typeof(byte[]));
        table.Columns.Add("StudentCourseTransactionId", typeof(byte[]));
        table.Columns.Add("FamilyId", typeof(byte[]));
        table.Columns.Add("AmountPaid", typeof(decimal));
        table.Columns.Add("Comments", typeof(string));
        table.Columns.Add("ExternalPaymentId", typeof(string));
        table.Columns.Add("PaymentType", typeof(int));
        table.Columns.Add("PaymentMode", typeof(int));
        table.Columns.Add("IsActive", typeof(bool));
        table.Columns.Add("CreatedAt", typeof(DateTime));
        table.Columns.Add("UpdatedOn", typeof(DateTime));

        table.Rows.Add(
            paymentId.ToByteArray(),
            Guid.NewGuid().ToByteArray(),
            Guid.NewGuid().ToByteArray(),
            10m,
            "Refunded",
            "ext-1",
            paymentType ?? (int)PaymentType.Refund,
            (int)PaymentMode.Helcim,
            true,
            DateTime.UtcNow.AddDays(-1),
            DateTime.UtcNow);

        return table.CreateDataReader();
    }
}
