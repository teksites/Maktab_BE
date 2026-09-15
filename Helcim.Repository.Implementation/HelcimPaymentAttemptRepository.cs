using Cumulus.Data;
using Data;
using Helcim.Repository;

namespace Helcim.Repository.Implementation;

public sealed class HelcimPaymentAttemptRepository : DbRepository, IHelcimPaymentAttemptRepository
{
    public HelcimPaymentAttemptRepository(IDatabase database) : base(database) { }

    public async Task<HelcimPaymentAttemptRecord?> GetByIdempotencyKey(string idempotencyKey, Guid userId)
    {
        using var connection = await Database.CreateAndOpenConnectionAsync();
        using var command = connection.CreateCommand();
        command.CommandText = @"SELECT PaymentAttemptId, UserId, CardId, MaktabTransactionId, PaymentCode, InvoiceNumber,
            IdempotencyKey, Amount, Status, HelcimTransactionId, FailureReason FROM helcim_payment_attempt
            WHERE IdempotencyKey = @IdempotencyKey AND UserId = @UserId";
        command.AddParameter("@IdempotencyKey", idempotencyKey);
        command.AddParameter("@UserId", userId.ToByteArray());
        using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;
        return Map(reader);
    }

    public async Task<HelcimPaymentAttemptRecord?> GetByInvoiceNumber(string invoiceNumber)
    {
        using var connection = await Database.CreateAndOpenConnectionAsync();
        using var command = connection.CreateCommand();
        command.CommandText = @"SELECT PaymentAttemptId, UserId, CardId, MaktabTransactionId, PaymentCode, InvoiceNumber,
            IdempotencyKey, Amount, Status, HelcimTransactionId, FailureReason FROM helcim_payment_attempt WHERE InvoiceNumber = @Invoice";
        command.AddParameter("@Invoice", invoiceNumber);
        using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? Map(reader) : null;
    }

    public async Task Add(HelcimPaymentAttemptRecord attempt)
    {
        using var connection = await Database.CreateAndOpenConnectionAsync(); using var command = connection.CreateCommand();
        command.CommandText = @"INSERT INTO helcim_payment_attempt (PaymentAttemptId, UserId, CardId, MaktabTransactionId,
            PaymentCode, InvoiceNumber, IdempotencyKey, Amount, Status) VALUES (@Id, @User, @Card, @Transaction,
            @PaymentCode, @Invoice, @Key, @Amount, @Status)";
        command.AddParameter("@Id", attempt.PaymentAttemptId.ToByteArray()); command.AddParameter("@User", attempt.UserId.ToByteArray());
        command.AddParameter("@Card", attempt.CardId.ToByteArray()); command.AddParameter("@Transaction", attempt.MaktabTransactionId.ToByteArray());
        command.AddParameter("@PaymentCode", attempt.PaymentCode); command.AddParameter("@Invoice", attempt.InvoiceNumber);
        command.AddParameter("@Key", attempt.IdempotencyKey); command.AddParameter("@Amount", attempt.Amount); command.AddParameter("@Status", (byte)attempt.Status);
        await command.ExecuteNonQueryAsync();
    }

    public async Task UpdateResult(Guid id, HelcimPaymentAttemptStatus status, int? transactionId, string? failureReason)
    {
        using var connection = await Database.CreateAndOpenConnectionAsync(); using var command = connection.CreateCommand();
        command.CommandText = "UPDATE helcim_payment_attempt SET Status = @Status, HelcimTransactionId = @TransactionId, FailureReason = @Failure WHERE PaymentAttemptId = @Id";
        command.AddParameter("@Status", (byte)status); command.AddParameter("@TransactionId", transactionId); command.AddParameter("@Failure", failureReason); command.AddParameter("@Id", id.ToByteArray());
        await command.ExecuteNonQueryAsync();
    }

    private static HelcimPaymentAttemptRecord Map(System.Data.IDataReader reader) => new()
    {
        PaymentAttemptId = ReadDbFieldGuid(reader, "PaymentAttemptId"), UserId = ReadDbFieldGuid(reader, "UserId"),
        CardId = ReadDbFieldGuid(reader, "CardId"), MaktabTransactionId = ReadDbFieldGuid(reader, "MaktabTransactionId"),
        PaymentCode = ReadDbFieldString(reader, "PaymentCode"), InvoiceNumber = ReadDbFieldString(reader, "InvoiceNumber"),
        IdempotencyKey = ReadDbFieldString(reader, "IdempotencyKey"), Amount = reader.GetDecimal(reader.GetOrdinal("Amount")),
        Status = (HelcimPaymentAttemptStatus)reader.GetByte(reader.GetOrdinal("Status")),
        HelcimTransactionId = ReadDbFieldNullInt(reader, "HelcimTransactionId"), FailureReason = reader.GetNullableString("FailureReason")
    };
}
