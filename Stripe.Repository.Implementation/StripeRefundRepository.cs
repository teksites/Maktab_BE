using System.Data;
using Cumulus.Data;
using Data;
using Stripe.Repository;

namespace Stripe.Repository.Implementation;

public sealed class StripeRefundRepository : IStripeRefundRepository
{
    private readonly IDatabase _database;
    public StripeRefundRepository(IDatabase database) => _database = database;

    public async Task<StripeRefundRecord?> GetByIdempotencyKeyAsync(string key)
    {
        using var connection = await _database.CreateAndOpenConnectionAsync(); using var command = connection.CreateCommand();
        command.CommandText = "SELECT IdempotencyKey, StripeRefundId, StripePaymentIntentId, StripeChargeId, ReferenceData, AmountMinor, Currency, StripeStatus, IsLiveMode, FailureReason FROM stripe_refund WHERE IdempotencyKey=@Key LIMIT 1";
        command.AddParameter("@Key", key); using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? Map(reader) : null;
    }

    public async Task<StripeRefundRecord?> GetByStripeRefundIdAsync(string stripeRefundId)
    {
        using var connection = await _database.CreateAndOpenConnectionAsync(); using var command = connection.CreateCommand();
        command.CommandText = "SELECT IdempotencyKey, StripeRefundId, StripePaymentIntentId, StripeChargeId, ReferenceData, AmountMinor, Currency, StripeStatus, IsLiveMode, FailureReason FROM stripe_refund WHERE StripeRefundId=@Id LIMIT 1";
        command.AddParameter("@Id", stripeRefundId); using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? Map(reader) : null;
    }

    public async Task ReserveAsync(StripeRefundRecord record)
    {
        using var connection = await _database.CreateAndOpenConnectionAsync(); using var command = connection.CreateCommand(); var now = DateTime.UtcNow;
        command.CommandText = "INSERT IGNORE INTO stripe_refund (StripePaymentIntentId, StripeChargeId, IdempotencyKey, ReferenceData, AmountMinor, Currency, StripeStatus, IsLiveMode, CreatedAt, UpdatedOn) VALUES (@Intent,@Charge,@Key,@Reference,@Amount,@Currency,@Status,0,@Now,@Now)";
        command.AddParameter("@Intent", (object?)record.StripePaymentIntentId ?? DBNull.Value); command.AddParameter("@Charge", (object?)record.StripeChargeId ?? DBNull.Value); command.AddParameter("@Key", record.IdempotencyKey); command.AddParameter("@Reference", (object?)record.ReferenceData ?? DBNull.Value); command.AddParameter("@Amount", record.AmountMinor); command.AddParameter("@Currency", record.Currency); command.AddParameter("@Status", record.StripeStatus); command.AddParameter("@Now", now); await command.ExecuteNonQueryAsync();
    }

    public async Task UpdateAsync(StripeRefundRecord record)
    {
        using var connection = await _database.CreateAndOpenConnectionAsync(); using var command = connection.CreateCommand();
        command.CommandText = "UPDATE stripe_refund SET StripeRefundId=@RefundId, StripeStatus=@Status, IsLiveMode=@Live, FailureReason=@Failure, UpdatedOn=@Now WHERE IdempotencyKey=@Key";
        command.AddParameter("@RefundId", (object?)record.StripeRefundId ?? DBNull.Value); command.AddParameter("@Status", record.StripeStatus); command.AddParameter("@Live", record.IsLiveMode); command.AddParameter("@Failure", (object?)record.FailureReason ?? DBNull.Value); command.AddParameter("@Now", DateTime.UtcNow); command.AddParameter("@Key", record.IdempotencyKey); await command.ExecuteNonQueryAsync();
    }

    private static StripeRefundRecord Map(System.Data.Common.DbDataReader reader) => new() { IdempotencyKey = reader.GetString(0), StripeRefundId = reader.IsDBNull(1) ? null : reader.GetString(1), StripePaymentIntentId = reader.IsDBNull(2) ? null : reader.GetString(2), StripeChargeId = reader.IsDBNull(3) ? null : reader.GetString(3), ReferenceData = reader.IsDBNull(4) ? null : reader.GetString(4), AmountMinor = reader.GetInt64(5), Currency = reader.GetString(6), StripeStatus = reader.GetString(7), IsLiveMode = reader.GetBoolean(8), FailureReason = reader.IsDBNull(9) ? null : reader.GetString(9) };
}
