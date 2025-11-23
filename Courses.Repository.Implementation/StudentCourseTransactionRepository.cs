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

            return await MapToTransactionSingleResponse(reader);
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
                results.Add(await MapToTransactionSingleResponse(reader));
            }

            return results;
        }

        
        public async Task<StudentCourseTransactionResponse> GetTransactionByFamilyForCurrentSession(Guid familyId, Guid instituteId)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = "SELECT * FROM student_course_transaction WHERE FamilyId = @FamilyId and ";
            cmd.AddParameter("@FamilyId", familyId.ToByteArray());

            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return null;

            return await MapToTransactionSingleResponse(reader);
        }

        /*public async Task<IEnumerable<StudentCourseTransactionResponse>> GetTransactionsByFamilyForCourse(Guid courseId, Guid familyId)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
        SELECT
            -- Transaction fields
            sct.StudentCourseTransactionId,
            sct.FamilyId,
            sct.PayableFee,
            sct.DayCareFee,
            sct.AmountDiscounted,
            sct.TotalPayable,
            sct.Comments,
            sct.Status          AS TransactionStatus,
            sct.PaymentCode,
            sct.IsActive,
            sct.TotalAmountPaid,
            sct.IsCompletelyPaid,
            sct.CreatedAt,
            sct.UpdatedOn,

            -- Enrollment fields
            sce.StudentCourseEnrollmentId,
            sce.CourseEnrollmentGroupId,
            sce.CourseId,
            sce.FamilyId        AS EnrollmentFamilyId,
            sce.ChildId,
            sce.IsActive        AS EnrollmentIsActive,
            sce.WillUseDayCare,
            sce.DayCareDays,
            sce.CreatedAt       AS EnrollmentCreatedAt,
            sce.UpdatedOn       AS EnrollmentUpdatedOn

        FROM student_course_transaction sct
        JOIN student_course_transaction_enrollment scte
            ON sct.StudentCourseTransactionId = scte.StudentCourseTransactionId
        JOIN student_course_enrollment sce
            ON scte.StudentCourseEnrollmentId = sce.StudentCourseEnrollmentId

        WHERE
            sct.FamilyId = @FamilyId
            AND sce.CourseId = @CourseId

        ORDER BY
            sct.CreatedAt DESC,
            sce.CreatedAt;
    ";

            cmd.AddParameter("@FamilyId", familyId.ToByteArray());
            cmd.AddParameter("@CourseId", courseId.ToByteArray());

            using var reader = await cmd.ExecuteReaderAsync();

            if (!reader.HasRows)
                return Enumerable.Empty<StudentCourseTransactionResponse>();

            // ---- Cache ordinals (speed + safety) ----
            var ordTxId = reader.GetOrdinal("StudentCourseTransactionId");
            var ordTxFamilyId = reader.GetOrdinal("FamilyId");
            var ordPayableFee = reader.GetOrdinal("PayableFee");
            var ordDayCareFee = reader.GetOrdinal("DayCareFee");
            var ordAmountDiscounted = reader.GetOrdinal("AmountDiscounted");
            var ordTotalPayable = reader.GetOrdinal("TotalPayable");
            var ordComments = reader.GetOrdinal("Comments");
            var ordStatus = reader.GetOrdinal("TransactionStatus");
            var ordPaymentCode = reader.GetOrdinal("PaymentCode");
            var ordTxIsActive = reader.GetOrdinal("IsActive");
            var ordTotalAmountPaid = reader.GetOrdinal("TotalAmountPaid");
            var ordIsCompletelyPaid = reader.GetOrdinal("IsCompletelyPaid");
            var ordTxCreatedAt = reader.GetOrdinal("CreatedAt");
            var ordTxUpdatedOn = reader.GetOrdinal("UpdatedOn");

            var ordEnrollmentId = reader.GetOrdinal("StudentCourseEnrollmentId");
            var ordGroupId = reader.GetOrdinal("CourseEnrollmentGroupId");
            var ordCourseId = reader.GetOrdinal("CourseId");
            var ordEnrollmentFamilyId = reader.GetOrdinal("EnrollmentFamilyId");
            var ordChildId = reader.GetOrdinal("ChildId");
            var ordEnrollmentIsActive = reader.GetOrdinal("EnrollmentIsActive");
            var ordWillUseDayCare = reader.GetOrdinal("WillUseDayCare");
            var ordDayCareDays = reader.GetOrdinal("DayCareDays");
            var ordEnrollmentCreatedAt = reader.GetOrdinal("EnrollmentCreatedAt");
            var ordEnrollmentUpdatedOn = reader.GetOrdinal("EnrollmentUpdatedOn");

            // ---- Aggregate results ----
            var lookup = new Dictionary<Guid, StudentCourseTransactionResponse>();

            while (await reader.ReadAsync())
            {
                var txId = reader.GetGuid(ordTxId);

                if (!lookup.TryGetValue(txId, out var tx))
                {
                    tx = new StudentCourseTransactionResponse
                    {
                        StudentCourseTransactionId = txId,
                        FamilyId = reader.GetGuid(ordTxFamilyId),
                        PayableFee = reader.GetDecimal(ordPayableFee),
                        DayCareFee = reader.GetDecimal(ordDayCareFee),
                        AmountDiscounted = Convert.ToDecimal(reader.GetInt32(ordAmountDiscounted)),
                        TotalPayable = reader.GetDecimal(ordTotalPayable),
                        Comments = reader.IsDBNull(ordComments)
                                          ? string.Empty
                                          : reader.GetString(ordComments),
                        TransactionStatus = (TransactionStatus)reader.GetInt32(ordStatus),
                        PaymentCode = reader.GetString(ordPaymentCode),
                        IsActive = reader.GetBoolean(ordTxIsActive),
                        TotalAmountPaid = reader.GetDecimal(ordTotalAmountPaid),
                        IsCompletelyPaid = reader.GetBoolean(ordIsCompletelyPaid),
                        CreatedAt = reader.GetDateTime(ordTxCreatedAt),
                        UpdatedOn = reader.GetDateTime(ordTxUpdatedOn),
                        Enrollments = new List<StudentCourseEnrollmentResponse>()
                    };

                    // Primary enrollment at top-level
                    tx.StudentCourseEnrollmentId = reader.GetGuid(ordEnrollmentId);

                    lookup.Add(txId, tx);
                }

                // Add enrollment row
                tx.Enrollments.Add(new StudentCourseEnrollmentResponse
                {
                    StudentCourseEnrollmentId = reader.GetGuid(ordEnrollmentId),
                    CourseEnrollmentGroupId = reader.GetGuid(ordGroupId),
                    CourseId = reader.GetGuid(ordCourseId),
                    FamilyId = reader.GetGuid(ordEnrollmentFamilyId),
                    ChildId = reader.GetGuid(ordChildId),
                    IsActive = reader.GetBoolean(ordEnrollmentIsActive),
                    WillUseDayCare = reader.GetBoolean(ordWillUseDayCare),
                    DayCareDays = reader.GetInt32(ordDayCareDays),
                    CreatedAt = reader.GetDateTime(ordEnrollmentCreatedAt),
                    UpdatedOn = reader.GetDateTime(ordEnrollmentUpdatedOn)
                });
            }

            return lookup.Values.ToList();
        }*/

        public async Task<IEnumerable<StudentCourseTransactionResponse>> GetTransactionByFamily(Guid familyId)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
        SELECT
            -- Transaction fields
            sct.StudentCourseTransactionId,
            sct.FamilyId,
            sct.PayableFee,
            sct.DayCareFee,
            sct.AmountDiscounted,
            sct.TotalPayable,
            sct.Comments,
            sct.Status          AS TransactionStatus,
            sct.PaymentCode,
            sct.IsActive,
            sct.TotalAmountPaid,
            sct.IsCompletelyPaid,
            sct.CreatedAt,
            sct.UpdatedOn,

            -- Enrollment fields
            sce.StudentCourseEnrollmentId,
            sce.CourseEnrollmentGroupId,
            sce.CourseId,
            sce.FamilyId        AS EnrollmentFamilyId,
            sce.ChildId,
            sce.IsActive        AS EnrollmentIsActive,
            sce.WillUseDayCare,
            sce.DayCareDays,
            sce.CreatedAt       AS EnrollmentCreatedAt,
            sce.UpdatedOn       AS EnrollmentUpdatedOn

        FROM student_course_transaction sct
        JOIN student_course_transaction_enrollment scte
            ON sct.StudentCourseTransactionId = scte.StudentCourseTransactionId
        JOIN student_course_enrollment sce
            ON scte.StudentCourseEnrollmentId = sce.StudentCourseEnrollmentId
        JOIN course_enrollment_groups ceg
            ON sce.CourseEnrollmentGroupId = ceg.CourseEnrollmentGroupId

        WHERE
            sct.FamilyId = @FamilyId

        ORDER BY
            sct.CreatedAt DESC,
            sce.CreatedAt;
    ";

            cmd.AddParameter("@FamilyId", familyId.ToByteArray());

            using var reader = await cmd.ExecuteReaderAsync();
            return await MapToTransactionResponse(reader);
        }
        /*public async Task<IEnumerable<StudentCourseTransactionResponse>> GetTransactionByFamilyForInstitute(Guid familyId, Guid instituteId)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
        SELECT
            -- Transaction fields
            sct.StudentCourseTransactionId,
            sct.FamilyId,
            sct.PayableFee,
            sct.DayCareFee,
            sct.AmountDiscounted,
            sct.TotalPayable,
            sct.Comments,
            sct.Status          AS TransactionStatus,
            sct.PaymentCode,
            sct.IsActive,
            sct.TotalAmountPaid,
            sct.IsCompletelyPaid,
            sct.CreatedAt,
            sct.UpdatedOn,

            -- Enrollment fields
            sce.StudentCourseEnrollmentId,
            sce.CourseEnrollmentGroupId,
            sce.CourseId,
            sce.FamilyId        AS EnrollmentFamilyId,
            sce.ChildId,
            sce.IsActive        AS EnrollmentIsActive,
            sce.WillUseDayCare,
            sce.DayCareDays,
            sce.CreatedAt       AS EnrollmentCreatedAt,
            sce.UpdatedOn       AS EnrollmentUpdatedOn

        FROM student_course_transaction sct
        JOIN student_course_transaction_enrollment scte
            ON sct.StudentCourseTransactionId = scte.StudentCourseTransactionId
        JOIN student_course_enrollment sce
            ON scte.StudentCourseEnrollmentId = sce.StudentCourseEnrollmentId
        JOIN course_enrollment_groups ceg
            ON sce.CourseEnrollmentGroupId = ceg.CourseEnrollmentGroupId

        WHERE
            sct.FamilyId     = @FamilyId
            AND ceg.InstituteId = @InstituteId

        ORDER BY
            sct.CreatedAt DESC,
            sce.CreatedAt;
    ";

            cmd.AddParameter("@FamilyId", familyId.ToByteArray());
            cmd.AddParameter("@InstituteId", instituteId.ToByteArray());

            using var reader = await cmd.ExecuteReaderAsync();

            if (!reader.HasRows)
            {
                // Better to return empty list instead of null for IEnumerable
                return Enumerable.Empty<StudentCourseTransactionResponse>();
            }

            // Cache ordinals once
            var ordTxId = reader.GetOrdinal("StudentCourseTransactionId");
            var ordTxFamilyId = reader.GetOrdinal("FamilyId");
            var ordPayableFee = reader.GetOrdinal("PayableFee");
            var ordDayCareFee = reader.GetOrdinal("DayCareFee");
            var ordAmountDiscounted = reader.GetOrdinal("AmountDiscounted");
            var ordTotalPayable = reader.GetOrdinal("TotalPayable");
            var ordComments = reader.GetOrdinal("Comments");
            var ordStatus = reader.GetOrdinal("TransactionStatus");
            var ordPaymentCode = reader.GetOrdinal("PaymentCode");
            var ordTxIsActive = reader.GetOrdinal("IsActive");
            var ordTotalAmountPaid = reader.GetOrdinal("TotalAmountPaid");
            var ordIsCompletelyPaid = reader.GetOrdinal("IsCompletelyPaid");
            var ordTxCreatedAt = reader.GetOrdinal("CreatedAt");
            var ordTxUpdatedOn = reader.GetOrdinal("UpdatedOn");

            var ordEnrollmentId = reader.GetOrdinal("StudentCourseEnrollmentId");
            var ordGroupId = reader.GetOrdinal("CourseEnrollmentGroupId");
            var ordCourseId = reader.GetOrdinal("CourseId");
            var ordEnrollmentFamilyId = reader.GetOrdinal("EnrollmentFamilyId");
            var ordChildId = reader.GetOrdinal("ChildId");
            var ordEnrollmentIsActive = reader.GetOrdinal("EnrollmentIsActive");
            var ordWillUseDayCare = reader.GetOrdinal("WillUseDayCare");
            var ordDayCareDays = reader.GetOrdinal("DayCareDays");
            var ordEnrollmentCreatedAt = reader.GetOrdinal("EnrollmentCreatedAt");
            var ordEnrollmentUpdatedOn = reader.GetOrdinal("EnrollmentUpdatedOn");

            var lookup = new Dictionary<Guid, StudentCourseTransactionResponse>();

            while (await reader.ReadAsync())
            {
                var transactionId = reader.GetGuid(ordTxId);

                if (!lookup.TryGetValue(transactionId, out var tx))
                {
                    tx = new StudentCourseTransactionResponse
                    {
                        StudentCourseTransactionId = transactionId,
                        FamilyId = reader.GetGuid(ordTxFamilyId),
                        PayableFee = reader.GetDecimal(ordPayableFee),
                        DayCareFee = reader.GetDecimal(ordDayCareFee),

                        // DB is int, DTO is decimal; if needed you can change this mapping:
                        AmountDiscounted = Convert.ToDecimal(reader.GetInt32(ordAmountDiscounted)),

                        TotalPayable = reader.GetDecimal(ordTotalPayable),
                        Comments = reader.IsDBNull(ordComments)
                                          ? string.Empty
                                          : reader.GetString(ordComments),
                        TransactionStatus = (TransactionStatus)reader.GetInt32(ordStatus),
                        PaymentCode = reader.GetString(ordPaymentCode),
                        IsActive = reader.GetBoolean(ordTxIsActive),
                        TotalAmountPaid = reader.GetDecimal(ordTotalAmountPaid),
                        IsCompletelyPaid = reader.GetBoolean(ordIsCompletelyPaid),
                        CreatedAt = reader.GetDateTime(ordTxCreatedAt),
                        UpdatedOn = reader.GetDateTime(ordTxUpdatedOn),
                        Enrollments = new List<StudentCourseEnrollmentResponse>()
                    };

                    // Set a “primary” enrollment Id at top-level (first one encountered)
                    tx.StudentCourseEnrollmentId = reader.GetGuid(ordEnrollmentId);

                    lookup.Add(transactionId, tx);
                }

                var enrollment = new StudentCourseEnrollmentResponse
                {
                    StudentCourseEnrollmentId = reader.GetGuid(ordEnrollmentId),
                    CourseEnrollmentGroupId = reader.GetGuid(ordGroupId),
                    CourseId = reader.GetGuid(ordCourseId),
                    FamilyId = reader.GetGuid(ordEnrollmentFamilyId),
                    ChildId = reader.GetGuid(ordChildId),
                    IsActive = reader.GetBoolean(ordEnrollmentIsActive),
                    WillUseDayCare = reader.GetBoolean(ordWillUseDayCare),
                    DayCareDays = reader.GetInt32(ordDayCareDays),
                    CreatedAt = reader.GetDateTime(ordEnrollmentCreatedAt),
                    UpdatedOn = reader.GetDateTime(ordEnrollmentUpdatedOn)
                };

                tx.Enrollments.Add(enrollment);
            }

            return lookup.Values.ToList();
        }
*/

        public async Task<IEnumerable<StudentCourseTransactionResponse>> GetInstituteTransactionsByFamily(Guid familyId, Guid instituteId)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
        SELECT
            -- Transaction fields
            sct.StudentCourseTransactionId,
            sct.FamilyId,
            sct.PayableFee,
            sct.DayCareFee,
            sct.AmountDiscounted,
            sct.TotalPayable,
            sct.Comments,
            sct.Status          AS TransactionStatus,
            sct.PaymentCode,
            sct.IsActive,
            sct.TotalAmountPaid,
            sct.IsCompletelyPaid,
            sct.CreatedAt,
            sct.UpdatedOn,

            -- Enrollment fields
            sce.StudentCourseEnrollmentId,
            sce.CourseEnrollmentGroupId,
            sce.CourseId,
            sce.FamilyId        AS EnrollmentFamilyId,
            sce.ChildId,
            sce.IsActive        AS EnrollmentIsActive,
            sce.WillUseDayCare,
            sce.DayCareDays,
            sce.CreatedAt       AS EnrollmentCreatedAt,
            sce.UpdatedOn       AS EnrollmentUpdatedOn

        FROM student_course_transaction sct
        JOIN student_course_transaction_enrollment scte
            ON sct.StudentCourseTransactionId = scte.StudentCourseTransactionId
        JOIN student_course_enrollment sce
            ON scte.StudentCourseEnrollmentId = sce.StudentCourseEnrollmentId
        JOIN course_enrollment_groups ceg
            ON sce.CourseEnrollmentGroupId = ceg.CourseEnrollmentGroupId

        WHERE
            sct.FamilyId   = @FamilyId
            AND ceg.InstituteId = @InstituteId

        ORDER BY
            sct.CreatedAt DESC,
            sce.CreatedAt;
    ";

            cmd.AddParameter("@FamilyId", familyId.ToByteArray());
            cmd.AddParameter("@InstituteId", instituteId.ToByteArray());

            using var reader = await cmd.ExecuteReaderAsync();
            return await MapToTransactionResponse(reader);
        }

        public async Task<IEnumerable<StudentCourseTransactionResponse>> GetCourseTransactionsByFamily(Guid courseId, Guid familyId)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
        SELECT
            -- Transaction fields
            sct.StudentCourseTransactionId,
            sct.FamilyId,
            sct.PayableFee,
            sct.DayCareFee,
            sct.AmountDiscounted,
            sct.TotalPayable,
            sct.Comments,
            sct.Status          AS TransactionStatus,
            sct.PaymentCode,
            sct.IsActive,
            sct.TotalAmountPaid,
            sct.IsCompletelyPaid,
            sct.CreatedAt,
            sct.UpdatedOn,

            -- Enrollment fields
            sce.StudentCourseEnrollmentId,
            sce.CourseEnrollmentGroupId,
            sce.CourseId,
            sce.FamilyId        AS EnrollmentFamilyId,
            sce.ChildId,
            sce.IsActive        AS EnrollmentIsActive,
            sce.WillUseDayCare,
            sce.DayCareDays,
            sce.CreatedAt       AS EnrollmentCreatedAt,
            sce.UpdatedOn       AS EnrollmentUpdatedOn

        FROM student_course_transaction sct
        JOIN student_course_transaction_enrollment scte
            ON sct.StudentCourseTransactionId = scte.StudentCourseTransactionId
        JOIN student_course_enrollment sce
            ON scte.StudentCourseEnrollmentId = sce.StudentCourseEnrollmentId

        WHERE
            sct.FamilyId = @FamilyId
            AND sce.CourseId = @CourseId

        ORDER BY
            sct.CreatedAt DESC,
            sce.CreatedAt;
    ";

            cmd.AddParameter("@FamilyId", familyId.ToByteArray());
            cmd.AddParameter("@CourseId", courseId.ToByteArray());

            using var reader = await cmd.ExecuteReaderAsync();
            return await MapToTransactionResponse(reader);
        }

        public async Task<IEnumerable<StudentCourseTransactionResponse>> GetAllTransactionsByCourse(Guid courseId)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
        SELECT
            -- Transaction fields
            sct.StudentCourseTransactionId,
            sct.FamilyId,
            sct.PayableFee,
            sct.DayCareFee,
            sct.AmountDiscounted,
            sct.TotalPayable,
            sct.Comments,
            sct.Status          AS TransactionStatus,
            sct.PaymentCode,
            sct.IsActive,
            sct.TotalAmountPaid,
            sct.IsCompletelyPaid,
            sct.CreatedAt,
            sct.UpdatedOn,

            -- Enrollment fields
            sce.StudentCourseEnrollmentId,
            sce.CourseEnrollmentGroupId,
            sce.CourseId,
            sce.FamilyId        AS EnrollmentFamilyId,
            sce.ChildId,
            sce.IsActive        AS EnrollmentIsActive,
            sce.WillUseDayCare,
            sce.DayCareDays,
            sce.CreatedAt       AS EnrollmentCreatedAt,
            sce.UpdatedOn       AS EnrollmentUpdatedOn

        FROM student_course_transaction sct
        JOIN student_course_transaction_enrollment scte
            ON sct.StudentCourseTransactionId = scte.StudentCourseTransactionId
        JOIN student_course_enrollment sce
            ON scte.StudentCourseEnrollmentId = sce.StudentCourseEnrollmentId

        WHERE
            sce.CourseId = @CourseId

        ORDER BY
            sct.CreatedAt DESC,
            sce.CreatedAt;
    ";

            cmd.AddParameter("@CourseId", courseId.ToByteArray());

            using var reader = await cmd.ExecuteReaderAsync();
            return await MapToTransactionResponse(reader);
        }
        public async Task<IEnumerable<StudentCourseTransactionResponse>> GetAllTransactionsByInstitute(Guid instituteId)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
        SELECT
            -- Transaction fields
            sct.StudentCourseTransactionId,
            sct.FamilyId,
            sct.PayableFee,
            sct.DayCareFee,
            sct.AmountDiscounted,
            sct.TotalPayable,
            sct.Comments,
            sct.Status          AS TransactionStatus,
            sct.PaymentCode,
            sct.IsActive,
            sct.TotalAmountPaid,
            sct.IsCompletelyPaid,
            sct.CreatedAt,
            sct.UpdatedOn,

            -- Enrollment fields
            sce.StudentCourseEnrollmentId,
            sce.CourseEnrollmentGroupId,
            sce.CourseId,
            sce.FamilyId        AS EnrollmentFamilyId,
            sce.ChildId,
            sce.IsActive        AS EnrollmentIsActive,
            sce.WillUseDayCare,
            sce.DayCareDays,
            sce.CreatedAt       AS EnrollmentCreatedAt,
            sce.UpdatedOn       AS EnrollmentUpdatedOn

        FROM student_course_transaction sct
        JOIN student_course_transaction_enrollment scte
            ON sct.StudentCourseTransactionId = scte.StudentCourseTransactionId
        JOIN student_course_enrollment sce
            ON scte.StudentCourseEnrollmentId = sce.StudentCourseEnrollmentId
        JOIN course_enrollment_groups ceg
            ON sce.CourseEnrollmentGroupId = ceg.CourseEnrollmentGroupId

        WHERE
            ceg.InstituteId = @InstituteId

        ORDER BY
            sct.CreatedAt DESC,
            sce.CreatedAt;
    ";

            cmd.AddParameter("@InstituteId", instituteId.ToByteArray());

            using var reader = await cmd.ExecuteReaderAsync();
            return await MapToTransactionResponse(reader);
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


        private static async Task<IEnumerable<StudentCourseTransactionResponse>> MapToTransactionResponse(
    DbDataReader reader)
        {
            if (!reader.HasRows)
                return Enumerable.Empty<StudentCourseTransactionResponse>();

            // ---- Cache ordinals ----
            var ordTxId = reader.GetOrdinal("StudentCourseTransactionId");
            var ordTxFamilyId = reader.GetOrdinal("FamilyId");
            var ordPayableFee = reader.GetOrdinal("PayableFee");
            var ordDayCareFee = reader.GetOrdinal("DayCareFee");
            var ordAmountDiscounted = reader.GetOrdinal("AmountDiscounted");
            var ordTotalPayable = reader.GetOrdinal("TotalPayable");
            var ordComments = reader.GetOrdinal("Comments");
            var ordStatus = reader.GetOrdinal("TransactionStatus");
            var ordPaymentCode = reader.GetOrdinal("PaymentCode");
            var ordTxIsActive = reader.GetOrdinal("IsActive");
            var ordTotalAmountPaid = reader.GetOrdinal("TotalAmountPaid");
            var ordIsCompletelyPaid = reader.GetOrdinal("IsCompletelyPaid");
            var ordTxCreatedAt = reader.GetOrdinal("CreatedAt");
            var ordTxUpdatedOn = reader.GetOrdinal("UpdatedOn");

            var ordEnrollmentId = reader.GetOrdinal("StudentCourseEnrollmentId");
            var ordGroupId = reader.GetOrdinal("CourseEnrollmentGroupId");
            var ordCourseId = reader.GetOrdinal("CourseId");
            var ordEnrollmentFamilyId = reader.GetOrdinal("EnrollmentFamilyId");
            var ordChildId = reader.GetOrdinal("ChildId");
            var ordEnrollmentIsActive = reader.GetOrdinal("EnrollmentIsActive");
            var ordWillUseDayCare = reader.GetOrdinal("WillUseDayCare");
            var ordDayCareDays = reader.GetOrdinal("DayCareDays");
            var ordEnrollmentCreatedAt = reader.GetOrdinal("EnrollmentCreatedAt");
            var ordEnrollmentUpdatedOn = reader.GetOrdinal("EnrollmentUpdatedOn");

            var lookup = new Dictionary<Guid, StudentCourseTransactionResponse>();

            while (await reader.ReadAsync())
            {
                var txId = reader.GetGuid(ordTxId);

                if (!lookup.TryGetValue(txId, out var tx))
                {
                    tx = new StudentCourseTransactionResponse
                    {
                        StudentCourseTransactionId = txId,
                        FamilyId = reader.GetGuid(ordTxFamilyId),
                        PayableFee = reader.GetDecimal(ordPayableFee),
                        DayCareFee = reader.GetDecimal(ordDayCareFee),

                        // DB: int, DTO: decimal
                        AmountDiscounted = Convert.ToDecimal(reader.GetInt32(ordAmountDiscounted)),

                        TotalPayable = reader.GetDecimal(ordTotalPayable),
                        Comments = reader.IsDBNull(ordComments)
                                          ? string.Empty
                                          : reader.GetString(ordComments),
                        TransactionStatus = (TransactionStatus)reader.GetInt32(ordStatus),
                        PaymentCode = reader.GetString(ordPaymentCode),
                        IsActive = reader.GetBoolean(ordTxIsActive),
                        TotalAmountPaid = reader.GetDecimal(ordTotalAmountPaid),
                        IsCompletelyPaid = reader.GetBoolean(ordIsCompletelyPaid),
                        CreatedAt = reader.GetDateTime(ordTxCreatedAt),
                        UpdatedOn = reader.GetDateTime(ordTxUpdatedOn),
                        Enrollments = new List<StudentCourseEnrollmentResponse>()
                    };

                    // “Primary” enrollment Id at top-level
                    tx.StudentCourseEnrollmentId = reader.GetGuid(ordEnrollmentId);

                    lookup.Add(txId, tx);
                }

                var enrollment = new StudentCourseEnrollmentResponse
                {
                    StudentCourseEnrollmentId = reader.GetGuid(ordEnrollmentId),
                    CourseEnrollmentGroupId = reader.GetGuid(ordGroupId),
                    CourseId = reader.GetGuid(ordCourseId),
                    FamilyId = reader.GetGuid(ordEnrollmentFamilyId),
                    ChildId = reader.GetGuid(ordChildId),
                    IsActive = reader.GetBoolean(ordEnrollmentIsActive),
                    WillUseDayCare = reader.GetBoolean(ordWillUseDayCare),
                    DayCareDays = reader.GetInt32(ordDayCareDays),
                    CreatedAt = reader.GetDateTime(ordEnrollmentCreatedAt),
                    UpdatedOn = reader.GetDateTime(ordEnrollmentUpdatedOn)
                };

                tx.Enrollments.Add(enrollment);
            }

            return lookup.Values.ToList();
        }

        private async Task<StudentCourseTransactionResponse> MapToTransactionSingleResponse(DbDataReader reader)
        {
            return new StudentCourseTransactionResponse
            {
                //StudentCourseEnrollmentId = reader.GetGuidFromByteArray("StudentCourseEnrollmentId"),
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
                results.Add(await MapToTransactionSingleResponse(reader));
            }

            return results;
        }

    }
}
