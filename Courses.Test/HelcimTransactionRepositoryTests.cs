using System.Data;
using System.Data.Common;
using Courses.Test.Infrastructure;
using MaktabDataContracts.Enums.Helcim;
using MaktabDataContracts.Requests.Helcim;
using Helcim.Repository.Implementation;

namespace Courses.Test;

public class HelcimTransactionRepositoryTests
{
    [Fact]
    public async Task GetByPaymentCode_OrdersByInvoiceNumberDescending()
    {
        string? commandText = null;

        var database = new FakeDatabase(
            CreateEmptyReader,
            command => commandText = command.CommandText);

        var repository = new HelcimTransactionRepository(database);

        var result = await repository.GetByPaymentCode("PAY001");

        Assert.Empty(result);
        Assert.NotNull(commandText);
        Assert.Contains("ORDER BY InvoiceNumber DESC, TransactionId DESC", commandText);
    }

    [Fact]
    public async Task GetByFamilyId_OrdersByInvoiceNumberDescending()
    {
        string? commandText = null;

        var database = new FakeDatabase(
            CreateEmptyReader,
            command => commandText = command.CommandText);

        var repository = new HelcimTransactionRepository(database);

        var result = await repository.GetByFamilyId(Guid.Parse("22222222-2222-2222-2222-222222222222"));

        Assert.Empty(result);
        Assert.NotNull(commandText);
        Assert.Contains("ORDER BY InvoiceNumber DESC, TransactionId DESC", commandText);
    }

    [Fact]
    public async Task GetDetailedByPaymentCode_MapsRawResponseTransactionResponseAndFamilyId()
    {
        var database = new FakeDatabase(() => CreateDetailedReader());
        var repository = new HelcimTransactionRepository(database);

        var result = await repository.GetDetailedByPaymentCode("PAY001");

        var item = Assert.Single(result);
        Assert.Equal("PAY001", item.PaymentCode);
        Assert.Equal(Guid.Parse("11111111-1111-1111-1111-111111111111"), item.MaktabTransactionId);
        Assert.Equal(Guid.Parse("22222222-2222-2222-2222-222222222222"), item.FamilyId);
        Assert.Equal("INV-PAY001-202605052141-1", item.InvoiceNumber);
        Assert.Equal(47889842, item.TransactionId);
        Assert.Equal("[{\"invoiceId\":63677011}]", item.RawResponse);
        Assert.Equal("{\"transactionId\":47889842}", item.TransactionResponse);
        Assert.Equal("MC", item.CardType);
        Assert.Equal(HelcimCurrency.Cad, item.Currency);
        Assert.Equal(HelcimInvoiceStatus.Paid, item.InvoiceStatus);
        Assert.Equal(HelcimCardTransactionStatus.Approved, item.CardTransactionStatus);
        Assert.Equal(HelcimInvoiceType.Invoice, item.InvoiceType);
        Assert.Equal(HelcimCardTransactionType.Purchase, item.CardTransactionType);
    }

    [Fact]
    public async Task GetDetailedByPaymentCode_PreservesRawCardTypeCode()
    {
        var database = new FakeDatabase(() => CreateDetailedReader("DB"));
        var repository = new HelcimTransactionRepository(database);

        var result = await repository.GetDetailedByPaymentCode("PAY001");

        var item = Assert.Single(result);
        Assert.Equal("DB", item.CardType);
    }

    [Fact]
    public async Task Add_ThrowsActionableMessage_WhenLegacyInvoicePrimaryKeyBlocksInsert()
    {
        var database = new FakeDatabase(
            CreateEmptyReader,
            executeException: new FakeDbException("Duplicate entry 'INV-D95NCR-202607191225-1' for key 'helcim_transaction.PRIMARY'"));
        var repository = new HelcimTransactionRepository(database);

        var transactionDetails = new AddHelcimTransactionDetails
        {
            PaymentCode = "PAY001",
            MaktabTransactionId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            InvoiceId = 63677011,
            InvoiceNumber = "INV-D95NCR-202607191225-1",
            TransactionId = 47889842,
            Currency = HelcimCurrency.Cad,
            InvoiceStatus = HelcimInvoiceStatus.Paid,
            CardTransactionStatus = HelcimCardTransactionStatus.Approved,
            InvoiceType = HelcimInvoiceType.Invoice,
            CardTransactionType = HelcimCardTransactionType.Purchase,
            IsActive = true
        };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => repository.Add(transactionDetails));

        Assert.Contains("keyed by InvoiceNumber", exception.Message);
        Assert.Contains("AlterHelcimTransactionPrimaryKeyToTransactionId.sql", exception.Message);
        Assert.IsType<FakeDbException>(exception.InnerException);
    }

    private static DbDataReader CreateEmptyReader()
    {
        var table = new DataTable();
        return table.CreateDataReader();
    }

    private static DbDataReader CreateDetailedReader(string cardType = "MC")
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
        table.Columns.Add("FamilyId", typeof(byte[]));

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
            cardType,
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
            "{\"transactionId\":47889842}",
            Guid.Parse("22222222-2222-2222-2222-222222222222").ToByteArray());

        return table.CreateDataReader();
    }
}
