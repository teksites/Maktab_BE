using System.Data.Common;
using Cumulus.Data;
using Data;
using MaktabDataContracts.Requests.Helcim;
using MaktabDataContracts.Responses.Helcim;
using MaktabDataContracts.Enums.Helcim;

namespace Helcim.Repository.Implementation
{
    public class HelcimTransactionRepository : DbRepository, IHelcimTransactionRepository
    {
        public HelcimTransactionRepository(IDatabase database) : base(database) { }

        public async Task Add(AddHelcimTransactionDetails transactionDetails)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                INSERT INTO helcim_transaction
                (
                    PaymentCode,
                    MaktabTransactionId,
                    UserIp,
                    InvoiceId,
                    InvoiceNumber,
                    InvoiceToken,
                    CustomerId,
                    CustomerCode,
                    TransactionId,
                    CardBatchId,
                    User,
                    ApprovalCode,
                    CardToken,
                    CardNumber,
                    CardHolderName,
                    CardType,
                    AvsResponse,
                    CvvResponse,
                    Warning,
                    Amount,
                    AmountPaid,
                    Currency,
                    InvoiceStatus,
                    CardTransactionStatus,
                    InvoiceType,
                    CardTransactionType,
                    CreatedAt,
                    UpdatedOn,
                    DatePaid,
                    IsActive,
                    RawResponse,
                    TransactionResponse
                )
                VALUES
                (
                    @PaymentCode,
                    @MaktabTransactionId,
                    @UserIp,
                    @InvoiceId,
                    @InvoiceNumber,
                    @InvoiceToken,
                    @CustomerId,
                    @CustomerCode,
                    @TransactionId,
                    @CardBatchId,
                    @User,
                    @ApprovalCode,
                    @CardToken,
                    @CardNumber,
                    @CardHolderName,
                    @CardType,
                    @AvsResponse,
                    @CvvResponse,
                    @Warning,
                    @Amount,
                    @AmountPaid,
                    @Currency,
                    @InvoiceStatus,
                    @CardTransactionStatus,
                    @InvoiceType,
                    @CardTransactionType,
                    @CreatedAt,
                    @UpdatedOn,
                    @DatePaid,
                    @IsActive,
                    @RawResponse,
                    @TransactionResponse
                )";

            cmd.AddParameter("@PaymentCode", transactionDetails.PaymentCode);
            cmd.AddParameter("@MaktabTransactionId", transactionDetails.MaktabTransactionId.ToByteArray());
            cmd.AddParameter("@UserIp", (object?)transactionDetails.UserIp ?? DBNull.Value);
            cmd.AddParameter("@InvoiceId", transactionDetails.InvoiceId);
            cmd.AddParameter("@InvoiceNumber", (object?)transactionDetails.InvoiceNumber ?? DBNull.Value);
            cmd.AddParameter("@InvoiceToken", (object?)transactionDetails.InvoiceToken ?? DBNull.Value);
            cmd.AddParameter("@CustomerId", transactionDetails.CustomerId);
            cmd.AddParameter("@CustomerCode", (object?)transactionDetails.CustomerCode ?? DBNull.Value);
            cmd.AddParameter("@TransactionId", transactionDetails.TransactionId);
            cmd.AddParameter("@CardBatchId", transactionDetails.CardBatchId);
            cmd.AddParameter("@User", (object?)transactionDetails.User ?? DBNull.Value);
            cmd.AddParameter("@ApprovalCode", (object?)transactionDetails.ApprovalCode ?? DBNull.Value);
            cmd.AddParameter("@CardToken", (object?)transactionDetails.CardToken ?? DBNull.Value);
            cmd.AddParameter("@CardNumber", (object?)transactionDetails.CardNumber ?? DBNull.Value);
            cmd.AddParameter("@CardHolderName", (object?)transactionDetails.CardHolderName ?? DBNull.Value);
            cmd.AddParameter("@CardType", (object?)transactionDetails.CardType ?? DBNull.Value);
            cmd.AddParameter("@AvsResponse", (object?)transactionDetails.AvsResponse ?? DBNull.Value);
            cmd.AddParameter("@CvvResponse", (object?)transactionDetails.CvvResponse ?? DBNull.Value);
            cmd.AddParameter("@Warning", (object?)transactionDetails.Warning ?? DBNull.Value);
            cmd.AddParameter("@Amount", transactionDetails.Amount);
            cmd.AddParameter("@AmountPaid", transactionDetails.AmountPaid);
            cmd.AddParameter("@Currency", (int)transactionDetails.Currency);
            cmd.AddParameter("@InvoiceStatus", (int)transactionDetails.InvoiceStatus);
            cmd.AddParameter("@CardTransactionStatus", (int)transactionDetails.CardTransactionStatus);
            cmd.AddParameter("@InvoiceType", (int)transactionDetails.InvoiceType);
            cmd.AddParameter("@CardTransactionType", (int)transactionDetails.CardTransactionType);
            cmd.AddParameter("@CreatedAt", (object?)transactionDetails.CreatedAt ?? DBNull.Value);
            cmd.AddParameter("@UpdatedOn", (object?)transactionDetails.UpdatedOn ?? DBNull.Value);
            cmd.AddParameter("@DatePaid", (object?)transactionDetails.DatePaid ?? DBNull.Value);
            cmd.AddParameter("@IsActive", transactionDetails.IsActive);
            cmd.AddParameter("@RawResponse", (object?)transactionDetails.RawResponse ?? DBNull.Value);
            cmd.AddParameter("@TransactionResponse", (object?)transactionDetails.TransactionResponse ?? DBNull.Value);

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<HelcimWebhookReservationResult> TryReserveWebhookProcessing(ReserveHelcimWebhookProcessing webhookProcessing)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var insertCmd = conn.CreateCommand();

            insertCmd.CommandText = @"
                INSERT IGNORE INTO helcim_webhook_log
                (
                    WebhookId,
                    WebhookType,
                    TransactionId,
                    WebhookTimestamp,
                    RawRequest,
                    SignatureHeader,
                    ProcessingStatus,
                    AttemptCount,
                    InvoiceNumber,
                    InvoiceStatus,
                    ErrorMessage,
                    ReceivedAt,
                    UpdatedOn,
                    ProcessedOn,
                    IsActive
                )
                VALUES
                (
                    @WebhookId,
                    @WebhookType,
                    @TransactionId,
                    @WebhookTimestamp,
                    @RawRequest,
                    @SignatureHeader,
                    @ProcessingStatus,
                    @AttemptCount,
                    NULL,
                    NULL,
                    NULL,
                    @ReceivedAt,
                    @UpdatedOn,
                    NULL,
                    @IsActive
                )";

            insertCmd.AddParameter("@WebhookId", webhookProcessing.WebhookId);
            insertCmd.AddParameter("@WebhookType", (int)webhookProcessing.WebhookType);
            insertCmd.AddParameter("@TransactionId", webhookProcessing.TransactionId);
            insertCmd.AddParameter("@WebhookTimestamp", webhookProcessing.WebhookTimestamp);
            insertCmd.AddParameter("@RawRequest", webhookProcessing.RawRequest);
            insertCmd.AddParameter("@SignatureHeader", webhookProcessing.SignatureHeader);
            insertCmd.AddParameter("@ProcessingStatus", (int)HelcimWebhookProcessingStatus.Processing);
            insertCmd.AddParameter("@AttemptCount", 1);
            insertCmd.AddParameter("@ReceivedAt", webhookProcessing.ReceivedAtUtc);
            insertCmd.AddParameter("@UpdatedOn", webhookProcessing.ReceivedAtUtc);
            insertCmd.AddParameter("@IsActive", true);

            var inserted = await insertCmd.ExecuteNonQueryAsync();
            if (inserted > 0)
            {
                return HelcimWebhookReservationResult.Reserved;
            }

            var existingLog = await GetWebhookLog(conn, webhookProcessing.WebhookId, webhookProcessing.TransactionId).ConfigureAwait(false);
            if (existingLog == null)
            {
                return HelcimWebhookReservationResult.AlreadyProcessing;
            }

            var canResumeProcessing =
                existingLog.ProcessingStatus == HelcimWebhookProcessingStatus.Failed
                || (existingLog.ProcessingStatus == HelcimWebhookProcessingStatus.Processing
                    && existingLog.UpdatedOnUtc.GetValueOrDefault(DateTime.MinValue) < webhookProcessing.StaleBeforeUtc);

            if (!canResumeProcessing)
            {
                return existingLog.ProcessingStatus == HelcimWebhookProcessingStatus.Processing
                    ? HelcimWebhookReservationResult.AlreadyProcessing
                    : HelcimWebhookReservationResult.AlreadyProcessed;
            }

            using var updateCmd = conn.CreateCommand();
            updateCmd.CommandText = @"
                UPDATE helcim_webhook_log
                SET
                    WebhookId = @WebhookId,
                    WebhookType = @WebhookType,
                    TransactionId = @TransactionId,
                    WebhookTimestamp = @WebhookTimestamp,
                    RawRequest = @RawRequest,
                    SignatureHeader = @SignatureHeader,
                    ProcessingStatus = @ProcessingStatus,
                    AttemptCount = AttemptCount + 1,
                    ErrorMessage = NULL,
                    UpdatedOn = @UpdatedOn,
                    ProcessedOn = NULL,
                    IsActive = @IsActive
                WHERE HelcimWebhookLogId = @HelcimWebhookLogId
                  AND (
                        ProcessingStatus = @FailedStatus
                        OR (ProcessingStatus = @CurrentProcessingStatus AND UpdatedOn < @StaleBeforeUtc)
                      )";

            updateCmd.AddParameter("@WebhookId", webhookProcessing.WebhookId);
            updateCmd.AddParameter("@WebhookType", (int)webhookProcessing.WebhookType);
            updateCmd.AddParameter("@TransactionId", webhookProcessing.TransactionId);
            updateCmd.AddParameter("@WebhookTimestamp", webhookProcessing.WebhookTimestamp);
            updateCmd.AddParameter("@RawRequest", webhookProcessing.RawRequest);
            updateCmd.AddParameter("@SignatureHeader", webhookProcessing.SignatureHeader);
            updateCmd.AddParameter("@ProcessingStatus", (int)HelcimWebhookProcessingStatus.Processing);
            updateCmd.AddParameter("@UpdatedOn", webhookProcessing.ReceivedAtUtc);
            updateCmd.AddParameter("@IsActive", true);
            updateCmd.AddParameter("@HelcimWebhookLogId", existingLog.HelcimWebhookLogId);
            updateCmd.AddParameter("@FailedStatus", (int)HelcimWebhookProcessingStatus.Failed);
            updateCmd.AddParameter("@CurrentProcessingStatus", (int)HelcimWebhookProcessingStatus.Processing);
            updateCmd.AddParameter("@StaleBeforeUtc", webhookProcessing.StaleBeforeUtc);

            var updated = await updateCmd.ExecuteNonQueryAsync();
            return updated > 0
                ? HelcimWebhookReservationResult.Reserved
                : HelcimWebhookReservationResult.AlreadyProcessing;
        }

        public async Task UpdateWebhookProcessing(UpdateHelcimWebhookProcessing webhookProcessing)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                UPDATE helcim_webhook_log
                SET
                    ProcessingStatus = @ProcessingStatus,
                    InvoiceNumber = @InvoiceNumber,
                    InvoiceStatus = @InvoiceStatus,
                    ErrorMessage = @ErrorMessage,
                    UpdatedOn = @UpdatedOn,
                    ProcessedOn = @ProcessedOn
                WHERE WebhookId = @WebhookId";

            cmd.AddParameter("@ProcessingStatus", (int)webhookProcessing.ProcessingStatus);
            cmd.AddParameter("@InvoiceNumber", (object?)webhookProcessing.InvoiceNumber ?? DBNull.Value);
            cmd.AddParameter("@InvoiceStatus", webhookProcessing.InvoiceStatus.HasValue ? (object)(int)webhookProcessing.InvoiceStatus.Value : DBNull.Value);
            cmd.AddParameter("@ErrorMessage", (object?)webhookProcessing.ErrorMessage ?? DBNull.Value);
            cmd.AddParameter("@UpdatedOn", webhookProcessing.UpdatedOnUtc);
            cmd.AddParameter("@ProcessedOn", (object?)webhookProcessing.ProcessedOnUtc ?? DBNull.Value);
            cmd.AddParameter("@WebhookId", webhookProcessing.WebhookId);

            await cmd.ExecuteNonQueryAsync();
        }

        public Task<List<HelcimTransactionResponse>> GetByPaymentCode(string paymentCode)
            => GetByColumnAsync("PaymentCode", paymentCode);

        public Task<List<HelcimTransactionResponseDetailed>> GetDetailedByPaymentCode(string paymentCode)
            => GetDetailedByColumnAsync("PaymentCode", paymentCode);

        public Task<List<HelcimTransactionResponse>> GetByMaktabTransactionId(Guid maktabTransactionId)
            => GetByColumnAsync("MaktabTransactionId", maktabTransactionId.ToByteArray());

        public Task<List<HelcimTransactionResponseDetailed>> GetDetailedByMaktabTransactionId(Guid maktabTransactionId)
            => GetDetailedByColumnAsync("MaktabTransactionId", maktabTransactionId.ToByteArray());

        public Task<List<HelcimTransactionResponse>> GetByTransactionId(int transactionId)
            => GetByColumnAsync("TransactionId", transactionId);

        private async Task<List<HelcimTransactionResponse>> GetByColumnAsync(string columnName, object value)
        {
            var results = new List<HelcimTransactionResponse>();

            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = $@"
                SELECT *
                FROM helcim_transaction
                WHERE {columnName} = @Value
                ORDER BY InvoiceNumber ASC, TransactionId ASC";

            cmd.AddParameter("@Value", value);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(MapToResponse(reader));
            }

            return results;
        }

        private async Task<List<HelcimTransactionResponseDetailed>> GetDetailedByColumnAsync(string columnName, object value)
        {
            var results = new List<HelcimTransactionResponseDetailed>();

            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = $@"
                SELECT *
                FROM helcim_transaction
                WHERE {columnName} = @Value
                ORDER BY InvoiceNumber ASC, TransactionId ASC";

            cmd.AddParameter("@Value", value);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(MapToDetailedResponse(reader));
            }

            return results;
        }

        private static async Task<HelcimWebhookLogRow?> GetWebhookLog(DbConnection conn, string webhookId, int transactionId)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT
                    HelcimWebhookLogId,
                    ProcessingStatus,
                    UpdatedOn
                FROM helcim_webhook_log
                WHERE WebhookId = @WebhookId OR TransactionId = @TransactionId
                ORDER BY UpdatedOn DESC
                LIMIT 1";

            cmd.AddParameter("@WebhookId", webhookId);
            cmd.AddParameter("@TransactionId", transactionId);

            using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
            if (!await reader.ReadAsync().ConfigureAwait(false))
            {
                return null;
            }

            return new HelcimWebhookLogRow
            {
                HelcimWebhookLogId = reader.GetInt64(reader.GetOrdinal("HelcimWebhookLogId")),
                ProcessingStatus = (HelcimWebhookProcessingStatus)reader.GetInt32("ProcessingStatus"),
                UpdatedOnUtc = reader.GetNullableDateTimeUtc("UpdatedOn")
            };
        }

        private static HelcimTransactionResponse MapToResponse(DbDataReader reader)
        {
            return new HelcimTransactionResponse
            {
                PaymentCode = reader.GetString("PaymentCode"),
                MaktabTransactionId = reader.GetGuidFromByteArray("MaktabTransactionId"),
                UserIp = reader.GetNullableString("UserIp"),
                InvoiceId = reader.GetInt32("InvoiceId"),
                InvoiceNumber = reader.GetNullableString("InvoiceNumber"),
                InvoiceToken = reader.GetNullableString("InvoiceToken"),
                CustomerId = reader.GetInt32("CustomerId"),
                CustomerCode = reader.GetNullableString("CustomerCode"),
                TransactionId = reader.GetInt32("TransactionId"),
                CardBatchId = reader.GetInt32("CardBatchId"),
                User = reader.GetNullableString("User"),
                ApprovalCode = reader.GetNullableString("ApprovalCode"),
                CardToken = reader.GetNullableString("CardToken"),
                CardNumber = reader.GetNullableString("CardNumber"),
                CardHolderName = reader.GetNullableString("CardHolderName"),
                CardType = reader.GetNullableString("CardType"),
                AvsResponse = reader.GetNullableString("AvsResponse"),
                CvvResponse = reader.GetNullableString("CvvResponse"),
                Warning = reader.GetNullableString("Warning"),
                Amount = reader.GetDecimal(reader.GetOrdinal("Amount")),
                AmountPaid = reader.GetDecimal(reader.GetOrdinal("AmountPaid")),
                Currency = (MaktabDataContracts.Enums.Helcim.HelcimCurrency)reader.GetInt32("Currency"),
                InvoiceStatus = (MaktabDataContracts.Enums.Helcim.HelcimInvoiceStatus)reader.GetInt32("InvoiceStatus"),
                CardTransactionStatus = (MaktabDataContracts.Enums.Helcim.HelcimCardTransactionStatus)reader.GetInt32("CardTransactionStatus"),
                InvoiceType = (MaktabDataContracts.Enums.Helcim.HelcimInvoiceType)reader.GetInt32("InvoiceType"),
                CardTransactionType = (MaktabDataContracts.Enums.Helcim.HelcimCardTransactionType)reader.GetInt32("CardTransactionType"),
                CreatedAt = reader.GetNullableDateTimeUtc("CreatedAt"),
                UpdatedOn = reader.GetNullableDateTimeUtc("UpdatedOn"),
                DatePaid = reader.GetNullableDateTimeUtc("DatePaid"),
                IsActive = reader.GetBoolean("IsActive")
            };
        }

        private static HelcimTransactionResponseDetailed MapToDetailedResponse(DbDataReader reader)
        {
            return new HelcimTransactionResponseDetailed
            {
                PaymentCode = reader.GetString("PaymentCode"),
                MaktabTransactionId = reader.GetGuidFromByteArray("MaktabTransactionId"),
                UserIp = reader.GetNullableString("UserIp"),
                InvoiceId = reader.GetInt32("InvoiceId"),
                InvoiceNumber = reader.GetNullableString("InvoiceNumber"),
                InvoiceToken = reader.GetNullableString("InvoiceToken"),
                CustomerId = reader.GetInt32("CustomerId"),
                CustomerCode = reader.GetNullableString("CustomerCode"),
                TransactionId = reader.GetInt32("TransactionId"),
                CardBatchId = reader.GetInt32("CardBatchId"),
                User = reader.GetNullableString("User"),
                ApprovalCode = reader.GetNullableString("ApprovalCode"),
                CardToken = reader.GetNullableString("CardToken"),
                CardNumber = reader.GetNullableString("CardNumber"),
                CardHolderName = reader.GetNullableString("CardHolderName"),
                CardType = reader.GetNullableString("CardType"),
                AvsResponse = reader.GetNullableString("AvsResponse"),
                CvvResponse = reader.GetNullableString("CvvResponse"),
                Warning = reader.GetNullableString("Warning"),
                Amount = reader.GetDecimal(reader.GetOrdinal("Amount")),
                AmountPaid = reader.GetDecimal(reader.GetOrdinal("AmountPaid")),
                Currency = (MaktabDataContracts.Enums.Helcim.HelcimCurrency)reader.GetInt32("Currency"),
                InvoiceStatus = (MaktabDataContracts.Enums.Helcim.HelcimInvoiceStatus)reader.GetInt32("InvoiceStatus"),
                CardTransactionStatus = (MaktabDataContracts.Enums.Helcim.HelcimCardTransactionStatus)reader.GetInt32("CardTransactionStatus"),
                InvoiceType = (MaktabDataContracts.Enums.Helcim.HelcimInvoiceType)reader.GetInt32("InvoiceType"),
                CardTransactionType = (MaktabDataContracts.Enums.Helcim.HelcimCardTransactionType)reader.GetInt32("CardTransactionType"),
                RawResponse = reader.GetNullableString("RawResponse"),
                TransactionResponse = reader.GetNullableString("TransactionResponse"),
                CreatedAt = reader.GetNullableDateTimeUtc("CreatedAt"),
                UpdatedOn = reader.GetNullableDateTimeUtc("UpdatedOn"),
                DatePaid = reader.GetNullableDateTimeUtc("DatePaid"),
                IsActive = reader.GetBoolean("IsActive")
            };
        }

        private sealed class HelcimWebhookLogRow
        {
            public long HelcimWebhookLogId { get; set; }
            public HelcimWebhookProcessingStatus ProcessingStatus { get; set; }
            public DateTime? UpdatedOnUtc { get; set; }
        }
    }
}
