using Cumulus.Data;
using Data;
using Helcim.Repository;

namespace Helcim.Repository.Implementation;

public sealed class DonationPaymentRepository : DbRepository, IDonationPaymentRepository
{
    public DonationPaymentRepository(IDatabase database) : base(database) { }

    public async Task AddOrUpdate(DonationPaymentRecord payment)
    {
        using var connection = await Database.CreateAndOpenConnectionAsync();
        using var command = connection.CreateCommand();
        command.CommandText = @"INSERT INTO donation_payment
            (DonationPaymentId, PaymentContextId, CampaignId, UserId, HelcimTransactionId, HelcimInvoiceId,
             HelcimInvoiceNumber, PaymentCode, Amount, NetAmount, Currency, InvoiceStatus, TransactionStatus,
             TransactionType, CardCompany, CardFundingType, CardFundingTypeKnown, LastFourDigits,
             CardHolderName, CardType, PaidAt)
            VALUES
            (@DonationPaymentId, @PaymentContextId, @CampaignId, @UserId, @HelcimTransactionId, @HelcimInvoiceId,
             @HelcimInvoiceNumber, @PaymentCode, @Amount, @NetAmount, @Currency, @InvoiceStatus, @TransactionStatus,
             @TransactionType, @CardCompany, @CardFundingType, @CardFundingTypeKnown, @LastFourDigits,
             @CardHolderName, @CardType, @PaidAt)
            ON DUPLICATE KEY UPDATE
             PaymentContextId = VALUES(PaymentContextId), CampaignId = VALUES(CampaignId), UserId = VALUES(UserId),
             HelcimInvoiceId = VALUES(HelcimInvoiceId), HelcimInvoiceNumber = VALUES(HelcimInvoiceNumber),
             PaymentCode = VALUES(PaymentCode), Amount = VALUES(Amount), NetAmount = VALUES(NetAmount),
             Currency = VALUES(Currency), InvoiceStatus = VALUES(InvoiceStatus), TransactionStatus = VALUES(TransactionStatus),
             TransactionType = VALUES(TransactionType), CardCompany = VALUES(CardCompany),
             CardFundingType = VALUES(CardFundingType), CardFundingTypeKnown = VALUES(CardFundingTypeKnown),
             LastFourDigits = VALUES(LastFourDigits), CardHolderName = VALUES(CardHolderName), CardType = VALUES(CardType),
             PaidAt = VALUES(PaidAt)";
        command.AddParameter("@DonationPaymentId", payment.DonationPaymentId.ToByteArray());
        command.AddParameter("@PaymentContextId", payment.PaymentContextId.ToByteArray());
        command.AddParameter("@CampaignId", payment.CampaignId.ToByteArray());
        command.AddParameter("@UserId", payment.UserId?.ToByteArray());
        command.AddParameter("@HelcimTransactionId", payment.HelcimTransactionId);
        command.AddParameter("@HelcimInvoiceId", payment.HelcimInvoiceId);
        command.AddParameter("@HelcimInvoiceNumber", payment.HelcimInvoiceNumber);
        command.AddParameter("@PaymentCode", string.IsNullOrWhiteSpace(payment.PaymentCode) ? null : payment.PaymentCode);
        command.AddParameter("@Amount", payment.Amount);
        command.AddParameter("@NetAmount", payment.NetAmount);
        command.AddParameter("@Currency", (int)payment.Currency);
        command.AddParameter("@InvoiceStatus", (int)payment.InvoiceStatus);
        command.AddParameter("@TransactionStatus", (int)payment.TransactionStatus);
        command.AddParameter("@TransactionType", (int)payment.TransactionType);
        command.AddParameter("@CardCompany", payment.CardCompany);
        command.AddParameter("@CardFundingType", payment.CardFundingType);
        command.AddParameter("@CardFundingTypeKnown", payment.CardFundingTypeKnown);
        command.AddParameter("@LastFourDigits", payment.LastFourDigits);
        command.AddParameter("@CardHolderName", payment.CardHolderName);
        command.AddParameter("@CardType", payment.CardType);
        command.AddParameter("@PaidAt", payment.PaidAt);
        await command.ExecuteNonQueryAsync();
    }
}
