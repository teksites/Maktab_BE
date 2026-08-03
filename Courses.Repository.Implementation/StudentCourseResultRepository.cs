using Cumulus.Data;
using Data;
using MaktabDataContracts.Enums;
using MaktabDataContracts.Responses.Course;
using System.Data.Common;

namespace Courses.Repository.Implementation
{
    public class StudentCourseResultRepository : DbRepository, IStudentCourseResultRepository
    {
        public StudentCourseResultRepository(IDatabase database) : base(database)
        {
        }

        public async Task<StudentCourseResultResponse?> GetByEnrollmentId(Guid studentCourseEnrollmentId)
        {
            var results = await GetResults("scr.StudentCourseEnrollmentId = @StudentCourseEnrollmentId", cmd =>
            {
                cmd.AddParameter("@StudentCourseEnrollmentId", studentCourseEnrollmentId.ToByteArray());
            }).ConfigureAwait(false);

            return results.FirstOrDefault();
        }

        public Task<IReadOnlyList<StudentCourseResultResponse>> GetByFamilyId(Guid familyId, Guid? courseId = null, Guid? courseEnrollmentGroupId = null)
        {
            return GetResultsByScope("scr.FamilyId = @FamilyId", familyId, courseId, courseEnrollmentGroupId, "@FamilyId");
        }

        public Task<IReadOnlyList<StudentCourseResultResponse>> GetByChildId(Guid childId, Guid? courseId = null, Guid? courseEnrollmentGroupId = null)
        {
            return GetResultsByScope("scr.ChildId = @ChildId", childId, courseId, courseEnrollmentGroupId, "@ChildId");
        }

        public async Task<StudentCourseResultResponse> Upsert(
            Guid studentCourseEnrollmentId,
            Guid childId,
            Guid familyId,
            Guid courseId,
            Guid courseEnrollmentGroupId,
            Guid instituteId,
            decimal attendancePercentage,
            StudentCourseResultStatus resultStatus,
            string remarks,
            Guid recordedByUserId,
            bool isActive)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false);
            using var cmd = conn.CreateCommand();

            var now = DateTime.UtcNow;
            var studentCourseResultId = Guid.NewGuid();

            cmd.CommandText = @"
                INSERT INTO student_course_results
                (StudentCourseResultId, StudentCourseEnrollmentId, ChildId, FamilyId, CourseId, CourseEnrollmentGroupId, InstituteId,
                 AttendancePercentage, ResultStatus, Remarks, RecordedByUserId, IsActive, CreatedAt, UpdatedOn)
                VALUES
                (@StudentCourseResultId, @StudentCourseEnrollmentId, @ChildId, @FamilyId, @CourseId, @CourseEnrollmentGroupId, @InstituteId,
                 @AttendancePercentage, @ResultStatus, @Remarks, @RecordedByUserId, @IsActive, @CreatedAt, @UpdatedOn)
                ON DUPLICATE KEY UPDATE
                    ChildId = VALUES(ChildId),
                    FamilyId = VALUES(FamilyId),
                    CourseId = VALUES(CourseId),
                    CourseEnrollmentGroupId = VALUES(CourseEnrollmentGroupId),
                    InstituteId = VALUES(InstituteId),
                    AttendancePercentage = VALUES(AttendancePercentage),
                    ResultStatus = VALUES(ResultStatus),
                    Remarks = VALUES(Remarks),
                    RecordedByUserId = VALUES(RecordedByUserId),
                    IsActive = VALUES(IsActive),
                    UpdatedOn = VALUES(UpdatedOn)";

            cmd.AddParameter("@StudentCourseResultId", studentCourseResultId.ToByteArray());
            cmd.AddParameter("@StudentCourseEnrollmentId", studentCourseEnrollmentId.ToByteArray());
            cmd.AddParameter("@ChildId", childId.ToByteArray());
            cmd.AddParameter("@FamilyId", familyId.ToByteArray());
            cmd.AddParameter("@CourseId", courseId.ToByteArray());
            cmd.AddParameter("@CourseEnrollmentGroupId", courseEnrollmentGroupId.ToByteArray());
            cmd.AddParameter("@InstituteId", instituteId.ToByteArray());
            cmd.AddParameter("@AttendancePercentage", attendancePercentage);
            cmd.AddParameter("@ResultStatus", (int)resultStatus);
            cmd.AddParameter("@Remarks", string.IsNullOrWhiteSpace(remarks) ? DBNull.Value : remarks.Trim());
            cmd.AddParameter("@RecordedByUserId", recordedByUserId.ToByteArray());
            cmd.AddParameter("@IsActive", isActive);
            cmd.AddParameter("@CreatedAt", now);
            cmd.AddParameter("@UpdatedOn", now);

            await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);

            return await GetByEnrollmentId(studentCourseEnrollmentId).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Failed to load student course result after upsert.");
        }

        private Task<IReadOnlyList<StudentCourseResultResponse>> GetResultsByScope(
            string baseCondition,
            Guid scopeId,
            Guid? courseId,
            Guid? courseEnrollmentGroupId,
            string scopeParameter)
        {
            var conditions = new List<string> { baseCondition };

            if (courseId.HasValue && courseId.Value != Guid.Empty)
            {
                conditions.Add("scr.CourseId = @CourseId");
            }

            if (courseEnrollmentGroupId.HasValue && courseEnrollmentGroupId.Value != Guid.Empty)
            {
                conditions.Add("scr.CourseEnrollmentGroupId = @CourseEnrollmentGroupId");
            }

            return GetResults(string.Join(" AND ", conditions), cmd =>
            {
                cmd.AddParameter(scopeParameter, scopeId.ToByteArray());
                if (courseId.HasValue && courseId.Value != Guid.Empty)
                {
                    cmd.AddParameter("@CourseId", courseId.Value.ToByteArray());
                }

                if (courseEnrollmentGroupId.HasValue && courseEnrollmentGroupId.Value != Guid.Empty)
                {
                    cmd.AddParameter("@CourseEnrollmentGroupId", courseEnrollmentGroupId.Value.ToByteArray());
                }
            });
        }

        private async Task<IReadOnlyList<StudentCourseResultResponse>> GetResults(string whereClause, Action<DbCommand> parameterize)
        {
            var results = new List<StudentCourseResultResponse>();

            using var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false);
            using var cmd = conn.CreateCommand();

            cmd.CommandText = $@"
                SELECT
                    scr.StudentCourseResultId,
                    scr.StudentCourseEnrollmentId,
                    scr.ChildId,
                    scr.FamilyId,
                    scr.CourseId,
                    scr.CourseEnrollmentGroupId,
                    scr.InstituteId,
                    scr.AttendancePercentage,
                    scr.ResultStatus,
                    scr.Remarks,
                    scr.RecordedByUserId,
                    scr.IsActive,
                    scr.CreatedAt,
                    scr.UpdatedOn,
                    c.Name AS CourseName,
                    ceg.GroupTitle,
                    ci.FirstName AS ChildFirstName,
                    ci.LastName AS ChildLastName,
                    ci.RegistrationNumber
                FROM student_course_results scr
                INNER JOIN courses c ON c.CourseId = scr.CourseId
                LEFT JOIN course_enrollment_groups ceg ON ceg.CourseEnrollmentGroupId = scr.CourseEnrollmentGroupId
                INNER JOIN child_information ci ON ci.ChildId = scr.ChildId
                WHERE {whereClause}
                ORDER BY scr.UpdatedOn DESC, c.Name, ceg.GroupTitle, ci.FirstName, ci.LastName";

            parameterize(cmd);

            using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                results.Add(new StudentCourseResultResponse
                {
                    StudentCourseResultId = reader.GetGuidFromByteArray("StudentCourseResultId"),
                    StudentCourseEnrollmentId = reader.GetGuidFromByteArray("StudentCourseEnrollmentId"),
                    ChildId = reader.GetGuidFromByteArray("ChildId"),
                    FamilyId = reader.GetGuidFromByteArray("FamilyId"),
                    CourseId = reader.GetGuidFromByteArray("CourseId"),
                    CourseEnrollmentGroupId = reader.GetGuidFromByteArray("CourseEnrollmentGroupId"),
                    InstituteId = reader.GetGuidFromByteArray("InstituteId"),
                    CourseName = reader.GetStringOrDefault("CourseName", string.Empty),
                    GroupTitle = reader.GetStringOrDefault("GroupTitle", string.Empty),
                    ChildName = $"{reader.GetStringOrDefault("ChildFirstName", string.Empty)} {reader.GetStringOrDefault("ChildLastName", string.Empty)}".Trim(),
                    RegistrationNumber = reader.GetStringOrDefault("RegistrationNumber", string.Empty),
                    AttendancePercentage = reader.GetDecimal(reader.GetOrdinal("AttendancePercentage")),
                    ResultStatus = (StudentCourseResultStatus)reader.GetInt32("ResultStatus"),
                    Remarks = reader.GetStringOrDefault("Remarks", string.Empty),
                    RecordedByUserId = reader.GetGuidFromByteArray("RecordedByUserId"),
                    IsActive = reader.GetBoolean("IsActive"),
                    CreatedAt = reader.GetDateTimeUtc("CreatedAt"),
                    UpdatedOn = reader.GetDateTimeUtc("UpdatedOn")
                });
            }

            return results;
        }
    }
}
