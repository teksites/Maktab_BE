using System.Data;
using Cumulus.Data;
using Data;
using MaktabDataContracts.Requests.Course;
using MaktabDataContracts.Responses.Course;
using MaktabDataContracts.Responses.Transactions;
using MaktabDataContracts.Enums;
using System.Data.Common;

namespace Courses.Repository.Implementation
{
    public class StudentCourseTransactionRepository : DbRepository, IStudentCourseTransactionRepository
    {
        public StudentCourseTransactionRepository(IDatabase database) : base(database) { }

        // ----------------------------
        // Add Transaction
        // ----------------------------
        public async Task<StudentCourseTransactionResponse> AddTransaction(AddStudentCourseTransaction transaction)
        {
            var transactionId = Guid.NewGuid();

            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                INSERT INTO student_course_transaction
                (StudentCourseTransactionId, FamilyId, PayableFee, DayCareFee, AmountDiscounted, TotalPayable, Comments, Status, PaymentCode, IsActive, TotalAmountPaid, IsCompletelyPaid, CreatedAt, UpdatedOn)
                VALUES
                (@TransactionId, @FamilyId, @PayableFee, @DayCareFee, @AmountDiscounted, @TotalPayable, @Comments, @Status, @PaymentCode, @IsActive, @TotalAmountPaid, @IsCompletelyPaid, @CreatedAt, @UpdatedOn)
            ";

            cmd.AddParameter("@TransactionId", transactionId.ToByteArray())
               .AddParameter("@FamilyId", transaction.FamilyId.ToByteArray())
               .AddParameter("@PayableFee", transaction.PayableFee)
               .AddParameter("@DayCareFee", transaction.DayCareFee)
               .AddParameter("@AmountDiscounted", transaction.AmountDiscounted)
               .AddParameter("@TotalPayable", transaction.TotalPayable)
               .AddParameter("@Comments", transaction.Comments)
               .AddParameter("@Status", (int)transaction.TransactionStatus)
               .AddParameter("@PaymentCode", transaction.PaymentCode)
               .AddParameter("@IsActive", transaction.IsActive)
               .AddParameter("@TotalAmountPaid", transaction.TotalAmountPaid)
               .AddParameter("@IsCompletelyPaid", transaction.IsCompletelyPaid)
               .AddParameter("@CreatedAt", DateTime.UtcNow)
               .AddParameter("@UpdatedOn", DateTime.UtcNow);

            await cmd.ExecuteNonQueryAsync();

            if (transaction.StudentCourseEnrollmentIds?.Count > 0)
                await AddEnrollmentsToTransaction(transactionId, transaction.StudentCourseEnrollmentIds);

            return await GetTransaction(transactionId) ?? throw new Exception("Failed to retrieve transaction");
        }

        // ----------------------------
        // Get Transaction by ID
        // ----------------------------
        public async Task<StudentCourseTransactionResponse?> GetTransaction(Guid transactionId)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = "SELECT * FROM student_course_transaction WHERE StudentCourseTransactionId = @TransactionId";
            cmd.AddParameter("@TransactionId", transactionId.ToByteArray());

            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return null;

            return await MapToTransactionResponse(reader);
        }

        // ----------------------------
        // Update Transaction
        // ----------------------------
        public async Task<bool> UpdateTransaction(Guid transactionId, AddStudentCourseTransaction transaction)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                UPDATE student_course_transaction
                SET FamilyId = @FamilyId,
                    PayableFee = @PayableFee,
                    DayCareFee = @DayCareFee,
                    AmountDiscounted = @AmountDiscounted,
                    TotalPayable = @TotalPayable,
                    Comments = @Comments,
                    Status = @Status,
                    PaymentCode = @PaymentCode,
                    IsActive = @IsActive,
                    TotalAmountPaid = @TotalAmountPaid,
                    IsCompletelyPaid = @IsCompletelyPaid,
                    UpdatedOn = @UpdatedOn
                WHERE StudentCourseTransactionId = @TransactionId
            ";

            cmd.AddParameter("@TransactionId", transactionId.ToByteArray())
               .AddParameter("@FamilyId", transaction.FamilyId.ToByteArray())
               .AddParameter("@PayableFee", transaction.PayableFee)
               .AddParameter("@DayCareFee", transaction.DayCareFee)
               .AddParameter("@AmountDiscounted", transaction.AmountDiscounted)
               .AddParameter("@TotalPayable", transaction.TotalPayable)
               .AddParameter("@Comments", transaction.Comments)
               .AddParameter("@Status", (int)transaction.TransactionStatus)
               .AddParameter("@PaymentCode", transaction.PaymentCode)
               .AddParameter("@IsActive", transaction.IsActive)
               .AddParameter("@TotalAmountPaid", transaction.TotalAmountPaid)
               .AddParameter("@IsCompletelyPaid", transaction.IsCompletelyPaid)
               .AddParameter("@UpdatedOn", DateTime.UtcNow);

            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        // ----------------------------
        // Delete Transaction
        // ----------------------------
        public async Task<bool> DeleteTransaction(Guid transactionId, bool hardDelete = false)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            if (hardDelete)
                cmd.CommandText = "DELETE FROM student_course_transaction WHERE StudentCourseTransactionId = @TransactionId";
            else
                cmd.CommandText = "UPDATE student_course_transaction SET IsActive = 0, UpdatedOn = @UpdatedOn WHERE StudentCourseTransactionId = @TransactionId";

            cmd.AddParameter("@TransactionId", transactionId.ToByteArray());
            if (!hardDelete) cmd.AddParameter("@UpdatedOn", DateTime.UtcNow);

            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        // ----------------------------
        // Get All Transactions
        // ----------------------------
        public async Task<IEnumerable<StudentCourseTransactionResponse>> GetAllTransactions()
        {
            var results = new List<StudentCourseTransactionResponse>();
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = "SELECT * FROM student_course_transaction";

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(await MapToTransactionResponse(reader));
            }

            return results;
        }

        public async Task<IEnumerable<StudentCourseTransactionResponse>> GetAllTransactionsByFamily(Guid familyId)
            => await GetTransactionsByColumn("FamilyId", familyId);

        public async Task<IEnumerable<StudentCourseTransactionResponse>> GetAllTransactionsByCourse(Guid courseId)
            => await GetTransactionsByColumn("CourseId", courseId);

        public async Task<IEnumerable<StudentCourseTransactionResponse>> GetAllTransactionsByEnrollment(Guid enrollmentId)
        {
            var results = new List<StudentCourseTransactionResponse>();
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                SELECT t.* 
                FROM student_course_transaction t
                INNER JOIN student_course_enrollment e ON t.StudentCourseTransactionId = e.StudentCourseTransactionId
                WHERE e.StudentCourseEnrollmentId = @EnrollmentId";
            cmd.AddParameter("@EnrollmentId", enrollmentId.ToByteArray());

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(await MapToTransactionResponse(reader));
            }

            return results;
        }

        // ----------------------------
        // Enrollments
        // ----------------------------
        public async Task<bool> AddEnrollmentsToTransaction(Guid transactionId, IEnumerable<Guid> enrollmentIds)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            var values = enrollmentIds.Select(e => $"('{transactionId}', '{e}')");
            cmd.CommandText = $"INSERT INTO student_course_enrollment (StudentCourseTransactionId, StudentCourseEnrollmentId) VALUES {string.Join(",", values)}";

            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        public async Task<IEnumerable<StudentCourseEnrollmentResponse>> GetEnrollmentsForTransaction(Guid transactionId)
        {
            var results = new List<StudentCourseEnrollmentResponse>();
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = "SELECT * FROM student_course_enrollment WHERE StudentCourseTransactionId = @TransactionId";
            cmd.AddParameter("@TransactionId", transactionId.ToByteArray());

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(new StudentCourseEnrollmentResponse
                {
                    StudentCourseEnrollmentId = reader.GetGuidFromByteArray("StudentCourseEnrollmentId")
                });
            }

            return results;
        }

        // ----------------------------
        // Payments
        // ----------------------------
        public async Task<IEnumerable<StudentCoursePaymentResponse>> GetPaymentsByFamilyAsync(Guid familyId)
        {
            var results = new List<StudentCoursePaymentResponse>();
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                SELECT p.* 
                FROM course_payment p
                INNER JOIN student_course_transaction t ON p.StudentCourseTransactionId = t.StudentCourseTransactionId
                WHERE p.FamilyId = @FamilyId";
            cmd.AddParameter("@FamilyId", familyId.ToByteArray());

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(new StudentCoursePaymentResponse
                {
                    PaymentId = reader.GetGuidFromByteArray("CoursePaymentId"),
                    StudentCourseTransactionId = reader.GetGuidFromByteArray("StudentCourseTransactionId"),
                    AmountPaid = reader.GetDecimal("AmountPaid"),
                    PaymentMode = (PaymentMode)reader.GetInt32("PaymentMode"),
                    PaidOn = reader.GetDateTime("CreatedAt")
                });
            }

            return results;
        }

        // ----------------------------
        // Pending Amounts
        // ----------------------------
        public async Task<IEnumerable<PendingAmountResponse>> GetPendingAmountsReportAsync(
            Guid? instituteId = null, Guid? courseId = null, Guid? courseGroupId = null, Guid? familyId = null, string? paymentCode = null)
        {
            var results = new List<PendingAmountResponse>();
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            var filters = new List<string>();
            if (instituteId.HasValue) { filters.Add("t.InstituteId = @InstituteId"); cmd.AddParameter("@InstituteId", instituteId.Value.ToByteArray()); }
            if (courseId.HasValue) { filters.Add("t.CourseId = @CourseId"); cmd.AddParameter("@CourseId", courseId.Value.ToByteArray()); }
            if (courseGroupId.HasValue) { filters.Add("t.CourseGroupId = @CourseGroupId"); cmd.AddParameter("@CourseGroupId", courseGroupId.Value.ToByteArray()); }
            if (familyId.HasValue) { filters.Add("t.FamilyId = @FamilyId"); cmd.AddParameter("@FamilyId", familyId.Value.ToByteArray()); }
            if (string.IsNullOrEmpty(paymentCode)) { filters.Add("t.PaymentCode = @PaymentCode"); cmd.AddParameter("@PaymentCode", paymentCode); }

            var whereClause = filters.Count > 0 ? "WHERE " + string.Join(" AND ", filters) : string.Empty;

            cmd.CommandText = $@"
                SELECT t.InstituteId, i.Name AS InstituteName,
                       t.CourseId, c.Name AS CourseName,
                       t.CourseGroupId AS CourseEnrollmentGroupId, g.Name AS CourseGroupName,
                       t.FamilyId,
                       t.PaymentCode,
                       SUM(t.TotalPayable - t.TotalAmountPaid) AS PendingAmount
                FROM student_course_transaction t
                LEFT JOIN institute i ON t.InstituteId = i.InstituteId
                LEFT JOIN course c ON t.CourseId = c.CourseId
                LEFT JOIN course_enrollment_group g ON t.CourseGroupId = g.CourseEnrollmentGroupId
                {whereClause}
                GROUP BY t.InstituteId, i.Name, t.CourseId, c.Name, t.CourseGroupId, g.Name, t.FamilyId, t.PaymentCode
            ";

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(new PendingAmountResponse
                {
                    InstituteId = reader.GetGuidFromByteArray("InstituteId"),
                    InstituteName = reader.GetString("InstituteName"),
                    CourseId = reader.GetGuidFromByteArray("CourseId"),
                    CourseName = reader.GetString("CourseName"),
                    CourseEnrollmentGroupId = reader.GetGuidFromByteArray("CourseEnrollmentGroupId"),
                    CourseGroupName = reader.GetString("CourseGroupName"),
                    FamilyId = reader.GetGuidFromByteArray("FamilyId"),
                    PendingAmount = reader.GetDecimal("PendingAmount")
                });
            }

            return results;
        }

        // ----------------------------
        // Get Transactions Per Course
        // ----------------------------
        public async Task<IEnumerable<StudentCourseTransactionResponse>> GetTransactionsPerCourseAsync(Guid courseId)
        {
            return await GetTransactionsByColumn("CourseId", courseId);
        }

        // ----------------------------
        // Pending Amount Helpers
        // ----------------------------
        public async Task<decimal> GetPendingAmountByInstitute(Guid instituteId)
        {
            var pending = await GetPendingAmountsReportAsync(instituteId: instituteId);
            return pending.Sum(p => p.PendingAmount);
        }

        public async Task<decimal> GetPendingAmountByFamily(Guid familyId)
        {
            var pending = await GetPendingAmountsReportAsync(familyId: familyId);
            return pending.Sum(p => p.PendingAmount);
        }

        public async Task<decimal> GetPendingAmountByCourseGroup(Guid courseGroupId)
        {
            var pending = await GetPendingAmountsReportAsync(courseGroupId: courseGroupId);
            return pending.Sum(p => p.PendingAmount);
        }

        public async Task<decimal> GetPendingAmountByCourse(Guid courseId)
        {
            var pending = await GetPendingAmountsReportAsync(courseId: courseId);
            return pending.Sum(p => p.PendingAmount);
        }

        //public async Task<StudentCoursePaymentResponse> GetStudentCourseTransactionByPaymentCode(string paymentCode)
        //{
        //    var pending = await GetPendingAmountsReportAsync(paymentCode: paymentCode);
        //    return pending.Sum(p => p.PendingAmount);
        //}


        /*
                public async Task<IEnumerable<StudentCourseTransactionResponse>> GetTransactionsPerCourseAsync(Guid courseId)
                {
                    var results = new List<StudentCourseTransactionResponse>();
                    using var conn = await Database.CreateAndOpenConnectionAsync();
                    using var cmd = conn.CreateCommand();

                    cmd.CommandText = @"SELECT * FROM student_course_transaction WHERE CourseId=@CourseId AND IsActive=TRUE";
                    cmd.AddParameter("@CourseId", courseId.ToByteArray());

                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        results.Add(await MapToTransactionResponse(reader));
                    }

                    return results;
                }

                // Pending amount calculations
                public async Task<decimal> GetPendingAmountByCourse(Guid courseId)
                {
                    return await GetPendingAmount(@"SELECT SUM(AmountDue - AmountPaid) FROM student_course_transaction WHERE CourseId=@CourseId AND IsActive=TRUE",
                                                  ("@CourseId", courseId));
                }

                public async Task<decimal> GetPendingAmountByFamily(Guid familyId)
                {
                    return await GetPendingAmount(@"SELECT SUM(AmountDue - AmountPaid) FROM student_course_transaction WHERE FamilyId=@FamilyId AND IsActive=TRUE",
                                                  ("@FamilyId", familyId));
                }

                public async Task<decimal> GetPendingAmountByCourseGroup(Guid courseGroupId)
                {
                    return await GetPendingAmount(@"SELECT SUM(AmountDue - AmountPaid) FROM student_course_transaction WHERE CourseEnrollmentGroupId=@CourseGroupId AND IsActive=TRUE",
                                                  ("@CourseGroupId", courseGroupId));
                }

                public async Task<decimal> GetPendingAmountByInstitute(Guid instituteId)
                {
                    return await GetPendingAmount(@"SELECT SUM(AmountDue - AmountPaid) FROM student_course_transaction t
                                           INNER JOIN course c ON t.CourseId = c.CourseId
                                           WHERE c.InstituteId=@InstituteId AND t.IsActive=TRUE",
                                                  ("@InstituteId", instituteId));
                }
        */
        // Reusable helper
        private async Task<decimal> GetPendingAmount(string query, (string name, Guid value) param)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = query;
            cmd.AddParameter(param.name, param.value.ToByteArray());

            var result = await cmd.ExecuteScalarAsync();
            return result == DBNull.Value ? 0 : Convert.ToDecimal(result);
        }

        // ----------------------------
        // Private Helpers
        // ----------------------------

        
        private async Task<StudentCourseTransactionResponse> MapToTransactionResponse(DbDataReader reader)
        {
            return new StudentCourseTransactionResponse
            {
                StudentCourseEnrollmentId = reader.GetGuidFromByteArray("StudentCourseEnrollmentId"),
                StudentCourseTransactionId = reader.GetGuidFromByteArray("StudentCourseTransactionId"),
                FamilyId = reader.GetGuidFromByteArray("FamilyId"),
                PayableFee = reader.GetDecimal("PayableFee"),
                DayCareFee = reader.GetDecimal("DayCareFee"),
                AmountDiscounted = reader.GetDecimal("AmountDiscounted"),
                TotalPayable = reader.GetDecimal("TotalPayable"),
                TotalAmountPaid = reader.GetDecimal("TotalAmountPaid"),
                Comments = reader.GetString("Comments"),
                PaymentCode = reader.GetString("PaymentCode"),
                TransactionStatus = (TransactionStatus)reader.GetInt32("Status"),
                IsActive = reader.GetBoolean("IsActive"),
                IsCompletelyPaid = reader.GetBoolean("IsCompletelyPaid"),
                CreatedAt = reader.GetDateTime("CreatedAt"),
                UpdatedOn = reader.GetDateTime("UpdatedOn")
            };
        }

        private async Task<IEnumerable<StudentCourseTransactionResponse>> GetTransactionsByColumn(string columnName, Guid columnValue)
        {
            var results = new List<StudentCourseTransactionResponse>();
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = $"SELECT * FROM student_course_transaction WHERE {columnName} = @{columnName}";
            cmd.AddParameter($"@{columnName}", columnValue.ToByteArray());

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(await MapToTransactionResponse(reader));
            }

            return results;
        }
    }
}
