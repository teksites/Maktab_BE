using System.Data;
using System.Data.Common;
using Courses.Test.Infrastructure;
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

    private static DbDataReader CreateEmptyReader()
    {
        var table = new DataTable();
        return table.CreateDataReader();
    }
}
