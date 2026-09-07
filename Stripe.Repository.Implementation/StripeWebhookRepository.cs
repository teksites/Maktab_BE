using System.Data;
using Cumulus.Data;
using Data;
using Stripe.Repository;

namespace Stripe.Repository.Implementation;

public sealed class StripeWebhookRepository : DbRepository, IStripeWebhookRepository
{
    public StripeWebhookRepository(IDatabase database) : base(database) { }

    public async Task<StripeWebhookReservationResult> TryReserveAsync(StripeWebhookReservation reservation)
    {
        using var connection = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false);
        using var insert = connection.CreateCommand();
        insert.CommandText = @"
            INSERT IGNORE INTO stripe_webhook_event
            (StripeEventId, EventType, IsLiveMode, SignatureHeader, RawPayload, ProcessingStatus, AttemptCount, ProcessingStartedAt, CreatedAt, UpdatedOn)
            VALUES (@EventId, @EventType, @LiveMode, @Signature, @RawPayload, @Status, 1, @Now, @Now, @Now)";
        insert.AddParameter("@EventId", reservation.EventId);
        insert.AddParameter("@EventType", reservation.EventType);
        insert.AddParameter("@LiveMode", reservation.LiveMode);
        insert.AddParameter("@Signature", reservation.SignatureHeader);
        insert.AddParameter("@RawPayload", reservation.RawPayload);
        insert.AddParameter("@Status", (int)StripeWebhookProcessingStatus.Processing);
        insert.AddParameter("@Now", reservation.ReceivedAtUtc);
        if (await insert.ExecuteNonQueryAsync().ConfigureAwait(false) > 0)
            return StripeWebhookReservationResult.Reserved;

        using var read = connection.CreateCommand();
        read.CommandText = @"SELECT StripeWebhookEventKey, ProcessingStatus, UpdatedOn FROM stripe_webhook_event WHERE StripeEventId = @EventId LIMIT 1";
        read.AddParameter("@EventId", reservation.EventId);
        using var reader = await read.ExecuteReaderAsync().ConfigureAwait(false);
        if (!await reader.ReadAsync().ConfigureAwait(false)) return StripeWebhookReservationResult.AlreadyProcessing;

        var key = reader.GetInt64(0);
        var status = (StripeWebhookProcessingStatus)reader.GetInt32(1);
        var updatedOn = reader.IsDBNull(2) ? DateTime.MinValue : reader.GetDateTime(2);
        if (status != StripeWebhookProcessingStatus.Failed && !(status == StripeWebhookProcessingStatus.Processing && updatedOn < reservation.StaleBeforeUtc))
            return status == StripeWebhookProcessingStatus.Processing ? StripeWebhookReservationResult.AlreadyProcessing : StripeWebhookReservationResult.AlreadyProcessed;

        reader.Close();
        using var resume = connection.CreateCommand();
        resume.CommandText = @"
            UPDATE stripe_webhook_event
            SET EventType = @EventType, IsLiveMode = @LiveMode, SignatureHeader = @Signature, RawPayload = @RawPayload,
                ProcessingStatus = @Processing, AttemptCount = AttemptCount + 1, ProcessingStartedAt = @Now, ProcessedAt = NULL,
                LastError = NULL, UpdatedOn = @Now
            WHERE StripeWebhookEventKey = @Key
              AND (ProcessingStatus = @Failed OR (ProcessingStatus = @Processing AND UpdatedOn < @StaleBefore))";
        resume.AddParameter("@EventType", reservation.EventType);
        resume.AddParameter("@LiveMode", reservation.LiveMode);
        resume.AddParameter("@Signature", reservation.SignatureHeader);
        resume.AddParameter("@RawPayload", reservation.RawPayload);
        resume.AddParameter("@Processing", (int)StripeWebhookProcessingStatus.Processing);
        resume.AddParameter("@Failed", (int)StripeWebhookProcessingStatus.Failed);
        resume.AddParameter("@StaleBefore", reservation.StaleBeforeUtc);
        resume.AddParameter("@Now", reservation.ReceivedAtUtc);
        resume.AddParameter("@Key", key);
        return await resume.ExecuteNonQueryAsync().ConfigureAwait(false) > 0
            ? StripeWebhookReservationResult.Reserved
            : StripeWebhookReservationResult.AlreadyProcessing;
    }

    public async Task UpdateAsync(StripeWebhookProcessingUpdate update)
    {
        using var connection = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false);
        using var command = connection.CreateCommand();
        command.CommandText = @"
            UPDATE stripe_webhook_event
            SET ProcessingStatus = @Status, LastError = @Error, UpdatedOn = @UpdatedOn, ProcessedAt = @ProcessedOn
            WHERE StripeEventId = @EventId";
        command.AddParameter("@Status", (int)update.Status);
        command.AddParameter("@Error", (object?)update.ErrorMessage ?? DBNull.Value);
        command.AddParameter("@UpdatedOn", update.UpdatedOnUtc);
        command.AddParameter("@ProcessedOn", (object?)update.ProcessedOnUtc ?? DBNull.Value);
        command.AddParameter("@EventId", update.EventId);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }
}
