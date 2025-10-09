using Cumulus.Data;
using Data;
using MaktabDataContracts.Responses.Transactions;
using System.Data;
using System.Data.Common;

namespace Courses.Repository.Implementation
{
    public class StudentCourseTransactionReportRepository : DbRepository, IStudentCourseTransactionReportRepository
    {
        public StudentCourseTransactionReportRepository(IDatabase database) : base(database) { }

        // ----------------------------
        // Transactions by Family
        // ----------------------------
        public async Task<IEnumerable<TransactionReportSummary>> GetTransactionsByFamily(Guid? familyId = null)
        {
            var results = new List<TransactionReportSummary>();
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                SELECT TransactionId, FamilyId, FamilyName, TotalPayable, TotalPaid, CreatedAt, UpdatedOn
                FROM student_course_transaction
                WHERE (@FamilyId IS NULL OR FamilyId=@FamilyId)";

            cmd.AddParameter("@FamilyId", familyId?.ToByteArray());

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                results.Add(MapToTransactionReportSummary(reader));

            return results;
        }

        // ----------------------------
        // Transactions by Course
        // ----------------------------
        public async Task<IEnumerable<CourseReportSummary>> GetTransactionsByCourse(Guid? instituteId = null, Guid? courseId = null)
        {
            var results = new List<CourseReportSummary>();
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                SELECT CourseId, CourseName, InstituteId, InstituteName,
                       COUNT(*) AS TotalEnrollments,
                       SUM(TotalPayable) AS TotalPayable,
                       SUM(TotalPaid) AS TotalPaid
                FROM student_course_transaction
                WHERE (@InstituteId IS NULL OR InstituteId=@InstituteId)
                  AND (@CourseId IS NULL OR CourseId=@CourseId)
                GROUP BY CourseId, CourseName, InstituteId, InstituteName";

            cmd.AddParameter("@InstituteId", instituteId?.ToByteArray());
            cmd.AddParameter("@CourseId", courseId?.ToByteArray());

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                results.Add(MapToCourseReportSummary(reader));

            return results;
        }

        // ----------------------------
        // Transactions by Course Group
        // ----------------------------
        public async Task<IEnumerable<EnrollmentGroupReportSummary>> GetTransactionsByCourseGroup(Guid? courseId = null, Guid? groupId = null)
        {
            var results = new List<EnrollmentGroupReportSummary>();
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                SELECT CourseEnrollmentGroupId, GroupTitle,
                       COUNT(*) AS TotalEnrollments,
                       SUM(TotalPayable) AS TotalPayable,
                       SUM(TotalPaid) AS TotalPaid
                FROM student_course_transaction
                WHERE (@CourseId IS NULL OR CourseId=@CourseId)
                  AND (@GroupId IS NULL OR CourseEnrollmentGroupId=@GroupId)
                GROUP BY CourseEnrollmentGroupId, GroupTitle";

            cmd.AddParameter("@CourseId", courseId?.ToByteArray());
            cmd.AddParameter("@GroupId", groupId?.ToByteArray());

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                results.Add(MapToEnrollmentGroupReportSummary(reader));

            return results;
        }

        // ----------------------------
        // Pending Amount by Family
        // ----------------------------
        public async Task<IEnumerable<TransactionPendingFamilySummary>> GetPendingAmountByFamily(Guid? instituteId = null)
        {
            var results = new List<TransactionPendingFamilySummary>();
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                SELECT FamilyId, FamilyName, SUM(TotalPayable - TotalPaid) AS PendingAmount
                FROM student_course_transaction
                WHERE (@InstituteId IS NULL OR InstituteId=@InstituteId)
                GROUP BY FamilyId, FamilyName";

            cmd.AddParameter("@InstituteId", instituteId?.ToByteArray());

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(new TransactionPendingFamilySummary
                {
                    FamilyId = reader.GetGuidFromByteArray("FamilyId"),
                    FamilyName = reader.GetStringOrDefault("FamilyName"),
                    PendingAmount = reader.GetDecimal("PendingAmount")
                });
            }

            return results;
        }

        // ----------------------------
        // Pending Amount by Institute
        // ----------------------------
        public async Task<IEnumerable<TransactionPendingInstituteSummary>> GetPendingAmountByInstitute()
        {
            var results = new List<TransactionPendingInstituteSummary>();
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                SELECT InstituteId, InstituteName, SUM(TotalPayable - TotalPaid) AS PendingAmount
                FROM student_course_transaction
                GROUP BY InstituteId, InstituteName";

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(new TransactionPendingInstituteSummary
                {
                    InstituteId = reader.GetGuidFromByteArray("InstituteId"),
                    InstituteName = reader.GetStringOrDefault("InstituteName"),
                    PendingAmount = reader.GetDecimal("PendingAmount")
                });
            }

            return results;
        }

        // ----------------------------
        // Pending Amount by Course Group
        // ----------------------------
        public async Task<IEnumerable<TransactionPendingCourseGroupSummary>> GetPendingAmountByCourseGroup(Guid? courseId = null)
        {
            var results = new List<TransactionPendingCourseGroupSummary>();
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                SELECT CourseEnrollmentGroupId, GroupTitle,
                       SUM(TotalPayable - TotalPaid) AS PendingAmount
                FROM student_course_transaction
                WHERE (@CourseId IS NULL OR CourseId=@CourseId)
                GROUP BY CourseEnrollmentGroupId, GroupTitle";

            cmd.AddParameter("@CourseId", courseId?.ToByteArray());

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(new TransactionPendingCourseGroupSummary
                {
                    CourseGroupId = reader.GetGuidFromByteArray("CourseEnrollmentGroupId"),
                    CourseGroupName = reader.GetStringOrDefault("GroupTitle"),
                    PendingAmount = reader.GetDecimal("PendingAmount")
                });
            }

            return results;
        }

        // ----------------------------
        // Total Collected by Institute
        // ----------------------------
        public async Task<IEnumerable<TransactionCollectedInstituteSummary>> GetTotalCollectedByInstitute(Guid? instituteId = null)
        {
            var results = new List<TransactionCollectedInstituteSummary>();
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                SELECT InstituteId, InstituteName, SUM(TotalPaid) AS CollectedAmount
                FROM student_course_transaction
                WHERE (@InstituteId IS NULL OR InstituteId=@InstituteId)
                GROUP BY InstituteId, InstituteName";

            cmd.AddParameter("@InstituteId", instituteId?.ToByteArray());

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(new TransactionCollectedInstituteSummary
                {
                    InstituteId = reader.GetGuidFromByteArray("InstituteId"),
                    InstituteName = reader.GetStringOrDefault("InstituteName"),
                    CollectedAmount = reader.GetDecimal("CollectedAmount")
                });
            }

            return results;
        }

        // ----------------------------
        // Total Pending / Collected scalar
        // ----------------------------
        public async Task<decimal> GetTotalPendingAmount(Guid? instituteId = null)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                SELECT SUM(TotalPayable - TotalPaid)
                FROM student_course_transaction
                WHERE (@InstituteId IS NULL OR InstituteId=@InstituteId)";

            cmd.AddParameter("@InstituteId", instituteId?.ToByteArray());

            var result = await cmd.ExecuteScalarAsync();
            return result != DBNull.Value ? Convert.ToDecimal(result) : 0m;
        }

        public async Task<decimal> GetTotalCollectedAmount(Guid? instituteId = null)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                SELECT SUM(TotalPaid)
                FROM student_course_transaction
                WHERE (@InstituteId IS NULL OR InstituteId=@InstituteId)";

            cmd.AddParameter("@InstituteId", instituteId?.ToByteArray());

            var result = await cmd.ExecuteScalarAsync();
            return result != DBNull.Value ? Convert.ToDecimal(result) : 0m;
        }

        // ----------------------------
        // Pending for a single family
        // ----------------------------
        public async Task<IEnumerable<TransactionPendingFamilySummary>> GetPendingAmountForFamily(Guid familyId)
        {
            var results = new List<TransactionPendingFamilySummary>();
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                SELECT FamilyId, FamilyName, SUM(TotalPayable - TotalPaid) AS PendingAmount
                FROM student_course_transaction
                WHERE FamilyId=@FamilyId
                GROUP BY FamilyId, FamilyName";

            cmd.AddParameter("@FamilyId", familyId.ToByteArray());

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(new TransactionPendingFamilySummary
                {
                    FamilyId = reader.GetGuidFromByteArray("FamilyId"),
                    FamilyName = reader.GetStringOrDefault("FamilyName"),
                    PendingAmount = reader.GetDecimal("PendingAmount")
                });
            }

            return results;
        }

        // ----------------------------
        // Pending Transactions placeholder
        // ----------------------------
        public async Task<IEnumerable<TransactionPendingCourseSummary>> GetPendingAmountByCourse(Guid? instituteId = null)
        {
            var results = new List<TransactionPendingCourseSummary>();
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
        SELECT CourseId, CourseName, SUM(TotalPayable - TotalPaid) AS PendingAmount
        FROM student_course_transaction
        WHERE (@InstituteId IS NULL OR InstituteId=@InstituteId)
        GROUP BY CourseId, CourseName";

            cmd.AddParameter("@InstituteId", instituteId?.ToByteArray());

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(new TransactionPendingCourseSummary
                {
                    CourseId = reader.GetGuidFromByteArray("CourseId"),
                    CourseName = reader.GetStringOrDefault("CourseName"),
                    PendingAmount = reader.GetDecimal("PendingAmount")
                });
            }

            return results;
        }

        // ----------------------------
        // Helper mappings
        // ----------------------------
        private TransactionReportSummary MapToTransactionReportSummary(DbDataReader reader) => new()
        {
            TransactionId = reader.GetGuidFromByteArray("TransactionId"),
            FamilyId = reader.GetGuidFromByteArray("FamilyId"),
            FamilyName = reader.GetStringOrDefault("FamilyName"),
            TotalPayable = reader.GetDecimal("TotalPayable"),
            TotalPaid = reader.GetDecimal("TotalPaid"),
            CreatedAt = reader.GetDateTimeUtc("CreatedAt"),
            UpdatedOn = reader.GetDateTimeUtc("UpdatedOn")
        };

        private CourseReportSummary MapToCourseReportSummary(DbDataReader reader) => new()
        {
            CourseId = reader.GetGuidFromByteArray("CourseId"),
            CourseName = reader.GetStringOrDefault("CourseName"),
            InstituteId = reader.GetGuidFromByteArray("InstituteId"),
            InstituteName = reader.GetStringOrDefault("InstituteName"),
            TotalEnrollments = reader.GetIntOrDefault("TotalEnrollments"),
            TotalPayable = reader.GetDecimal("TotalPayable"),
            TotalPaid = reader.GetDecimal("TotalPaid")
        };

        private EnrollmentGroupReportSummary MapToEnrollmentGroupReportSummary(DbDataReader reader) => new()
        {
            CourseEnrollmentGroupId = reader.GetGuidFromByteArray("CourseEnrollmentGroupId"),
            GroupTitle = reader.GetStringOrDefault("GroupTitle"),
            TotalEnrollments = reader.GetIntOrDefault("TotalEnrollments"),
            TotalPayable = reader.GetDecimal("TotalPayable"),
            TotalPaid = reader.GetDecimal("TotalPaid")
        };

        public async Task<IEnumerable<TransactionReportSummary>> GetPendingTransactions(
            Guid? instituteId = null,
            Guid? courseId = null,
            Guid? familyId = null)
        {
            var results = new List<TransactionReportSummary>();

            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            // Only pending transactions (TotalPayable - TotalPaid > 0)
            cmd.CommandText = @"
        SELECT TransactionId, FamilyId, FamilyName, TotalPayable, TotalPaid, CreatedAt, UpdatedOn
        FROM student_course_transaction
        WHERE (TotalPayable - TotalPaid) > 0
          AND (@InstituteId IS NULL OR InstituteId=@InstituteId)
          AND (@CourseId IS NULL OR CourseId=@CourseId)
          AND (@FamilyId IS NULL OR FamilyId=@FamilyId)";

            cmd.AddParameter("@InstituteId", instituteId?.ToByteArray());
            cmd.AddParameter("@CourseId", courseId?.ToByteArray());
            cmd.AddParameter("@FamilyId", familyId?.ToByteArray());

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(new TransactionReportSummary
                {
                    TransactionId = reader.GetGuidFromByteArray("TransactionId"),
                    FamilyId = reader.GetGuidFromByteArray("FamilyId"),
                    FamilyName = reader.GetStringOrDefault("FamilyName"),
                    TotalPayable = reader.GetDecimal("TotalPayable"),
                    TotalPaid = reader.GetDecimal("TotalPaid"),
                    CreatedAt = reader.GetDateTimeUtc("CreatedAt"),
                    UpdatedOn = reader.GetDateTimeUtc("UpdatedOn")
                });
            }

            return results;
        }

    }
}
