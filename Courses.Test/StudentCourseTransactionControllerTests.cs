using Courses.Services;
using Helcim;
using Helcim.Services;
using MaktabDataContracts.Enums;
using MaktabDataContracts.Enums.Helcim;
using MaktabDataContracts.Responses.Helcim;
using Moq;
using Users.Services;

namespace Courses.Test;

public class StudentCourseTransactionControllerTests
{
    [Fact]
    public async Task GetDetailedHelcimTransactionsByPaymentCode_ReturnsNormalizedView()
    {
        var transactionService = new Mock<IStudentCourseTransactionService>();
        var helcimService = new Mock<IHelcimTransactionService>();
        var dataAccessVerificationService = new Mock<IDataAccessVerificationService>();

        helcimService
            .Setup(x => x.GetDetailedByPaymentCode("PAY001"))
            .ReturnsAsync(new List<HelcimTransactionResponseDetailed>
            {
                new()
                {
                    PaymentCode = "PAY001",
                    MaktabTransactionId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                    FamilyId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                    InvoiceId = 1001,
                    InvoiceNumber = "INV-001",
                    TransactionId = 3001,
                    CardBatchId = 4001,
                    CardType = "DB",
                    Currency = HelcimCurrency.Cad,
                    InvoiceStatus = HelcimInvoiceStatus.Refunded,
                    CardTransactionStatus = HelcimCardTransactionStatus.Approved,
                    InvoiceType = HelcimInvoiceType.Invoice,
                    CardTransactionType = HelcimCardTransactionType.Reverse,
                    IsActive = true
                }
            });

        var controller = new StudentCourseTransactionController(
            transactionService.Object,
            helcimService.Object,
            dataAccessVerificationService.Object);

        var result = await controller.GetDetailedHelcimTransactionsByPaymentCode("PAY001");

        var item = Assert.Single(result);
        Assert.Equal("DB", item.CardType);
        Assert.Equal(HelcimNormalizedPaymentSourceType.DebitCard, item.PaymentSourceType);
        Assert.Equal("Debit Card", item.PaymentSourceLabel);
        Assert.Equal(HelcimNormalizedCardType.Debit, item.NormalizedCardType);
        Assert.Equal("Debit", item.NormalizedCardTypeLabel);
        Assert.Equal(CardType.Unknown, item.KnownCardType);
    }
}
