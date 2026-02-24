using System.Data;
using Cumulus.Data;
using Data;
using MaktabDataContracts.Requests.Course;
using MaktabDataContracts.Responses.Course;
using MaktabDataContracts.Responses.Transactions;
using MaktabDataContracts.Enums;
using System.Data.Common;
using System.Security.Cryptography;
using TransactionStatus = MaktabDataContracts.Enums.TransactionStatus;

namespace Courses.Repository.Implementation
{
    public class StudentCourseTransactionRepository : DbRepository, IStudentCourseTransactionRepository
    {
        public StudentCourseTransactionRepository(IDatabase database) : base(database) { }

        public async Task<IEnumerable<StudentCourseTransactionResponse>> GetTransactionsPerCourseAsync(Guid courseId)
        {
            // Delegate to your existing implementation
            return await GetAllTransactionsByCourse(courseId);
        }

        // ----------------------------
        // Add Transaction
        // ----------------------------
        public async Task<StudentCourseTransactionResponse> AddTransaction(AddStudentCourseTransaction transaction)
        {
            var transactionId = Guid.NewGuid();

            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var dbTx = await conn.BeginTransactionAsync();

            try
            {
                var paymentCode = await GenerateUniquePaymentCodeAsync(conn).ConfigureAwait(false);

                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = dbTx;

                    cmd.CommandText = @"
                        INSERT INTO student_course_transaction
                        (StudentCourseTransactionId, FamilyId, PayableFee, DayCareFee, DayCareDiscount,
                         FeeAmountDiscount, TotalPayable, Comments, Status, PaymentCode, IsActive,
                         TotalAmountPaid, IsCompletelyPaid, CreatedAt, UpdatedOn)
                        VALUES
                        (@TransactionId, @FamilyId, @PayableFee, @DayCareFee, @DayCareDiscount,
                         @FeeAmountDiscount, @TotalPayable, @Comments, @Status, @PaymentCode, @IsActive,
                         @TotalAmountPaid, @IsCompletelyPaid, @CreatedAt, @UpdatedOn)
                    ";

                    cmd.AddParameter("@TransactionId", transactionId.ToByteArray())
                       .AddParameter("@FamilyId", transaction.FamilyId.ToByteArray())
                       .AddParameter("@PayableFee", transaction.PayableFee)
                       .AddParameter("@DayCareFee", transaction.DayCareFee)
                       // schema: DayCareDiscount INT
                       .AddParameter("@DayCareDiscount", (int)transaction.DayCareDiscount)
                       .AddParameter("@FeeAmountDiscount", (int)transaction.FeeAmountDiscount)
                       .AddParameter("@TotalPayable", transaction.TotalPayable)
                       .AddParameter("@Comments", (object?)transaction.Comments ?? DBNull.Value)
                       .AddParameter("@Status", (int)transaction.TransactionStatus)
                       .AddParameter("@PaymentCode", paymentCode)
                       .AddParameter("@IsActive", transaction.IsActive)
                       .AddParameter("@TotalAmountPaid", transaction.TotalAmountPaid)
                       .AddParameter("@IsCompletelyPaid", transaction.IsCompletelyPaid)
                       .AddParameter("@CreatedAt", DateTime.UtcNow)
                       .AddParameter("@UpdatedOn", DateTime.UtcNow);

                    await cmd.ExecuteNonQueryAsync();
                }

                // If you want to link enrollments when creating a transaction:
                if (transaction.StudentCourseEnrollmentIds?.Count > 0)
                {
                    await AddEnrollmentsToTransactionInternal(conn, dbTx, transactionId, transaction.StudentCourseEnrollmentIds);
                }

                await dbTx.CommitAsync();
                return await GetTransactionSimple(transactionId) ?? throw new Exception("Failed to retrieve transaction");
            }
            catch
            {
                await dbTx.RollbackAsync();
                throw;
            }
        }

        // ----------------------------
        // Add Single Enrollment Link
        // ----------------------------
        public async Task<bool> AddEnrollmentsToTransaction(Guid studentCourseTransactionId, Guid studentCourseEnrollmentId)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.AddParameter("@Id", Guid.NewGuid().ToByteArray());  // PK
            cmd.AddParameter("@StudentCourseTransactionId", studentCourseTransactionId.ToByteArray());
            cmd.AddParameter("@StudentCourseEnrollmentId", studentCourseEnrollmentId.ToByteArray());

            cmd.CommandText = @"
                INSERT INTO student_course_transaction_enrollment
                (Id, StudentCourseTransactionId, StudentCourseEnrollmentId)
                VALUES
                (@Id, @StudentCourseTransactionId, @StudentCourseEnrollmentId)
            ";

            var affected = await cmd.ExecuteNonQueryAsync();
            return affected > 0;
        }

        // ----------------------------
        // Get Transaction by PaymentCode (case-insensitive)
        // ----------------------------
        public async Task<StudentCourseTransactionResponse?> GetTransactionByPaymentCode(string paymentCode)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                SELECT
                    sct.StudentCourseTransactionId,
                    sct.FamilyId,
                    sct.PayableFee,
                    sct.DayCareFee,
                    sct.DayCareDiscount,
                    sct.FeeAmountDiscount,
                    sct.TotalPayable,
                    sct.Comments,
                    sct.Status AS TransactionStatus,
                    sct.PaymentCode,
                    sct.IsActive,
                    sct.TotalAmountPaid,
                    sct.IsCompletelyPaid,
                    sct.CreatedAt,
                    sct.UpdatedOn,

                    sce.StudentCourseEnrollmentId,
                    sce.CourseEnrollmentGroupId,
                    sce.CourseId,
                    sce.FamilyId AS EnrollmentFamilyId,
                    sce.ChildId,
                    sce.IsActive AS EnrollmentIsActive,
                    sce.WillUseDayCare,
                    sce.DayCareDays,
                    sce.CreatedAt AS EnrollmentCreatedAt,
                    sce.UpdatedOn AS EnrollmentUpdatedOn,
                    sce.EnrollmentIndex as EnrollmentIndex,
                    ceg.GroupTitle AS GroupTitle,
                    ceg.GroupTitleFr AS GroupTitleFr,
                    ci.FirstName AS ChildFirstName,
                    ci.LastName AS ChildLastName,
                    ci.RegistrationNumber AS ChildRegistrationNumber,
                                        fi.ContactId AS ParentUserId,
                    fi.FirstName AS ParentFirstName,
                    fi.LastName AS ParentLastName,
                    fi.Email AS ParentEmail,
                    fi.Phone AS ParentPhone,
                    fi.Relationship AS ParentRelationship,
                    fi.ContactType AS ParentContactType,
                    crcs.RegistrationFee AS CourseRegistrationFee

                FROM student_course_transaction sct
                JOIN student_course_transaction_enrollment scte
                    ON sct.StudentCourseTransactionId = scte.StudentCourseTransactionId
                JOIN student_course_enrollment sce
                    ON scte.StudentCourseEnrollmentId = sce.StudentCourseEnrollmentId
                JOIN course_enrollment_groups ceg
                    ON sce.CourseEnrollmentGroupId = ceg.CourseEnrollmentGroupId
                JOIN child_information ci
                    ON ci.ChildId = sce.ChildId
                                LEFT JOIN (
                    SELECT
                        ui.UserId AS ContactId,
                        ui.FamilyId,
                        ui.FirstName,
                        ui.LastName,
                        ui.Email,
                        ui.Phone,
                        ui.Relationship,
                        0 AS ContactType
                    FROM user_info ui
                    WHERE ui.IsActive = b'1'

                    UNION ALL

                    SELECT
                        oci.ContactId AS ContactId,
                        oci.FamilyId,
                        oci.FirstName,
                        oci.LastName,
                        NULL AS Email,
                        oci.Phone,
                        oci.Relationship,
                        oci.ContactType
                    FROM other_contacts_information oci
                    WHERE oci.IsActive = 1
                ) fi
                    ON fi.FamilyId = sct.FamilyId
                JOIN courses crcs
                    ON ceg.CourseId = crcs.CourseId

                WHERE LOWER(sct.PaymentCode) = LOWER(@PaymentCode)

                ORDER BY sct.CreatedAt DESC, sce.CreatedAt;
            ";

            cmd.AddParameter("@PaymentCode", paymentCode);

            using var reader = await cmd.ExecuteReaderAsync();
            var list = await MapToTransactionResponse(reader);

            return list.FirstOrDefault();
        }

        // ----------------------------
        // Get Transaction by ID (detailed w/ enrollments)
        // ----------------------------
        public async Task<StudentCourseTransactionResponse?> GetTransaction(Guid transactionId)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                SELECT
                    sct.StudentCourseTransactionId,
                    sct.FamilyId,
                    sct.PayableFee,
                    sct.DayCareFee,
                    sct.DayCareDiscount,
                    sct.FeeAmountDiscount,
                    sct.TotalPayable,
                    sct.Comments,
                    sct.Status AS TransactionStatus,
                    sct.PaymentCode,
                    sct.IsActive,
                    sct.TotalAmountPaid,
                    sct.IsCompletelyPaid,
                    sct.CreatedAt,
                    sct.UpdatedOn,

                    sce.StudentCourseEnrollmentId,
                    sce.CourseEnrollmentGroupId,
                    sce.CourseId,
                    sce.FamilyId AS EnrollmentFamilyId,
                    sce.ChildId,
                    sce.IsActive AS EnrollmentIsActive,
                    sce.WillUseDayCare,
                    sce.DayCareDays,
                    sce.CreatedAt AS EnrollmentCreatedAt,
                    sce.UpdatedOn AS EnrollmentUpdatedOn,
                    sce.EnrollmentIndex as EnrollmentIndex,
                    ceg.GroupTitle AS GroupTitle,
                    ceg.GroupTitleFr AS GroupTitleFr,
                    ci.FirstName AS ChildFirstName,
                    ci.LastName AS ChildLastName,
                    ci.RegistrationNumber AS ChildRegistrationNumber,
                                        fi.ContactId AS ParentUserId,
                    fi.FirstName AS ParentFirstName,
                    fi.LastName AS ParentLastName,
                    fi.Email AS ParentEmail,
                    fi.Phone AS ParentPhone,
                    fi.Relationship AS ParentRelationship,
                    fi.ContactType AS ParentContactType,
                    crcs.RegistrationFee AS CourseRegistrationFee

                FROM student_course_transaction sct
                JOIN student_course_transaction_enrollment scte
                    ON sct.StudentCourseTransactionId = scte.StudentCourseTransactionId
                JOIN student_course_enrollment sce
                    ON scte.StudentCourseEnrollmentId = sce.StudentCourseEnrollmentId
                JOIN course_enrollment_groups ceg
                    ON sce.CourseEnrollmentGroupId = ceg.CourseEnrollmentGroupId
                JOIN child_information ci
                    ON ci.ChildId = sce.ChildId
                                LEFT JOIN (
                    SELECT
                        ui.UserId AS ContactId,
                        ui.FamilyId,
                        ui.FirstName,
                        ui.LastName,
                        ui.Email,
                        ui.Phone,
                        ui.Relationship,
                        0 AS ContactType
                    FROM user_info ui
                    WHERE ui.IsActive = b'1'

                    UNION ALL

                    SELECT
                        oci.ContactId AS ContactId,
                        oci.FamilyId,
                        oci.FirstName,
                        oci.LastName,
                        NULL AS Email,
                        oci.Phone,
                        oci.Relationship,
                        oci.ContactType
                    FROM other_contacts_information oci
                    WHERE oci.IsActive = 1
                ) fi
                    ON fi.FamilyId = sct.FamilyId
                JOIN courses crcs
                    ON ceg.CourseId = crcs.CourseId

                WHERE sct.StudentCourseTransactionId = @TransactionId
                ORDER BY sct.CreatedAt DESC, sce.CreatedAt;
            ";

            cmd.AddParameter("@TransactionId", transactionId.ToByteArray());

            using var reader = await cmd.ExecuteReaderAsync();
            var list = await MapToTransactionResponse(reader);

            return list.FirstOrDefault();
        }

        // ----------------------------
        // Get Transaction Simple (no joins)
        // ----------------------------
        public async Task<StudentCourseTransactionResponse?> GetTransactionSimple(Guid transactionId)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = "SELECT * FROM student_course_transaction WHERE StudentCourseTransactionId = @TransactionId";
            cmd.AddParameter("@TransactionId", transactionId.ToByteArray());

            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return null;

            return await MapToTransactionSimpleResponse(reader);
        }

        private async Task<StudentCourseTransactionResponse> MapToTransactionSimpleResponse(DbDataReader reader)
        {
            return new StudentCourseTransactionResponse
            {
                StudentCourseTransactionId = reader.GetGuidFromByteArray("StudentCourseTransactionId"),
                FamilyId = reader.GetGuidFromByteArray("FamilyId"),
                PayableFee = reader.GetDecimal("PayableFee"),
                DayCareFee = reader.GetDecimal("DayCareFee"),
                DayCareDiscount = Convert.ToDecimal(reader.GetInt32("DayCareDiscount")),
                FeeAmountDiscount = Convert.ToDecimal(reader.GetInt32("FeeAmountDiscount")),
                TotalPayable = reader.GetDecimal("TotalPayable"),
                TotalAmountPaid = reader.GetDecimal("TotalAmountPaid"),
                Comments = reader.IsDBNull("Comments") ? string.Empty : reader.GetString("Comments"),
                PaymentCode = reader.GetString("PaymentCode"),
                TransactionStatus = (TransactionStatus)reader.GetInt32("Status"),
                IsActive = reader.GetBoolean("IsActive"),
                IsCompletelyPaid = reader.GetBoolean("IsCompletelyPaid"),
                CreatedAt = reader.GetDateTime("CreatedAt"),
                UpdatedOn = reader.GetDateTime("UpdatedOn")
            };
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
                    DayCareDiscount = @DayCareDiscount,
                    FeeAmountDiscount = @FeeAmountDiscount,
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
               .AddParameter("@DayCareDiscount", (int)transaction.DayCareDiscount)
               .AddParameter("@FeeAmountDiscount", (int)transaction.FeeAmountDiscount)
               .AddParameter("@TotalPayable", transaction.TotalPayable)
               .AddParameter("@Comments", (object?)transaction.Comments ?? DBNull.Value)
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
        // Get All Transactions (simple list)
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

        // ? FIXED: previously had broken SQL and wrong assumption about schema
        public async Task<StudentCourseTransactionResponse> GetTransactionByFamilyForCurrentSession(Guid familyId, Guid instituteId)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            // �Current session� is ambiguous; safest interpretation:
            // return most recent transaction for this family where enrollments belong to courses under this institute.
            cmd.CommandText = @"
                SELECT
                    sct.StudentCourseTransactionId,
                    sct.FamilyId,
                    sct.PayableFee,
                    sct.DayCareFee,
                    sct.DayCareDiscount,
                    sct.FeeAmountDiscount,
                    sct.TotalPayable,
                    sct.Comments,
                    sct.Status AS TransactionStatus,
                    sct.PaymentCode,
                    sct.IsActive,
                    sct.TotalAmountPaid,
                    sct.IsCompletelyPaid,
                    sct.CreatedAt,
                    sct.UpdatedOn,

                    sce.StudentCourseEnrollmentId,
                    sce.CourseEnrollmentGroupId,
                    sce.CourseId,
                    sce.FamilyId AS EnrollmentFamilyId,
                    sce.ChildId,
                    sce.IsActive AS EnrollmentIsActive,
                    sce.WillUseDayCare,
                    sce.DayCareDays,
                    sce.CreatedAt AS EnrollmentCreatedAt,
                    sce.UpdatedOn AS EnrollmentUpdatedOn,
                    sce.EnrollmentIndex as EnrollmentIndex,
                    ceg.GroupTitle AS GroupTitle,
                    ceg.GroupTitleFr AS GroupTitleFr,
                    ci.FirstName AS ChildFirstName,
                    ci.LastName AS ChildLastName,
                    ci.RegistrationNumber AS ChildRegistrationNumber,
                                        fi.ContactId AS ParentUserId,
                    fi.FirstName AS ParentFirstName,
                    fi.LastName AS ParentLastName,
                    fi.Email AS ParentEmail,
                    fi.Phone AS ParentPhone,
                    fi.Relationship AS ParentRelationship,
                    fi.ContactType AS ParentContactType,
                    crcs.RegistrationFee AS CourseRegistrationFee

                FROM student_course_transaction sct
                JOIN student_course_transaction_enrollment scte
                    ON sct.StudentCourseTransactionId = scte.StudentCourseTransactionId
                JOIN student_course_enrollment sce
                    ON scte.StudentCourseEnrollmentId = sce.StudentCourseEnrollmentId
                JOIN course_enrollment_groups ceg
                    ON sce.CourseEnrollmentGroupId = ceg.CourseEnrollmentGroupId
                JOIN child_information ci
                    ON ci.ChildId = sce.ChildId
                                LEFT JOIN (
                    SELECT
                        ui.UserId AS ContactId,
                        ui.FamilyId,
                        ui.FirstName,
                        ui.LastName,
                        ui.Email,
                        ui.Phone,
                        ui.Relationship,
                        0 AS ContactType
                    FROM user_info ui
                    WHERE ui.IsActive = b'1'

                    UNION ALL

                    SELECT
                        oci.ContactId AS ContactId,
                        oci.FamilyId,
                        oci.FirstName,
                        oci.LastName,
                        NULL AS Email,
                        oci.Phone,
                        oci.Relationship,
                        oci.ContactType
                    FROM other_contacts_information oci
                    WHERE oci.IsActive = 1
                ) fi
                    ON fi.FamilyId = sct.FamilyId
                JOIN courses crcs
                    ON ceg.CourseId = crcs.CourseId
                WHERE
                    sct.FamilyId = @FamilyId
                    AND crcs.InstituteId = @InstituteId
                ORDER BY sct.CreatedAt DESC, sce.CreatedAt
                LIMIT 500;
            ";

            cmd.AddParameter("@FamilyId", familyId.ToByteArray());
            cmd.AddParameter("@InstituteId", instituteId.ToByteArray());

            using var reader = await cmd.ExecuteReaderAsync();
            var list = await MapToTransactionResponse(reader);
            return list.FirstOrDefault();
        }

        // ----------------------------
        // Get Transactions by Family (detailed)
        // ----------------------------
        public async Task<IEnumerable<StudentCourseTransactionResponse>> GetTransactionByFamily(Guid familyId)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                SELECT
                    sct.StudentCourseTransactionId,
                    sct.FamilyId,
                    sct.PayableFee,
                    sct.DayCareFee,
                    sct.DayCareDiscount,
                    sct.FeeAmountDiscount,
                    sct.TotalPayable,
                    sct.Comments,
                    sct.Status AS TransactionStatus,
                    sct.PaymentCode,
                    sct.IsActive,
                    sct.TotalAmountPaid,
                    sct.IsCompletelyPaid,
                    sct.CreatedAt,
                    sct.UpdatedOn,

                    sce.StudentCourseEnrollmentId,
                    sce.CourseEnrollmentGroupId,
                    sce.CourseId,
                    sce.FamilyId AS EnrollmentFamilyId,
                    sce.ChildId,
                    sce.IsActive AS EnrollmentIsActive,
                    sce.WillUseDayCare,
                    sce.DayCareDays,
                    sce.CreatedAt AS EnrollmentCreatedAt,
                    sce.UpdatedOn AS EnrollmentUpdatedOn,
                    sce.EnrollmentIndex as EnrollmentIndex,
                    ceg.GroupTitle AS GroupTitle,
                    ceg.GroupTitleFr AS GroupTitleFr,
                    ci.FirstName AS ChildFirstName,
                    ci.LastName AS ChildLastName,
                    ci.RegistrationNumber AS ChildRegistrationNumber,
                                        fi.ContactId AS ParentUserId,
                    fi.FirstName AS ParentFirstName,
                    fi.LastName AS ParentLastName,
                    fi.Email AS ParentEmail,
                    fi.Phone AS ParentPhone,
                    fi.Relationship AS ParentRelationship,
                    fi.ContactType AS ParentContactType,
                    crcs.RegistrationFee AS CourseRegistrationFee

                FROM student_course_transaction sct
                JOIN student_course_transaction_enrollment scte
                    ON sct.StudentCourseTransactionId = scte.StudentCourseTransactionId
                JOIN student_course_enrollment sce
                    ON scte.StudentCourseEnrollmentId = sce.StudentCourseEnrollmentId
                JOIN course_enrollment_groups ceg
                    ON sce.CourseEnrollmentGroupId = ceg.CourseEnrollmentGroupId
                JOIN child_information ci
                    ON ci.ChildId = sce.ChildId
                                LEFT JOIN (
                    SELECT
                        ui.UserId AS ContactId,
                        ui.FamilyId,
                        ui.FirstName,
                        ui.LastName,
                        ui.Email,
                        ui.Phone,
                        ui.Relationship,
                        0 AS ContactType
                    FROM user_info ui
                    WHERE ui.IsActive = b'1'

                    UNION ALL

                    SELECT
                        oci.ContactId AS ContactId,
                        oci.FamilyId,
                        oci.FirstName,
                        oci.LastName,
                        NULL AS Email,
                        oci.Phone,
                        oci.Relationship,
                        oci.ContactType
                    FROM other_contacts_information oci
                    WHERE oci.IsActive = 1
                ) fi
                    ON fi.FamilyId = sct.FamilyId
                JOIN courses crcs
                    ON ceg.CourseId = crcs.CourseId

                WHERE sct.FamilyId = @FamilyId
                ORDER BY sct.CreatedAt DESC, sce.CreatedAt;
            ";

            cmd.AddParameter("@FamilyId", familyId.ToByteArray());

            using var reader = await cmd.ExecuteReaderAsync();
            return await MapToTransactionResponse(reader);
        }

        public async Task<IEnumerable<StudentCourseTransactionResponse>> GetInstituteTransactionsByFamily(Guid familyId, Guid instituteId)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                SELECT
                    sct.StudentCourseTransactionId,
                    sct.FamilyId,
                    sct.PayableFee,
                    sct.DayCareFee,
                    sct.DayCareDiscount,
                    sct.FeeAmountDiscount,
                    sct.TotalPayable,
                    sct.Comments,
                    sct.Status AS TransactionStatus,
                    sct.PaymentCode,
                    sct.IsActive,
                    sct.TotalAmountPaid,
                    sct.IsCompletelyPaid,
                    sct.CreatedAt,
                    sct.UpdatedOn,

                    sce.StudentCourseEnrollmentId,
                    sce.CourseEnrollmentGroupId,
                    sce.CourseId,
                    sce.FamilyId AS EnrollmentFamilyId,
                    sce.ChildId,
                    sce.IsActive AS EnrollmentIsActive,
                    sce.WillUseDayCare,
                    sce.DayCareDays,
                    sce.CreatedAt AS EnrollmentCreatedAt,
                    sce.UpdatedOn AS EnrollmentUpdatedOn,
                    sce.EnrollmentIndex as EnrollmentIndex,
                    ceg.GroupTitle AS GroupTitle,
                    ceg.GroupTitleFr AS GroupTitleFr,
                    ci.FirstName AS ChildFirstName,
                    ci.LastName AS ChildLastName,
                    ci.RegistrationNumber AS ChildRegistrationNumber,
                                        fi.ContactId AS ParentUserId,
                    fi.FirstName AS ParentFirstName,
                    fi.LastName AS ParentLastName,
                    fi.Email AS ParentEmail,
                    fi.Phone AS ParentPhone,
                    fi.Relationship AS ParentRelationship,
                    fi.ContactType AS ParentContactType,
                    crcs.RegistrationFee AS CourseRegistrationFee

                FROM student_course_transaction sct
                JOIN student_course_transaction_enrollment scte
                    ON sct.StudentCourseTransactionId = scte.StudentCourseTransactionId
                JOIN student_course_enrollment sce
                    ON scte.StudentCourseEnrollmentId = sce.StudentCourseEnrollmentId
                JOIN course_enrollment_groups ceg
                    ON sce.CourseEnrollmentGroupId = ceg.CourseEnrollmentGroupId
                JOIN child_information ci
                    ON ci.ChildId = sce.ChildId
                                LEFT JOIN (
                    SELECT
                        ui.UserId AS ContactId,
                        ui.FamilyId,
                        ui.FirstName,
                        ui.LastName,
                        ui.Email,
                        ui.Phone,
                        ui.Relationship,
                        0 AS ContactType
                    FROM user_info ui
                    WHERE ui.IsActive = b'1'

                    UNION ALL

                    SELECT
                        oci.ContactId AS ContactId,
                        oci.FamilyId,
                        oci.FirstName,
                        oci.LastName,
                        NULL AS Email,
                        oci.Phone,
                        oci.Relationship,
                        oci.ContactType
                    FROM other_contacts_information oci
                    WHERE oci.IsActive = 1
                ) fi
                    ON fi.FamilyId = sct.FamilyId
                JOIN courses crcs
                    ON ceg.CourseId = crcs.CourseId

                WHERE sct.FamilyId = @FamilyId
                  AND crcs.InstituteId = @InstituteId
                ORDER BY sct.CreatedAt DESC, sce.CreatedAt;
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
                    sct.StudentCourseTransactionId,
                    sct.FamilyId,
                    sct.PayableFee,
                    sct.DayCareFee,
                    sct.DayCareDiscount,
                    sct.FeeAmountDiscount,
                    sct.TotalPayable,
                    sct.Comments,
                    sct.Status AS TransactionStatus,
                    sct.PaymentCode,
                    sct.IsActive,
                    sct.TotalAmountPaid,
                    sct.IsCompletelyPaid,
                    sct.CreatedAt,
                    sct.UpdatedOn,

                    sce.StudentCourseEnrollmentId,
                    sce.CourseEnrollmentGroupId,
                    sce.CourseId,
                    sce.FamilyId AS EnrollmentFamilyId,
                    sce.ChildId,
                    sce.IsActive AS EnrollmentIsActive,
                    sce.WillUseDayCare,
                    sce.DayCareDays,
                    sce.CreatedAt AS EnrollmentCreatedAt,
                    sce.UpdatedOn AS EnrollmentUpdatedOn,
                    sce.EnrollmentIndex as EnrollmentIndex,
                    ceg.GroupTitle AS GroupTitle,
                    ceg.GroupTitleFr AS GroupTitleFr,
                    ci.FirstName AS ChildFirstName,
                    ci.LastName AS ChildLastName,
                    ci.RegistrationNumber AS ChildRegistrationNumber,
                                        fi.ContactId AS ParentUserId,
                    fi.FirstName AS ParentFirstName,
                    fi.LastName AS ParentLastName,
                    fi.Email AS ParentEmail,
                    fi.Phone AS ParentPhone,
                    fi.Relationship AS ParentRelationship,
                    fi.ContactType AS ParentContactType,
                    crcs.RegistrationFee AS CourseRegistrationFee

                FROM student_course_transaction sct
                JOIN student_course_transaction_enrollment scte
                    ON sct.StudentCourseTransactionId = scte.StudentCourseTransactionId
                JOIN student_course_enrollment sce
                    ON scte.StudentCourseEnrollmentId = sce.StudentCourseEnrollmentId
                JOIN course_enrollment_groups ceg
                    ON sce.CourseEnrollmentGroupId = ceg.CourseEnrollmentGroupId
                JOIN child_information ci
                    ON ci.ChildId = sce.ChildId
                                LEFT JOIN (
                    SELECT
                        ui.UserId AS ContactId,
                        ui.FamilyId,
                        ui.FirstName,
                        ui.LastName,
                        ui.Email,
                        ui.Phone,
                        ui.Relationship,
                        0 AS ContactType
                    FROM user_info ui
                    WHERE ui.IsActive = b'1'

                    UNION ALL

                    SELECT
                        oci.ContactId AS ContactId,
                        oci.FamilyId,
                        oci.FirstName,
                        oci.LastName,
                        NULL AS Email,
                        oci.Phone,
                        oci.Relationship,
                        oci.ContactType
                    FROM other_contacts_information oci
                    WHERE oci.IsActive = 1
                ) fi
                    ON fi.FamilyId = sct.FamilyId
                JOIN courses crcs
                    ON ceg.CourseId = crcs.CourseId

                WHERE sct.FamilyId = @FamilyId
                  AND sce.CourseId = @CourseId
                ORDER BY sct.CreatedAt DESC, sce.CreatedAt;
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
                    sct.StudentCourseTransactionId,
                    sct.FamilyId,
                    sct.PayableFee,
                    sct.DayCareFee,
                    sct.DayCareDiscount,
                    sct.FeeAmountDiscount,
                    sct.TotalPayable,
                    sct.Comments,
                    sct.Status AS TransactionStatus,
                    sct.PaymentCode,
                    sct.IsActive,
                    sct.TotalAmountPaid,
                    sct.IsCompletelyPaid,
                    sct.CreatedAt,
                    sct.UpdatedOn,

                    sce.StudentCourseEnrollmentId,
                    sce.CourseEnrollmentGroupId,
                    sce.CourseId,
                    sce.FamilyId AS EnrollmentFamilyId,
                    sce.ChildId,
                    sce.IsActive AS EnrollmentIsActive,
                    sce.WillUseDayCare,
                    sce.DayCareDays,
                    sce.CreatedAt AS EnrollmentCreatedAt,
                    sce.UpdatedOn AS EnrollmentUpdatedOn,
                    sce.EnrollmentIndex as EnrollmentIndex,
                    ceg.GroupTitle AS GroupTitle,
                    ceg.GroupTitleFr AS GroupTitleFr,
                    ci.FirstName AS ChildFirstName,
                    ci.LastName AS ChildLastName,
                    ci.RegistrationNumber AS ChildRegistrationNumber,
                                        fi.ContactId AS ParentUserId,
                    fi.FirstName AS ParentFirstName,
                    fi.LastName AS ParentLastName,
                    fi.Email AS ParentEmail,
                    fi.Phone AS ParentPhone,
                    fi.Relationship AS ParentRelationship,
                    fi.ContactType AS ParentContactType,
                    crcs.RegistrationFee AS CourseRegistrationFee

                FROM student_course_transaction sct
                JOIN student_course_transaction_enrollment scte
                    ON sct.StudentCourseTransactionId = scte.StudentCourseTransactionId
                JOIN student_course_enrollment sce
                    ON scte.StudentCourseEnrollmentId = sce.StudentCourseEnrollmentId
                JOIN course_enrollment_groups ceg
                    ON sce.CourseEnrollmentGroupId = ceg.CourseEnrollmentGroupId
                JOIN child_information ci
                    ON ci.ChildId = sce.ChildId
                                LEFT JOIN (
                    SELECT
                        ui.UserId AS ContactId,
                        ui.FamilyId,
                        ui.FirstName,
                        ui.LastName,
                        ui.Email,
                        ui.Phone,
                        ui.Relationship,
                        0 AS ContactType
                    FROM user_info ui
                    WHERE ui.IsActive = b'1'

                    UNION ALL

                    SELECT
                        oci.ContactId AS ContactId,
                        oci.FamilyId,
                        oci.FirstName,
                        oci.LastName,
                        NULL AS Email,
                        oci.Phone,
                        oci.Relationship,
                        oci.ContactType
                    FROM other_contacts_information oci
                    WHERE oci.IsActive = 1
                ) fi
                    ON fi.FamilyId = sct.FamilyId
                JOIN courses crcs
                    ON ceg.CourseId = crcs.CourseId

                WHERE sce.CourseId = @CourseId
                ORDER BY sct.CreatedAt DESC, sce.CreatedAt;
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
                    sct.StudentCourseTransactionId,
                    sct.FamilyId,
                    sct.PayableFee,
                    sct.DayCareFee,
                    sct.DayCareDiscount,
                    sct.FeeAmountDiscount,
                    sct.TotalPayable,
                    sct.Comments,
                    sct.Status AS TransactionStatus,
                    sct.PaymentCode,
                    sct.IsActive,
                    sct.TotalAmountPaid,
                    sct.IsCompletelyPaid,
                    sct.CreatedAt,
                    sct.UpdatedOn,

                    sce.StudentCourseEnrollmentId,
                    sce.CourseEnrollmentGroupId,
                    sce.CourseId,
                    sce.FamilyId AS EnrollmentFamilyId,
                    sce.ChildId,
                    sce.IsActive AS EnrollmentIsActive,
                    sce.WillUseDayCare,
                    sce.DayCareDays,
                    sce.CreatedAt AS EnrollmentCreatedAt,
                    sce.UpdatedOn AS EnrollmentUpdatedOn,
                    sce.EnrollmentIndex as EnrollmentIndex,
                    ceg.GroupTitle AS GroupTitle,
                    ceg.GroupTitleFr AS GroupTitleFr,
                    ci.FirstName AS ChildFirstName,
                    ci.LastName AS ChildLastName,
                    ci.RegistrationNumber AS ChildRegistrationNumber,
                                        fi.ContactId AS ParentUserId,
                    fi.FirstName AS ParentFirstName,
                    fi.LastName AS ParentLastName,
                    fi.Email AS ParentEmail,
                    fi.Phone AS ParentPhone,
                    fi.Relationship AS ParentRelationship,
                    fi.ContactType AS ParentContactType,
                    crcs.RegistrationFee AS CourseRegistrationFee

                FROM student_course_transaction sct
                JOIN student_course_transaction_enrollment scte
                    ON sct.StudentCourseTransactionId = scte.StudentCourseTransactionId
                JOIN student_course_enrollment sce
                    ON scte.StudentCourseEnrollmentId = sce.StudentCourseEnrollmentId
                JOIN course_enrollment_groups ceg
                    ON sce.CourseEnrollmentGroupId = ceg.CourseEnrollmentGroupId
                JOIN child_information ci
                    ON ci.ChildId = sce.ChildId
                                LEFT JOIN (
                    SELECT
                        ui.UserId AS ContactId,
                        ui.FamilyId,
                        ui.FirstName,
                        ui.LastName,
                        ui.Email,
                        ui.Phone,
                        ui.Relationship,
                        0 AS ContactType
                    FROM user_info ui
                    WHERE ui.IsActive = b'1'

                    UNION ALL

                    SELECT
                        oci.ContactId AS ContactId,
                        oci.FamilyId,
                        oci.FirstName,
                        oci.LastName,
                        NULL AS Email,
                        oci.Phone,
                        oci.Relationship,
                        oci.ContactType
                    FROM other_contacts_information oci
                    WHERE oci.IsActive = 1
                ) fi
                    ON fi.FamilyId = sct.FamilyId
                JOIN courses crcs
                    ON ceg.CourseId = crcs.CourseId

                WHERE crcs.InstituteId = @InstituteId
                ORDER BY sct.CreatedAt DESC, sce.CreatedAt;
            ";

            cmd.AddParameter("@InstituteId", instituteId.ToByteArray());

            using var reader = await cmd.ExecuteReaderAsync();
            return await MapToTransactionResponse(reader);
        }

        // ----------------------------
        // Enrollments (bulk)
        // ----------------------------
        public async Task<bool> AddEnrollmentsToTransaction(Guid transactionId, IEnumerable<Guid> enrollmentIds)
        {
            if (enrollmentIds == null) return false;

            var ids = enrollmentIds
                .Where(x => x != Guid.Empty)
                .Distinct()
                .ToList();

            if (ids.Count == 0) return false;

            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var dbTx = await conn.BeginTransactionAsync();

            try
            {
                await AddEnrollmentsToTransactionInternal(conn, dbTx, transactionId, ids);
                await dbTx.CommitAsync();
                return true;
            }
            catch
            {
                await dbTx.RollbackAsync();
                throw;
            }
        }

        private async Task AddEnrollmentsToTransactionInternal(DbConnection conn, DbTransaction dbTx, Guid transactionId, IEnumerable<Guid> enrollmentIds)
        {
            // ?? Extra Safety: verify all enrollment IDs exist to avoid FK error mid-insert
            // (If you prefer "fail fast", keep it; if you want silent skip, change behavior.)
            var ids = enrollmentIds.ToList();
            if (ids.Count == 0) return;

            // chunk to avoid huge SQL
            const int chunkSize = 200;

            for (int offset = 0; offset < ids.Count; offset += chunkSize)
            {
                var chunk = ids.Skip(offset).Take(chunkSize).ToList();

                // Exists-check
                if (!await EnrollmentIdsExistAsync(conn, dbTx, chunk))
                    throw new Exception("One or more StudentCourseEnrollmentIds do not exist. Aborting to prevent FK failure.");

                using var cmd = conn.CreateCommand();
                cmd.Transaction = dbTx;

                var values = new List<string>(chunk.Count);

                for (int i = 0; i < chunk.Count; i++)
                {
                    var pId = $"@Id{i}";
                    var pTx = $"@Tx{i}";
                    var pEn = $"@En{i}";

                    values.Add($"({pId}, {pTx}, {pEn})");

                    cmd.AddParameter(pId, Guid.NewGuid().ToByteArray());
                    cmd.AddParameter(pTx, transactionId.ToByteArray());
                    cmd.AddParameter(pEn, chunk[i].ToByteArray());
                }

                cmd.CommandText = $@"
                    INSERT INTO student_course_transaction_enrollment
                        (Id, StudentCourseTransactionId, StudentCourseEnrollmentId)
                    VALUES {string.Join(", ", values)};
                ";

                await cmd.ExecuteNonQueryAsync();
            }
        }

        private async Task<bool> EnrollmentIdsExistAsync(DbConnection conn, DbTransaction dbTx, List<Guid> enrollmentIds)
        {
            // Build IN (@p0, @p1, ...) with binary(16)
            using var cmd = conn.CreateCommand();
            cmd.Transaction = dbTx;

            var inParams = new List<string>(enrollmentIds.Count);

            for (int i = 0; i < enrollmentIds.Count; i++)
            {
                var p = $"@e{i}";
                inParams.Add(p);
                cmd.AddParameter(p, enrollmentIds[i].ToByteArray());
            }

            cmd.CommandText = $@"
                SELECT COUNT(*) 
                FROM student_course_enrollment
                WHERE StudentCourseEnrollmentId IN ({string.Join(", ", inParams)});
            ";

            var countObj = await cmd.ExecuteScalarAsync();
            var count = Convert.ToInt32(countObj);

            return count == enrollmentIds.Count;
        }

        // ? FIXED: correct table relationship (must go through student_course_transaction_enrollment)
        public async Task<IEnumerable<StudentCourseEnrollmentResponse>> GetEnrollmentsForTransaction(Guid transactionId)
        {
            var results = new List<StudentCourseEnrollmentResponse>();
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                SELECT sce.*
                FROM student_course_transaction_enrollment scte
                JOIN student_course_enrollment sce
                    ON scte.StudentCourseEnrollmentId = sce.StudentCourseEnrollmentId
                WHERE scte.StudentCourseTransactionId = @TransactionId
                ORDER BY sce.CreatedAt;
            ";

            cmd.AddParameter("@TransactionId", transactionId.ToByteArray());

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(new StudentCourseEnrollmentResponse
                {
                    StudentCourseEnrollmentId = reader.GetGuidFromByteArray("StudentCourseEnrollmentId"),
                    CourseEnrollmentGroupId = reader.GetGuidFromByteArray("CourseEnrollmentGroupId"),
                    CourseId = reader.GetGuidFromByteArray("CourseId"),
                    FamilyId = reader.GetGuidFromByteArray("FamilyId"),
                    ChildId = reader.GetGuidFromByteArray("ChildId"),
                    IsActive = reader.GetBoolean("IsActive"),
                    WillUseDayCare = reader.GetBoolean("WillUseDayCare"),
                    DayCareDays = reader.GetInt32("DayCareDays"),
                    CreatedAt = reader.GetDateTime("CreatedAt"),
                    UpdatedOn = reader.GetDateTime("UpdatedOn"),
                    EnrollmentIndex = reader.GetInt32("EnrollmentIndex")
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
                INNER JOIN student_course_transaction t 
                    ON p.StudentCourseTransactionId = t.StudentCourseTransactionId
                WHERE p.FamilyId = @FamilyId
                  AND p.IsActive = 1
                ORDER BY p.CreatedAt DESC;
            ";

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
        // Pending Amounts (fixed for your real schema)
        // ----------------------------
        public async Task<IEnumerable<PendingAmountResponse>> GetPendingAmountsReportAsync(
            Guid? instituteId = null, Guid? courseId = null, Guid? courseGroupId = null, Guid? familyId = null, string? paymentCode = null)
        {
            var results = new List<PendingAmountResponse>();
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            var filters = new List<string>();

            // real base table alias: sct
            if (familyId.HasValue)
            {
                filters.Add("sct.FamilyId = @FamilyId");
                cmd.AddParameter("@FamilyId", familyId.Value.ToByteArray());
            }

            // ? FIX: only add paymentCode filter when it is NOT null/empty
            if (!string.IsNullOrWhiteSpace(paymentCode))
            {
                filters.Add("LOWER(sct.PaymentCode) = LOWER(@PaymentCode)");
                cmd.AddParameter("@PaymentCode", paymentCode);
            }

            if (instituteId.HasValue)
            {
                filters.Add("crs.InstituteId = @InstituteId");
                cmd.AddParameter("@InstituteId", instituteId.Value.ToByteArray());
            }

            if (courseId.HasValue)
            {
                filters.Add("crs.CourseId = @CourseId");
                cmd.AddParameter("@CourseId", courseId.Value.ToByteArray());
            }

            if (courseGroupId.HasValue)
            {
                filters.Add("ceg.CourseEnrollmentGroupId = @CourseGroupId");
                cmd.AddParameter("@CourseGroupId", courseGroupId.Value.ToByteArray());
            }

            var whereClause = filters.Count > 0 ? "WHERE " + string.Join(" AND ", filters) : string.Empty;

            cmd.CommandText = $@"
                SELECT
                    crs.InstituteId AS InstituteId,
                    ins.Name AS InstituteName,
                    crs.CourseId AS CourseId,
                    crs.Name AS CourseName,
                    ceg.CourseEnrollmentGroupId AS CourseEnrollmentGroupId,
                    ceg.GroupTitle AS CourseGroupName,
                    sct.FamilyId AS FamilyId,
                    sct.PaymentCode AS PaymentCode,
                    SUM(sct.TotalPayable - sct.TotalAmountPaid) AS PendingAmount
                FROM student_course_transaction sct
                JOIN student_course_transaction_enrollment scte
                    ON sct.StudentCourseTransactionId = scte.StudentCourseTransactionId
                JOIN student_course_enrollment sce
                    ON scte.StudentCourseEnrollmentId = sce.StudentCourseEnrollmentId
                JOIN course_enrollment_groups ceg
                    ON sce.CourseEnrollmentGroupId = ceg.CourseEnrollmentGroupId
                JOIN courses crs
                    ON ceg.CourseId = crs.CourseId
                JOIN institutes ins
                    ON crs.InstituteId = ins.InstituteId
                {whereClause}
                GROUP BY
                    crs.InstituteId, ins.Name,
                    crs.CourseId, crs.Name,
                    ceg.CourseEnrollmentGroupId, ceg.GroupTitle,
                    sct.FamilyId, sct.PaymentCode
                ORDER BY PendingAmount DESC;
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
        private static async Task<IEnumerable<StudentCourseTransactionResponse>> MapToTransactionResponse(DbDataReader reader)
        {
            if (!reader.HasRows)
                return Enumerable.Empty<StudentCourseTransactionResponse>();

            var ordTxId = reader.GetOrdinal("StudentCourseTransactionId");
            var ordTxFamilyId = reader.GetOrdinal("FamilyId");
            var ordPayableFee = reader.GetOrdinal("PayableFee");
            var ordDayCareFee = reader.GetOrdinal("DayCareFee");
            var ordDayCareDiscount = reader.GetOrdinal("DayCareDiscount");
            var ordFeeAmountDiscount = reader.GetOrdinal("FeeAmountDiscount");
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
            var ordEnrollmentIndex = reader.GetOrdinal("EnrollmentIndex");
            var ordGroupTitle = reader.GetOrdinal("GroupTitle");
            var ordGroupTitleFr = reader.GetOrdinal("GroupTitleFr");
            var ordChildFirstName = reader.GetOrdinal("ChildFirstName");
            var ordChildLastName = reader.GetOrdinal("ChildLastName");
            var ordChildRegistrationNumber = reader.GetOrdinal("ChildRegistrationNumber");
            var ordParentUserId = reader.GetOrdinal("ParentUserId");
            var ordParentFirstName = reader.GetOrdinal("ParentFirstName");
            var ordParentLastName = reader.GetOrdinal("ParentLastName");
            var ordParentEmail = reader.GetOrdinal("ParentEmail");
            var ordParentPhone = reader.GetOrdinal("ParentPhone");
            var ordParentRelationship = reader.GetOrdinal("ParentRelationship");
            var ordParentContactType = reader.GetOrdinal("ParentContactType");
            var ordCourseRegistrationFee = reader.GetOrdinal("CourseRegistrationFee");

            var lookup = new Dictionary<Guid, StudentCourseTransactionResponse>();
            var enrollmentIdsByTx = new Dictionary<Guid, HashSet<Guid>>();
            var familyInfoIdsByTx = new Dictionary<Guid, HashSet<string>>();

            while (await reader.ReadAsync())
            {
                var txId = reader.GetGuid(ordTxId);
                var enrollmentId = reader.GetGuid(ordEnrollmentId);
                var childName = $"{reader.GetString(ordChildFirstName)} {reader.GetString(ordChildLastName)}".Trim();

                if (!lookup.TryGetValue(txId, out var tx))
                {
                    tx = new StudentCourseTransactionResponse
                    {
                        StudentCourseTransactionId = txId,
                        FamilyId = reader.GetGuid(ordTxFamilyId),
                        PayableFee = reader.GetDecimal(ordPayableFee),
                        DayCareFee = reader.GetDecimal(ordDayCareFee),
                        DayCareDiscount = Convert.ToDecimal(reader.GetInt32(ordDayCareDiscount)),
                        FeeAmountDiscount = Convert.ToDecimal(reader.GetInt32(ordFeeAmountDiscount)),
                        TotalPayable = reader.GetDecimal(ordTotalPayable),
                        Comments = reader.IsDBNull(ordComments) ? string.Empty : reader.GetString(ordComments),
                        TransactionStatus = (TransactionStatus)reader.GetInt32(ordStatus),
                        PaymentCode = reader.GetString(ordPaymentCode),
                        IsActive = reader.GetBoolean(ordTxIsActive),
                        TotalAmountPaid = reader.GetDecimal(ordTotalAmountPaid),
                        IsCompletelyPaid = reader.GetBoolean(ordIsCompletelyPaid),
                        CreatedAt = reader.GetDateTime(ordTxCreatedAt),
                        UpdatedOn = reader.GetDateTime(ordTxUpdatedOn),
                        RegistrationFee = reader.GetInt32(ordCourseRegistrationFee),
                        Enrollments = new List<StudentCourseEnrollmentResponse>(),
                        FamilyInformation = new List<FamilyInformationResponse>()
                    };

                    tx.StudentCourseEnrollmentId = enrollmentId;
                    lookup.Add(txId, tx);
                    enrollmentIdsByTx[txId] = new HashSet<Guid>();
                    familyInfoIdsByTx[txId] = new HashSet<string>();
                }

                if (!enrollmentIdsByTx.TryGetValue(txId, out var enrollmentIds))
                {
                    enrollmentIds = new HashSet<Guid>();
                    enrollmentIdsByTx[txId] = enrollmentIds;
                }

                if (enrollmentIds.Add(enrollmentId))
                {
                    var enrollmentResponse = new StudentCourseEnrollmentResponse
                    {
                        StudentCourseEnrollmentId = enrollmentId,
                        CourseEnrollmentGroupId = reader.GetGuid(ordGroupId),
                        CourseId = reader.GetGuid(ordCourseId),
                        FamilyId = reader.GetGuid(ordEnrollmentFamilyId),
                        ChildId = reader.GetGuid(ordChildId),
                        ChildName = childName,
                        IsActive = reader.GetBoolean(ordEnrollmentIsActive),
                        WillUseDayCare = reader.GetBoolean(ordWillUseDayCare),
                        DayCareDays = reader.GetInt32(ordDayCareDays),
                        CreatedAt = reader.GetDateTime(ordEnrollmentCreatedAt),
                        UpdatedOn = reader.GetDateTime(ordEnrollmentUpdatedOn),
                        EnrollmentIndex = reader.GetInt32(ordEnrollmentIndex),
                        GroupTitle = reader.IsDBNull(ordGroupTitle) ? string.Empty : reader.GetString(ordGroupTitle),
                        GroupTitleFr = reader.IsDBNull(ordGroupTitleFr) ? string.Empty : reader.GetString(ordGroupTitleFr)
                    };

                    var childRegistrationNumberProperty = typeof(StudentCourseEnrollmentResponse).GetProperty("RegistrationNumber");
                    if (childRegistrationNumberProperty?.CanWrite == true)
                    {
                        var childRegistrationNumber = reader.IsDBNull(ordChildRegistrationNumber)
                            ? string.Empty
                            : reader.GetString(ordChildRegistrationNumber);
                        childRegistrationNumberProperty.SetValue(enrollmentResponse, childRegistrationNumber);
                    }

                    tx.Enrollments.Add(enrollmentResponse);
                }

                if (!reader.IsDBNull(ordParentUserId))
                {
                    var parentUserId = reader.GetGuid(ordParentUserId);
                    // UNION includes user_info (Email NOT NULL) and other_contacts_information (Email NULL).
                    // Key by source+id so both contacts are appended even if IDs collide.
                    var contactSource = reader.IsDBNull(ordParentEmail) ? "other_contact" : "user_info";
                    var familyInfoKey = $"{contactSource}:{parentUserId:N}";
                    if (!familyInfoIdsByTx.TryGetValue(txId, out var familyInfoIds))
                    {
                        familyInfoIds = new HashSet<string>();
                        familyInfoIdsByTx[txId] = familyInfoIds;
                    }

                    if (familyInfoIds.Add(familyInfoKey))
                    {
                        var relationship = (Relationship)reader.GetInt32(ordParentRelationship);
                        var contactType = (ContactType)reader.GetInt32(ordParentContactType);
                        tx.FamilyInformation.Add(new FamilyInformationResponse
                        {
                            Name = $"{reader.GetString(ordParentFirstName)} {reader.GetString(ordParentLastName)}".Trim(),
                            Email = reader.IsDBNull(ordParentEmail) ? string.Empty : reader.GetString(ordParentEmail),
                            Phone = reader.IsDBNull(ordParentPhone) ? string.Empty : reader.GetString(ordParentPhone),
                            RelationshipType = relationship,
                            ContactType = contactType
                        });
                    }
                }
            }

            return lookup.Values.ToList();
        }

        private async Task<StudentCourseTransactionResponse> MapToTransactionSingleResponse(DbDataReader reader)
        {
            return new StudentCourseTransactionResponse
            {
                StudentCourseTransactionId = reader.GetGuidFromByteArray("StudentCourseTransactionId"),
                FamilyId = reader.GetGuidFromByteArray("FamilyId"),
                PayableFee = reader.GetDecimal("PayableFee"),
                DayCareFee = reader.GetDecimal("DayCareFee"),
                DayCareDiscount = Convert.ToDecimal(reader.GetInt32("DayCareDiscount")),
                FeeAmountDiscount = Convert.ToDecimal(reader.GetInt32("FeeAmountDiscount")),
                TotalPayable = reader.GetDecimal("TotalPayable"),
                TotalAmountPaid = reader.GetDecimal("TotalAmountPaid"),
                Comments = reader.IsDBNull("Comments") ? string.Empty : reader.GetString("Comments"),
                PaymentCode = reader.GetString("PaymentCode"),
                TransactionStatus = (TransactionStatus)reader.GetInt32("Status"),
                IsActive = reader.GetBoolean("IsActive"),
                IsCompletelyPaid = reader.GetBoolean("IsCompletelyPaid"),
                CreatedAt = reader.GetDateTime("CreatedAt"),
                UpdatedOn = reader.GetDateTime("UpdatedOn")
            };
        }

        private async Task<string> GenerateUniquePaymentCodeAsync(DbConnection conn)
        {
            const int maxAttempts = 20;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                var code = Generate6CharCode();

                using var checkCmd = conn.CreateCommand();
                checkCmd.CommandText = @"
                    SELECT 1
                    FROM student_course_transaction
                    WHERE PaymentCode = @Code
                    LIMIT 1;
                ";
                checkCmd.AddParameter("@Code", code);

                var exists = await checkCmd.ExecuteScalarAsync();

                if (exists == null || exists == DBNull.Value)
                    return code;
            }

            throw new Exception("Failed to generate a unique payment code after several attempts.");
        }

        private static readonly char[] PaymentCodeChars =
            "ABCDEFGHJKLMNPQRSTUVWXYZ23456789".ToCharArray(); // excludes I,O,1,0

        private static string Generate6CharCode()
        {
            Span<char> buffer = stackalloc char[6];
            Span<byte> bytes = stackalloc byte[6];

            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(bytes);

            for (int i = 0; i < buffer.Length; i++)
                buffer[i] = PaymentCodeChars[bytes[i] % PaymentCodeChars.Length];

            return new string(buffer);
        }

        public async Task<bool> DeleteStudentCourseTransactionEnrollmentByEnrollmentId(Guid studentCourseEnrollmentId)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = "DELETE FROM `student_course_transaction_enrollment` WHERE StudentCourseEnrollmentId = @StudentCourseEnrollmentId";
            cmd.AddParameter("@StudentCourseEnrollmentId", studentCourseEnrollmentId.ToByteArray());
            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        public async Task<bool> DeleteStudentCourseTransactionEnrollmentByTransactionId(Guid transactionId)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = "DELETE FROM `student_course_transaction_enrollment` WHERE StudentCourseTransactionId = @StudentCourseTransactionId";
            cmd.AddParameter("@StudentCourseTransactionId", transactionId.ToByteArray());
            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        public async Task<bool> DeleteStudentCourseTransactionEnrollmentById(Guid id)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = "DELETE FROM `student_course_transaction_enrollment` WHERE Id = @Id";
            cmd.AddParameter("@Id", id.ToByteArray());
            return await cmd.ExecuteNonQueryAsync() > 0;
        }
    }
}



