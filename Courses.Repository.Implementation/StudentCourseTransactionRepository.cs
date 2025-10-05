using Cumulus.Data;
using Data;
using MaktabDataContracts.Requests.Course;
using MaktabDataContracts.Responses.Course;
using Newtonsoft.Json;

namespace Courses.Repository.Implementation
{
    public class StudentCourseTransactionRepository : DbRepository, IStudentCourseTransactionRepository
    {
        public StudentCourseTransactionRepository(IDatabase database) : base(database) { }

        public async Task<StudentCourseTransactionResponse> AddTransaction(AddStudentCourseTransaction transaction)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                INSERT INTO student_course_transaction 
                (StudentCourseTransactionId, StudentCourseEnrollmentId, FamilyId, PayableFee, DayCareFee, 
                 AmountDiscounted, TotalPayable, Comments, TransactionStatus, PaymentCode, 
                 IsActive, CreatedAt, UpdatedOn, IsCompletelyPaid, TotalAmountPaid)
                VALUES 
                (@StudentCourseTransactionId, @StudentCourseEnrollmentId, @FamilyId, @PayableFee, @DayCareFee, 
                 @AmountDiscounted, @TotalPayable, @Comments, @TransactionStatus, @PaymentCode, 
                 @IsActive, @CreatedAt, @UpdatedOn, @IsCompletelyPaid, @TotalAmountPaid)";

            var transactionId = transaction.StudentCourseTransactionId != Guid.Empty
                ? transaction.StudentCourseTransactionId
                : Guid.NewGuid();

            cmd.AddParameter("@StudentCourseTransactionId", transactionId.ToByteArray());
            cmd.AddParameter("@StudentCourseEnrollmentId", JsonConvert.SerializeObject(transaction.StudentCourseEnrollmentId));
            cmd.AddParameter("@FamilyId", transaction.FamilyId.ToByteArray());
            cmd.AddParameter("@PayableFee", transaction.PayableFee);
            cmd.AddParameter("@DayCareFee", transaction.DayCareFee);
            cmd.AddParameter("@AmountDiscounted", transaction.AmountDiscounted);
            cmd.AddParameter("@TotalPayable", transaction.TotalPayable);
            cmd.AddParameter("@Comments", transaction.Comments ?? string.Empty);
            cmd.AddParameter("@TransactionStatus", (int)transaction.TransactionStatus);
            cmd.AddParameter("@PaymentCode", transaction.PaymentCode ?? string.Empty);
            cmd.AddParameter("@IsActive", transaction.IsActive);
            cmd.AddParameter("@CreatedAt", DateTime.UtcNow);
            cmd.AddParameter("@UpdatedOn", DateTime.UtcNow);
            cmd.AddParameter("@IsCompletelyPaid", transaction.IsCompletelyPaid);
            cmd.AddParameter("@TotalAmountPaid", transaction.TotalAmountPaid);

            await cmd.ExecuteNonQueryAsync();

            return await GetTransaction(transactionId);
        }

        public async Task<StudentCourseTransactionResponse?> GetTransaction(Guid transactionId)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"SELECT * FROM student_course_transaction WHERE StudentCourseTransactionId = @Id";
            cmd.AddParameter("@Id", transactionId.ToByteArray());

            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                return null;

            return MapToTransactionResponse(reader);
        }

        public async Task<IEnumerable<StudentCourseTransactionResponse>> GetAllTransactions(Guid familyId)
        {
            var results = new List<StudentCourseTransactionResponse>();

            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"SELECT * FROM student_course_transaction WHERE FamilyId = @FamilyId AND IsActive = TRUE";
            cmd.AddParameter("@FamilyId", familyId.ToByteArray());

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(MapToTransactionResponse(reader));
            }

            return results;
        }

        public async Task<bool> UpdateTransaction(Guid transactionId, AddStudentCourseTransaction transaction)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                UPDATE student_course_transaction 
                SET StudentCourseEnrollmentId = @StudentCourseEnrollmentId,
                    PayableFee = @PayableFee,
                    DayCareFee = @DayCareFee,
                    AmountDiscounted = @AmountDiscounted,
                    TotalPayable = @TotalPayable,
                    Comments = @Comments,
                    TransactionStatus = @TransactionStatus,
                    PaymentCode = @PaymentCode,
                    IsActive = @IsActive,
                    UpdatedOn = @UpdatedOn,
                    IsCompletelyPaid = @IsCompletelyPaid,
                    TotalAmountPaid = @TotalAmountPaid
                WHERE StudentCourseTransactionId = @Id";

            cmd.AddParameter("@Id", transactionId.ToByteArray());
            cmd.AddParameter("@StudentCourseEnrollmentId", JsonConvert.SerializeObject(transaction.StudentCourseEnrollmentId));
            cmd.AddParameter("@PayableFee", transaction.PayableFee);
            cmd.AddParameter("@DayCareFee", transaction.DayCareFee);
            cmd.AddParameter("@AmountDiscounted", transaction.AmountDiscounted);
            cmd.AddParameter("@TotalPayable", transaction.TotalPayable);
            cmd.AddParameter("@Comments", transaction.Comments ?? string.Empty);
            cmd.AddParameter("@TransactionStatus", (int)transaction.TransactionStatus);
            cmd.AddParameter("@PaymentCode", transaction.PaymentCode ?? string.Empty);
            cmd.AddParameter("@IsActive", transaction.IsActive);
            cmd.AddParameter("@UpdatedOn", DateTime.UtcNow);
            cmd.AddParameter("@IsCompletelyPaid", transaction.IsCompletelyPaid);
            cmd.AddParameter("@TotalAmountPaid", transaction.TotalAmountPaid);

            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        public async Task<bool> DeleteTransaction(Guid transactionId, bool hardDelete = false)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            if (hardDelete)
                cmd.CommandText = @"DELETE FROM student_course_transaction WHERE StudentCourseTransactionId = @Id";
            else
                cmd.CommandText = @"UPDATE student_course_transaction SET IsActive = FALSE WHERE StudentCourseTransactionId = @Id";

            cmd.AddParameter("@Id", transactionId.ToByteArray());
            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        private StudentCourseTransactionResponse MapToTransactionResponse(dynamic reader)
        {
            return new StudentCourseTransactionResponse
            {
                StudentCourseTransactionId = reader.GetGuidFromByteArray("StudentCourseTransactionId"),
                StudentCourseEnrollmentId = JsonConvert.DeserializeObject<List<Guid>>(reader.GetString("StudentCourseEnrollmentId")) ?? new List<Guid>(),
                FamilyId = reader.GetGuidFromByteArray("FamilyId"),
                PayableFee = reader.GetInt32("PayableFee"),
                DayCareFee = reader.GetInt32("DayCareFee"),
                AmountDiscounted = reader.GetInt32("AmountDiscounted"),
                TotalPayable = reader.GetInt32("TotalPayable"),
                Comments = reader.IsDBNull("Comments") ? string.Empty : reader.GetString("Comments"),
                TransactionStatus = (MaktabDataContracts.Enums.TransactionStatus)reader.GetInt32("TransactionStatus"),
                PaymentCode = reader.GetString("PaymentCode"),
                IsActive = reader.GetBoolean("IsActive"),
                CreatedAt = reader.GetDateTime("CreatedAt"),
                UpdatedOn = reader.GetDateTime("UpdatedOn"),
                IsCompletelyPaid = reader.GetBoolean("IsCompletelyPaid"),
                TotalAmountPaid = reader.GetInt32("TotalAmountPaid")
            };
        }
    }
}
