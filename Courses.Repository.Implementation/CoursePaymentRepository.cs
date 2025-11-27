using System.Data;
using System.Data.Common;
using Cumulus.Data;
using Data;
using MaktabDataContracts.Enums;
using MaktabDataContracts.Requests.Course;
using MaktabDataContracts.Responses.Course;

namespace Courses.Repository.Implementation
{
    public class CoursePaymentRepository : DbRepository, ICoursePaymentRepository
    {
        public CoursePaymentRepository(IDatabase database) : base(database)
        {
        }

        public async Task<CoursePaymentResponse> AddPayment(AddCoursePayment payment)
        {
            var paymentId = Guid.NewGuid();
            var now = DateTime.UtcNow;

            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                INSERT INTO course_payment
                (
                    CoursePaymentId,
                    StudentCourseTransactionId,
                    FamilyId,
                    AmountPaid,
                    Comments,
                    PaymentMode,
                    IsActive,
                    CreatedAt,
                    UpdatedOn
                )
                VALUES
                (
                    @CoursePaymentId,
                    @StudentCourseTransactionId,
                    @FamilyId,
                    @AmountPaid,
                    @Comments,
                    @PaymentMode,
                    @IsActive,
                    @CreatedAt,
                    @UpdatedOn
                );";

            cmd.AddParameter("@CoursePaymentId", paymentId.ToByteArray());
            cmd.AddParameter("@StudentCourseTransactionId", payment.StudentCourseTransactionId.ToByteArray());
            cmd.AddParameter("@FamilyId", payment.FamilyId.ToByteArray());
            cmd.AddParameter("@AmountPaid", payment.AmountPaid);
            cmd.AddParameter("@Comments", (object?)payment.Comments ?? DBNull.Value);
            cmd.AddParameter("@PaymentMode", (int)payment.PaymentMode);
            cmd.AddParameter("@IsActive", payment.IsActive);
            cmd.AddParameter("@CreatedAt", now);
            cmd.AddParameter("@UpdatedOn", now);

            await cmd.ExecuteNonQueryAsync();

            return await GetPayment(paymentId);
        }

        public async Task<bool> UpdatePayment(Guid paymentId, AddCoursePayment payment)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                UPDATE course_payment
                SET
                    AmountPaid = @AmountPaid,
                    Comments   = @Comments,
                    PaymentMode = @PaymentMode,
                    IsActive   = @IsActive,
                    UpdatedOn  = @UpdatedOn
                WHERE CoursePaymentId = @CoursePaymentId";

            cmd.AddParameter("@CoursePaymentId", paymentId.ToByteArray());
            cmd.AddParameter("@AmountPaid", payment.AmountPaid);
            cmd.AddParameter("@Comments", (object?)payment.Comments ?? DBNull.Value);
            cmd.AddParameter("@PaymentMode", (int)payment.PaymentMode);
            cmd.AddParameter("@IsActive", payment.IsActive);
            cmd.AddParameter("@UpdatedOn", DateTime.UtcNow);

            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        public async Task<bool> DeletePayment(Guid paymentId, bool hardDelete = false)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            if (hardDelete)
            {
                cmd.CommandText = @"DELETE FROM course_payment WHERE CoursePaymentId = @CoursePaymentId";
            }
            else
            {
                cmd.CommandText = @"
                    UPDATE course_payment
                    SET IsActive = FALSE,
                        UpdatedOn = @UpdatedOn
                    WHERE CoursePaymentId = @CoursePaymentId";

                cmd.AddParameter("@UpdatedOn", DateTime.UtcNow);
            }

            cmd.AddParameter("@CoursePaymentId", paymentId.ToByteArray());

            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        public async Task<CoursePaymentResponse?> GetPayment(Guid paymentId)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"SELECT * FROM course_payment WHERE CoursePaymentId = @CoursePaymentId";
            cmd.AddParameter("@CoursePaymentId", paymentId.ToByteArray());

            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return null;

            return MapReaderToCoursePayment(reader);
        }

        public async Task<IEnumerable<CoursePaymentResponse>> GetAllPayments(Guid transactionId)
        {
            var results = new List<CoursePaymentResponse>();

            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                SELECT * 
                FROM course_payment 
                WHERE StudentCourseTransactionId = @StudentCourseTransactionId
                  AND IsActive = TRUE";

            cmd.AddParameter("@StudentCourseTransactionId", transactionId.ToByteArray());

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(MapReaderToCoursePayment(reader));
            }

            return results;
        }

        public async Task<decimal> GetTotalPaidForStudentTransaction(Guid transactionId)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                SELECT COALESCE(SUM(AmountPaid), 0) AS TotalPaid
                FROM course_payment
                WHERE StudentCourseTransactionId = @StudentCourseTransactionId
                  AND IsActive = TRUE";

            cmd.AddParameter("@StudentCourseTransactionId", transactionId.ToByteArray());

            var result = await cmd.ExecuteScalarAsync();
            if (result == null || result == DBNull.Value) return 0m;

            return Convert.ToDecimal(result);
        }

        public async Task<IEnumerable<CoursePaymentResponse>> GetAllPaymentsByStudentTransactionId(Guid studentTransactionId)
        {
            // same as GetAllPayments – just different method name
            return await GetAllPayments(studentTransactionId);
        }

        public async Task<IEnumerable<CoursePaymentResponse>> GetAllPaymentsByTransaction(Guid transactionId)
        {
            // keep for backward compatibility; same implementation
            return await GetAllPayments(transactionId);
        }

        private CoursePaymentResponse MapReaderToCoursePayment(DbDataReader reader)
        {
            return new CoursePaymentResponse
            {
                PaymentId = reader.GetGuidFromByteArray("CoursePaymentId"),
                StudentCourseTransactionId = reader.GetGuidFromByteArray("StudentCourseTransactionId"),
                FamilyId = reader.GetGuidFromByteArray("FamilyId"),
                AmountPaid = reader.GetDecimal("AmountPaid"),
                Comments = reader.GetNullableString("Comments"),
                PaymentMode = (PaymentMode)reader.GetInt32("PaymentMode"),
                IsActive = reader.GetBoolean("IsActive"),
                CreatedAt = reader.GetDateTime("CreatedAt"),
                UpdatedOn = reader.GetDateTime("UpdatedOn")
            };
        }
    }
}
