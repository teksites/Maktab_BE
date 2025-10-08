using System.Data;
using System.Data.Common;
using Data;
using MaktabDataContracts.Requests.Course;
using MaktabDataContracts.Responses.Course;
using MaktabDataContracts.Enums;

namespace Courses.Repository.Implementation
{
    public class CoursePaymentRepository : ICoursePaymentRepository
    {
        private readonly IDatabase _database;

        public CoursePaymentRepository(IDatabase database)
        {
            _database = database;
        }

        public async Task<CoursePaymentResponse> AddPayment(AddCoursePayment payment)
        {
            var paymentId = Guid.NewGuid();
            var sql = @"
                INSERT INTO course_payment
                (CoursePaymentId, StudentCourseTransactionId, FamilyId, AmountPaid, Comments, PaymentMode, IsActive, CreatedAt, UpdatedOn)
                VALUES
                (@Id, @TransactionId, @FamilyId, @AmountPaid, @Comments, @PaymentMode, @IsActive, NOW(), NOW())
            ";

            await ExecuteNonQueryAsync(sql, new
            {
                Id = paymentId,
                TransactionId = payment.StudentCourseTransactionId,
                payment.FamilyId,
                payment.AmountPaid,
                payment.Comments,
                PaymentMode = (int)payment.PaymentMode,
                payment.IsActive
            });

            return new CoursePaymentResponse
            {
                PaymentId = paymentId,
                StudentCourseTransactionId = payment.StudentCourseTransactionId,
                FamilyId = payment.FamilyId,
                AmountPaid = payment.AmountPaid,
                Comments = payment.Comments,
                PaymentMode = payment.PaymentMode,
                IsActive = payment.IsActive,
                CreatedAt = DateTime.UtcNow,
                UpdatedOn = DateTime.UtcNow
            };
        }

        public async Task<bool> UpdatePayment(Guid paymentId, AddCoursePayment payment)
        {
            var sql = @"
                UPDATE course_payment
                SET AmountPaid=@AmountPaid, Comments=@Comments, PaymentMode=@PaymentMode, IsActive=@IsActive, UpdatedOn=NOW()
                WHERE CoursePaymentId=@Id
            ";

            var rowsAffected = await ExecuteNonQueryAsync(sql, new
            {
                Id = paymentId,
                payment.AmountPaid,
                payment.Comments,
                PaymentMode = (int)payment.PaymentMode,
                payment.IsActive
            });

            return rowsAffected > 0;
        }

        public async Task<bool> DeletePayment(Guid paymentId, bool hardDelete = false)
        {
            if (hardDelete)
            {
                var sql = "DELETE FROM course_payment WHERE CoursePaymentId=@Id";
                var deleted = await ExecuteNonQueryAsync(sql, new { Id = paymentId });
                return deleted > 0;
            }
            else
            {
                var sql = "UPDATE course_payment SET IsActive=0, UpdatedOn=NOW() WHERE CoursePaymentId=@Id";
                var updated = await ExecuteNonQueryAsync(sql, new { Id = paymentId });
                return updated > 0;
            }
        }

        public async Task<CoursePaymentResponse?> GetPayment(Guid paymentId)
        {
            var sql = "SELECT * FROM course_payment WHERE CoursePaymentId=@Id AND IsActive=1";
            return await ExecuteReaderSingleAsync(sql, MapReaderToCoursePayment, new { Id = paymentId });
        }

        // Correct interface method
        public async Task<IEnumerable<CoursePaymentResponse>> GetAllPayments(Guid transactionId)
        {
            var sql = "SELECT * FROM course_payment WHERE StudentCourseTransactionId=@TransactionId AND IsActive=1";
            return await ExecuteReaderListAsync(sql, MapReaderToCoursePayment, new { TransactionId = transactionId });
        }

        public async Task<decimal> GetTotalPaidForTransaction(Guid transactionId)
        {
            var sql = "SELECT COALESCE(SUM(AmountPaid),0) FROM course_payment WHERE StudentCourseTransactionId=@TransactionId AND IsActive=1";
            return await ExecuteScalarDecimalAsync(sql, new { TransactionId = transactionId });
        }

        #region Helper Methods

        private async Task<int> ExecuteNonQueryAsync(string sql, object parameters)
        {
            await using var conn = await _database.CreateAndOpenConnectionAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            AddParameters(cmd, parameters);
            return await cmd.ExecuteNonQueryAsync();
        }

        private async Task<decimal> ExecuteScalarDecimalAsync(string sql, object parameters)
        {
            await using var conn = await _database.CreateAndOpenConnectionAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            AddParameters(cmd, parameters);

            var result = await cmd.ExecuteScalarAsync();
            return result == DBNull.Value || result == null ? 0 : Convert.ToDecimal(result);
        }

        private async Task<T> ExecuteReaderSingleAsync<T>(string sql, Func<DbDataReader, T> map, object parameters)
        {
            await using var conn = await _database.CreateAndOpenConnectionAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            AddParameters(cmd, parameters);

            await using var reader = await cmd.ExecuteReaderAsync();
            return await reader.ReadAsync() ? map(reader) : default!;
        }

        private async Task<List<T>> ExecuteReaderListAsync<T>(string sql, Func<DbDataReader, T> map, object parameters)
        {
            var list = new List<T>();
            await using var conn = await _database.CreateAndOpenConnectionAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            AddParameters(cmd, parameters);

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                list.Add(map(reader));

            return list;
        }

        public async Task<IEnumerable<CoursePaymentResponse>> GetAllPaymentsByTransaction(Guid transactionId)
        {
            var sql = "SELECT * FROM course_payment WHERE StudentCourseTransactionId=@TransactionId AND IsActive=1";
            return await ExecuteReaderListAsync(sql, MapReaderToCoursePayment, new { TransactionId = transactionId });
        }

        private void AddParameters(DbCommand cmd, object parameters)
        {
            if (parameters == null) return;

            foreach (var prop in parameters.GetType().GetProperties())
            {
                var dbParam = cmd.CreateParameter();
                dbParam.ParameterName = $"@{prop.Name}";
                dbParam.Value = prop.GetValue(parameters) ?? DBNull.Value;
                cmd.Parameters.Add(dbParam);
            }
        }

        private CoursePaymentResponse MapReaderToCoursePayment(DbDataReader reader) => new()
        {
            PaymentId = reader.GetGuid(reader.GetOrdinal("CoursePaymentId")),
            StudentCourseTransactionId = reader.GetGuid(reader.GetOrdinal("StudentCourseTransactionId")),
            FamilyId = reader.GetGuid(reader.GetOrdinal("FamilyId")),
            AmountPaid = reader.GetDecimal(reader.GetOrdinal("AmountPaid")),
            Comments = reader.IsDBNull(reader.GetOrdinal("Comments")) ? null : reader.GetString(reader.GetOrdinal("Comments")),
            PaymentMode = (PaymentMode)reader.GetInt32(reader.GetOrdinal("PaymentMode")),
            IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive")),
            CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
            UpdatedOn = reader.GetDateTime(reader.GetOrdinal("UpdatedOn"))
        };

        #endregion
    }
}
