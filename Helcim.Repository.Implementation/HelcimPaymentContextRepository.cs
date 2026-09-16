using Cumulus.Data;
using Data;
using Helcim.Repository;
using MaktabDataContracts.Enums;

namespace Helcim.Repository.Implementation;

public sealed class HelcimPaymentContextRepository : DbRepository, IHelcimPaymentContextRepository
{
    public HelcimPaymentContextRepository(IDatabase database) : base(database) { }

    public async Task Save(HelcimPaymentContext context)
    {
        using var connection = await Database.CreateAndOpenConnectionAsync();
        using var command = connection.CreateCommand();
        command.CommandText = @"INSERT INTO helcim_payment_context
            (PaymentContextId, InvoiceNumber, PaymentCode, MaktabTransactionId, PaymentType, CampaignId, UserId, FamilyId, SaveCardInfo, Amount)
            VALUES (@ContextId, @Invoice, @PaymentCode, @TransactionId, @PaymentType, @CampaignId, @UserId, @FamilyId, @SaveCardInfo, @Amount)
            ON DUPLICATE KEY UPDATE PaymentCode = VALUES(PaymentCode), MaktabTransactionId = VALUES(MaktabTransactionId),
                PaymentType = VALUES(PaymentType), CampaignId = VALUES(CampaignId), UserId = VALUES(UserId), FamilyId = VALUES(FamilyId),
                SaveCardInfo = VALUES(SaveCardInfo), Amount = VALUES(Amount)";
        command.AddParameter("@ContextId", context.PaymentContextId.ToByteArray());
        command.AddParameter("@Invoice", context.InvoiceNumber);
        command.AddParameter("@PaymentCode", string.IsNullOrWhiteSpace(context.PaymentCode) ? null : context.PaymentCode);
        command.AddParameter("@TransactionId", context.MaktabTransactionId == Guid.Empty ? null : context.MaktabTransactionId.ToByteArray());
        command.AddParameter("@PaymentType", (int)context.PaymentType);
        command.AddParameter("@CampaignId", context.CampaignId?.ToByteArray());
        command.AddParameter("@UserId", context.UserId?.ToByteArray());
        command.AddParameter("@FamilyId", context.FamilyId?.ToByteArray());
        command.AddParameter("@SaveCardInfo", context.SaveCardInfo);
        command.AddParameter("@Amount", context.Amount);
        await command.ExecuteNonQueryAsync();
    }

    public Task<HelcimPaymentContext?> GetByInvoiceNumber(string invoiceNumber)
        => Get("InvoiceNumber", invoiceNumber);

    public Task<HelcimPaymentContext?> GetByPaymentContextId(Guid paymentContextId)
        => Get("PaymentContextId", paymentContextId.ToByteArray());

    public async Task<HelcimPaymentContext?> GetByPaymentCode(string paymentCode)
    {
        using var connection = await Database.CreateAndOpenConnectionAsync();
        using var command = connection.CreateCommand();
        command.CommandText = @"SELECT PaymentContextId, InvoiceNumber, PaymentCode, MaktabTransactionId, PaymentType, CampaignId, UserId, FamilyId, SaveCardInfo, Amount
            FROM helcim_payment_context WHERE PaymentCode = @Value ORDER BY CreatedAt DESC LIMIT 1";
        command.AddParameter("@Value", paymentCode);
        using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? Map(reader) : null;
    }

    private async Task<HelcimPaymentContext?> Get(string column, object value)
    {
        using var connection = await Database.CreateAndOpenConnectionAsync();
        using var command = connection.CreateCommand();
        command.CommandText = $@"SELECT PaymentContextId, InvoiceNumber, PaymentCode, MaktabTransactionId, PaymentType, CampaignId, UserId, FamilyId, SaveCardInfo, Amount
            FROM helcim_payment_context WHERE {column} = @Value";
        command.AddParameter("@Value", value);
        using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;
        return Map(reader);
    }

    private static HelcimPaymentContext Map(System.Data.Common.DbDataReader reader)
    {
        return new HelcimPaymentContext
        {
            PaymentContextId = ReadDbFieldGuid(reader, "PaymentContextId"),
            InvoiceNumber = ReadDbFieldString(reader, "InvoiceNumber"),
            PaymentCode = ReadDbFieldString(reader, "PaymentCode"),
            MaktabTransactionId = ReadDbFieldNullableGuid(reader, "MaktabTransactionId") ?? Guid.Empty,
            PaymentType = (PaymentInitiationType)reader.GetInt32(reader.GetOrdinal("PaymentType")),
            CampaignId = ReadDbFieldNullableGuid(reader, "CampaignId"),
            UserId = ReadDbFieldNullableGuid(reader, "UserId"),
            FamilyId = ReadDbFieldNullableGuid(reader, "FamilyId"),
            SaveCardInfo = ReadDbFieldBool(reader, "SaveCardInfo"),
            Amount = reader.GetDecimal(reader.GetOrdinal("Amount"))
        };
    }
}
