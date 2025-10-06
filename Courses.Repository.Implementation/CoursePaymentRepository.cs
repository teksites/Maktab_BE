using System.Data.Common;
using Cumulus.Data;
using Data;
using MaktabDataContracts.Requests.Course;
using MaktabDataContracts.Responses.Course;
using MaktabDataContracts.Enums;

namespace Courses.Repository.Implementation
{
    public class CoursePaymentRepository : DbRepository, ICoursePaymentRepository
    {
        public CoursePaymentRepository(IDatabase database) : base(database) { }

        // Add a new payment
        public async Task<CoursePaymentResponse> AddPayment(AddCoursePayment payment)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            var paymentId = Guid.NewGuid();

            cmd.CommandText = @"
                INSERT INTO course_payment 
                (CoursePaymentId, StudentCourseTransactionId, FamilyId, AmountPaid, Comments, PaymentMode, IsActive, CreatedAt, UpdatedOn)
                VALUES
                (@CoursePaymentId, @StudentCourseTransactionId, @FamilyId, @AmountPaid, @Comments, @PaymentMode, @IsActive, @CreatedAt, @UpdatedOn)";

            cmd.AddParameter("@CoursePaymentId", paymentId.ToByteArray());
            cmd.AddParameter("@StudentCourseTransactionId", payment.StudentCourseTransactionId.ToByteArray());
            cmd.AddParameter("@FamilyId", payment.FamilyId.ToByteArray());
            cmd.AddParameter("@AmountPaid", payment.AmountPaid);
            cmd.AddParameter("@Comments", payment.Comments);
            cmd.AddParameter("@PaymentMode", (int)payment.PaymentMode); // direct cast
            cmd.AddParameter("@IsActive", payment.IsActive);            // direct bool
            cmd.AddParameter("@CreatedAt", DateTime.UtcNow);
            cmd.AddParameter("@UpdatedOn", DateTime.UtcNow);

            await cmd.ExecuteNonQueryAsync();
            return await GetPayment(paymentId);
        }

        // Get payment by Id
        public async Task<CoursePaymentResponse> GetPayment(Guid paymentId)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"SELECT * FROM course_payment WHERE CoursePaymentId = @CoursePaymentId";
            cmd.AddParameter("@CoursePaymentId", paymentId.ToByteArray());

            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return null;

            return MapToPaymentResponse(reader);
        }

        // Get all payments for a student course transaction
        public async Task<IEnumerable<CoursePaymentResponse>> GetAllPayments(Guid studentCourseTransactionId)
        {
            var results = new List<CoursePaymentResponse>();
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"SELECT * FROM course_payment WHERE StudentCourseTransactionId = @TransactionId AND IsActive = TRUE";
            cmd.AddParameter("@TransactionId", studentCourseTransactionId.ToByteArray());

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(MapToPaymentResponse(reader));
            }

            return results;
        }

        // Update payment
        public async Task<bool> UpdatePayment(Guid paymentId, AddCoursePayment payment)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                UPDATE course_payment 
                SET AmountPaid = @AmountPaid, Comments = @Comments, PaymentMode = @PaymentMode, UpdatedOn = @UpdatedOn
                WHERE CoursePaymentId = @CoursePaymentId";

            cmd.AddParameter("@CoursePaymentId", paymentId.ToByteArray());
            cmd.AddParameter("@AmountPaid", payment.AmountPaid);
            cmd.AddParameter("@Comments", payment.Comments);
            cmd.AddParameter("@PaymentMode", (int)payment.PaymentMode);
            cmd.AddParameter("@UpdatedOn", DateTime.UtcNow);

            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        // Delete payment
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
                cmd.CommandText = @"UPDATE course_payment SET IsActive = FALSE, UpdatedOn = @UpdatedOn WHERE CoursePaymentId = @CoursePaymentId";
                cmd.AddParameter("@UpdatedOn", DateTime.UtcNow);
            }

            cmd.AddParameter("@CoursePaymentId", paymentId.ToByteArray());
            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        // Map DbDataReader to CoursePaymentResponse
        private CoursePaymentResponse MapToPaymentResponse(DbDataReader reader)
        {
            return new CoursePaymentResponse
            {
                PaymentId = reader.GetGuidFromByteArray("CoursePaymentId"),
                StudentCourseTransactionId = reader.GetGuidFromByteArray("StudentCourseTransactionId"),
                FamilyId = reader.GetGuidFromByteArray("FamilyId"),
                AmountPaid = reader.GetInt32("AmountPaid"),
                Comments = reader.IsDBNull("Comments") ? string.Empty : reader.GetString("Comments"),
                PaymentMode = (PaymentMode)reader.GetInt32("PaymentMode"), // direct cast
                IsActive = reader.GetBoolean("IsActive"),
                CreatedAt = reader.GetDateTime("CreatedAt"),
                UpdatedOn = reader.GetDateTime("UpdatedOn")
            };
        }
    }
}
