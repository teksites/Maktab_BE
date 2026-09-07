using System.Data;
using Cumulus.Data;
using Data;
using Stripe.Repository;

namespace Stripe.Repository.Implementation;

public sealed class StripePaymentIntentRepository : IStripePaymentIntentRepository
{
    private readonly IDatabase _database;
    public StripePaymentIntentRepository(IDatabase database) => _database = database;

    public async Task<StripePaymentIntentRecord?> GetByIdempotencyKeyAsync(string key)
    {
        using var connection = await _database.CreateAndOpenConnectionAsync(); using var command = connection.CreateCommand();
        command.CommandText = "SELECT IdempotencyKey, StripePaymentIntentId, StripeChargeId, ReferenceData, AmountMinor, AmountReceivedMinor, Currency, StripeStatus, IsLiveMode, LastPaymentErrorCode, LastPaymentErrorMessage FROM stripe_payment_intent WHERE IdempotencyKey = @Key LIMIT 1";
        command.AddParameter("@Key", key); using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? Map(reader) : null;
    }

    public async Task<StripePaymentIntentRecord?> GetByStripePaymentIntentIdAsync(string stripePaymentIntentId)
    {
        using var connection = await _database.CreateAndOpenConnectionAsync(); using var command = connection.CreateCommand();
        command.CommandText = "SELECT IdempotencyKey, StripePaymentIntentId, StripeChargeId, ReferenceData, AmountMinor, AmountReceivedMinor, Currency, StripeStatus, IsLiveMode, LastPaymentErrorCode, LastPaymentErrorMessage FROM stripe_payment_intent WHERE StripePaymentIntentId = @Id LIMIT 1";
        command.AddParameter("@Id", stripePaymentIntentId); using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? Map(reader) : null;
    }

    public async Task ReserveAsync(StripePaymentIntentRecord record)
    {
        using var connection = await _database.CreateAndOpenConnectionAsync(); using var command = connection.CreateCommand(); var now = DateTime.UtcNow;
        command.CommandText = "INSERT IGNORE INTO stripe_payment_intent (IdempotencyKey, ReferenceData, AmountMinor, AmountReceivedMinor, Currency, StripeStatus, IsLiveMode, CreatedAt, UpdatedOn) VALUES (@Key,@Reference,@Amount,0,@Currency,@Status,0,@Now,@Now)";
        command.AddParameter("@Key", record.IdempotencyKey); command.AddParameter("@Reference", (object?)record.ReferenceData ?? DBNull.Value); command.AddParameter("@Amount", record.AmountMinor); command.AddParameter("@Currency", record.Currency); command.AddParameter("@Status", record.StripeStatus); command.AddParameter("@Now", now); await command.ExecuteNonQueryAsync();
    }

    public async Task UpdateAsync(StripePaymentIntentRecord record)
    {
        using var connection = await _database.CreateAndOpenConnectionAsync(); using var command = connection.CreateCommand();
        command.CommandText = "UPDATE stripe_payment_intent SET StripePaymentIntentId=@Id, StripeChargeId=@Charge, AmountReceivedMinor=@Received, StripeStatus=@Status, IsLiveMode=@Live, LastPaymentErrorCode=@ErrorCode, LastPaymentErrorMessage=@ErrorMessage, UpdatedOn=@Now WHERE IdempotencyKey=@Key";
        command.AddParameter("@Id", (object?)record.StripePaymentIntentId ?? DBNull.Value); command.AddParameter("@Charge", (object?)record.StripeChargeId ?? DBNull.Value); command.AddParameter("@Received", record.AmountReceivedMinor); command.AddParameter("@Status", record.StripeStatus); command.AddParameter("@Live", record.IsLiveMode); command.AddParameter("@ErrorCode", (object?)record.ErrorCode ?? DBNull.Value); command.AddParameter("@ErrorMessage", (object?)record.ErrorMessage ?? DBNull.Value); command.AddParameter("@Now", DateTime.UtcNow); command.AddParameter("@Key", record.IdempotencyKey); await command.ExecuteNonQueryAsync();
    }

    private static StripePaymentIntentRecord Map(System.Data.Common.DbDataReader reader) => new() { IdempotencyKey = reader.GetString(0), StripePaymentIntentId = reader.IsDBNull(1) ? null : reader.GetString(1), StripeChargeId = reader.IsDBNull(2) ? null : reader.GetString(2), ReferenceData = reader.IsDBNull(3) ? null : reader.GetString(3), AmountMinor = reader.GetInt64(4), AmountReceivedMinor = reader.GetInt64(5), Currency = reader.GetString(6), StripeStatus = reader.GetString(7), IsLiveMode = reader.GetBoolean(8), ErrorCode = reader.IsDBNull(9) ? null : reader.GetString(9), ErrorMessage = reader.IsDBNull(10) ? null : reader.GetString(10) };
}
