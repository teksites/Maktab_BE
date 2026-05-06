using System.Data;
using System.Data.Common;
using Courses.Test.Infrastructure;
using MaktabDataContracts.Enums.Helcim;
using Helcim.Repository.Implementation;

namespace Courses.Test;

public class HelcimTransactionRepositoryTests
{
    [Fact]
    public async Task GetByPaymentCode_OrdersByInvoiceNumberAscending()
    {
        string? commandText = null;

        var database = new FakeDatabase(
            CreateEmptyReader,
            command => commandText = command.CommandText);

        var repository = new HelcimTransactionRepository(database);

        var result = await repository.GetByPaymentCode("PAY001");

        Assert.Empty(result);
        Assert.NotNull(commandText);
        Assert.Contains("ORDER BY InvoiceNumber ASC, TransactionId ASC", commandText);
    }

    [Fact]
    public async Task GetDetailedByPaymentCode_MapsRawResponseAndTransactionResponse()
    {
        var database = new FakeDatabase(CreateDetailedReader);
        var repository = new HelcimTransactionRepository(database);

        var result = await repository.GetDetailedByPaymentCode("PAY001");

        var item = Assert.Single(result);
        Assert.Equal("PAY001", item.PaymentCode);
        Assert.Equal(Guid.Parse("11111111-1111-1111-1111-111111111111"), item.MaktabTransactionId);
        Assert.Equal("INV-PAY001-202605052141-1", item.InvoiceNumber);
        Assert.Equal(47889842, item.TransactionId);
        Assert.Equal("[{\"invoiceId\":63677011}]", item.RawResponse);
        Assert.Equal("{\"transactionId\":47889842}", item.TransactionResponse);
        Assert.Equal(HelcimCurrency.Cad, item.Currency);
        Assert.Equal(HelcimInvoiceStatus.Paid, item.InvoiceStatus);
        Assert.Equal(HelcimCardTransactionStatus.Approved, item.CardTransactionStatus);
        Assert.Equal(HelcimInvoiceType.Invoice, item.InvoiceType);
        Assert.Equal(HelcimCardTransactionType.Purchase, item.CardTransactionType);
    }

    private static DbDataReader CreateEmptyReader()
    {
        var table = new DataTable();
        return table.CreateDataReader();
    }

    private static DbDataReader CreateDetailedReader()
    {
        var table = new DataTable();
        table.Columns.Add("PaymentCode", typeof(string));
        table.Columns.Add("MaktabTransactionId", typeof(byte[]));
        table.Columns.Add("UserIp", typeof(string));
        table.Columns.Add("InvoiceId", typeof(int));
        table.Columns.Add("InvoiceNumber", typeof(string));
        table.Columns.Add("InvoiceToken", typeof(string));
        table.Columns.Add("CustomerId", typeof(int));
        table.Columns.Add("CustomerCode", typeof(string));
        table.Columns.Add("TransactionId", typeof(int));
        table.Columns.Add("CardBatchId", typeof(int));
        table.Columns.Add("User", typeof(string));
        table.Columns.Add("ApprovalCode", typeof(string));
        table.Columns.Add("CardToken", typeof(string));
        table.Columns.Add("CardNumber", typeof(string));
        table.Columns.Add("CardHolderName", typeof(string));
        table.Columns.Add("CardType", typeof(string));
        table.Columns.Add("AvsResponse", typeof(string));
        table.Columns.Add("CvvResponse", typeof(string));
        table.Columns.Add("Warning", typeof(string));
        table.Columns.Add("Amount", typeof(decimal));
        table.Columns.Add("AmountPaid", typeof(decimal));
        table.Columns.Add("Currency", typeof(int));
        table.Columns.Add("InvoiceStatus", typeof(int));
        table.Columns.Add("CardTransactionStatus", typeof(int));
        table.Columns.Add("InvoiceType", typeof(int));
        table.Columns.Add("CardTransactionType", typeof(int));
        table.Columns.Add("CreatedAt", typeof(DateTime));
        table.Columns.Add("UpdatedOn", typeof(DateTime));
        table.Columns.Add("DatePaid", typeof(DateTime));
        table.Columns.Add("IsActive", typeof(bool));
        table.Columns.Add("RawResponse", typeof(string));
        table.Columns.Add("TransactionResponse", typeof(string));

        table.Rows.Add(
            "PAY001",
            Guid.Parse("11111111-1111-1111-1111-111111111111").ToByteArray(),
            "127.0.0.1",
            63677011,
            "INV-PAY001-202605052141-1",
            "invoice-token",
            40499452,
            "CST1010",
            47889842,
            6429263,
            "Helcim System",
            "T8E7ST",
            "card-token",
            "5413330011",
            "malik ten",
            "MC",
            "X",
            "M",
            string.Empty,
            99m,
            99m,
            (int)HelcimCurrency.Cad,
            (int)HelcimInvoiceStatus.Paid,
            (int)HelcimCardTransactionStatus.Approved,
            (int)HelcimInvoiceType.Invoice,
            (int)HelcimCardTransactionType.Purchase,
            new DateTime(2026, 5, 3, 11, 42, 8, DateTimeKind.Utc),
            new DateTime(2026, 5, 3, 11, 42, 9, DateTimeKind.Utc),
            new DateTime(2026, 5, 3, 11, 42, 9, DateTimeKind.Utc),
            true,
            "[{\"invoiceId\":63677011}]",
            "{\"transactionId\":47889842}");

        return table.CreateDataReader();
    }
}
