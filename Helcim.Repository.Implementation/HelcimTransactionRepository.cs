using System.Data.Common;
using Cumulus.Data;
using Data;
using MaktabDataContracts.Requests.Helcim;
using MaktabDataContracts.Responses.Helcim;

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
                    RawResponse
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
                    @RawResponse
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

            await cmd.ExecuteNonQueryAsync();
        }

        public Task<List<HelcimTransactionResponse>> GetByPaymentCode(string paymentCode)
            => GetByColumnAsync("PaymentCode", paymentCode);

        public Task<List<HelcimTransactionResponse>> GetByMaktabTransactionId(Guid maktabTransactionId)
            => GetByColumnAsync("MaktabTransactionId", maktabTransactionId.ToByteArray());

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
    }
}
