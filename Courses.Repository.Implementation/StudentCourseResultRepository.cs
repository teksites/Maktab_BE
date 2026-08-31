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

        public async Task<StudentCourseResultResponse?> GetByChildAndCourse(Guid childId, Guid courseId)
        {
            var results = await GetResults(
                "scr.ChildId = @ChildId AND scr.CourseId = @CourseId",
                cmd =>
                {
                    cmd.AddParameter("@ChildId", childId.ToByteArray());
                    cmd.AddParameter("@CourseId", courseId.ToByteArray());
                }).ConfigureAwait(false);

            return results.FirstOrDefault();
        }

        public Task<IReadOnlyList<StudentCourseResultResponse>> GetByFamilyId(Guid familyId, Guid? courseId = null)
        {
            return GetResultsByScope("scr.FamilyId = @FamilyId", familyId, courseId, "@FamilyId");
        }

        public Task<IReadOnlyList<StudentCourseResultResponse>> GetByChildId(Guid childId, Guid? courseId = null)
        {
            return GetResultsByScope("scr.ChildId = @ChildId", childId, courseId, "@ChildId");
        }

        public Task<IReadOnlyList<StudentCourseResultResponse>> GetByCourseId(Guid courseId)
        {
            return GetResults("scr.CourseId = @CourseId", cmd =>
            {
                cmd.AddParameter("@CourseId", courseId.ToByteArray());
            });
        }

        public async Task<StudentCourseResultResponse> Upsert(
            Guid childId,
            Guid familyId,
            Guid courseId,
            Guid instituteId,
            decimal? attendancePercentage,
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
                (StudentCourseResultId, ChildId, FamilyId, CourseId, InstituteId,
                 AttendancePercentage, ResultStatus, Remarks, RecordedByUserId, IsActive, CreatedAt, UpdatedOn)
                VALUES
                (@StudentCourseResultId, @ChildId, @FamilyId, @CourseId, @InstituteId,
                 @AttendancePercentage, @ResultStatus, @Remarks, @RecordedByUserId, @IsActive, @CreatedAt, @UpdatedOn)
                ON DUPLICATE KEY UPDATE
                    FamilyId = VALUES(FamilyId),
                    InstituteId = VALUES(InstituteId),
                    AttendancePercentage = VALUES(AttendancePercentage),
                    ResultStatus = VALUES(ResultStatus),
                    Remarks = VALUES(Remarks),
                    RecordedByUserId = VALUES(RecordedByUserId),
                    IsActive = VALUES(IsActive),
                    UpdatedOn = VALUES(UpdatedOn)";

            cmd.AddParameter("@StudentCourseResultId", studentCourseResultId.ToByteArray());
            cmd.AddParameter("@ChildId", childId.ToByteArray());
            cmd.AddParameter("@FamilyId", familyId.ToByteArray());
            cmd.AddParameter("@CourseId", courseId.ToByteArray());
            cmd.AddParameter("@InstituteId", instituteId.ToByteArray());
            cmd.AddParameter("@AttendancePercentage", (object?)attendancePercentage ?? DBNull.Value);
            cmd.AddParameter("@ResultStatus", (int)resultStatus);
            cmd.AddParameter("@Remarks", string.IsNullOrWhiteSpace(remarks) ? DBNull.Value : remarks.Trim());
            cmd.AddParameter("@RecordedByUserId", recordedByUserId.ToByteArray());
            cmd.AddParameter("@IsActive", isActive);
            cmd.AddParameter("@CreatedAt", now);
            cmd.AddParameter("@UpdatedOn", now);

            await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);

            return await GetByChildAndCourse(childId, courseId).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Failed to load student course result after upsert.");
        }

        private Task<IReadOnlyList<StudentCourseResultResponse>> GetResultsByScope(
            string baseCondition,
            Guid scopeId,
            Guid? courseId,
            string scopeParameter)
        {
            var conditions = new List<string> { baseCondition };

            if (courseId.HasValue && courseId.Value != Guid.Empty)
            {
                conditions.Add("scr.CourseId = @CourseId");
            }

            return GetResults(string.Join(" AND ", conditions), cmd =>
            {
                cmd.AddParameter(scopeParameter, scopeId.ToByteArray());
                if (courseId.HasValue && courseId.Value != Guid.Empty)
                {
                    cmd.AddParameter("@CourseId", courseId.Value.ToByteArray());
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
                    scr.ChildId,
                    scr.FamilyId,
                    scr.CourseId,
                    scr.InstituteId,
                    scr.AttendancePercentage,
                    scr.ResultStatus,
                    scr.Remarks,
                    scr.RecordedByUserId,
                    scr.IsActive,
                    scr.CreatedAt,
                    scr.UpdatedOn,
                    c.Name AS CourseName,
                    ci.FirstName AS ChildFirstName,
                    ci.LastName AS ChildLastName,
                    ci.ArabicName AS ChildArabicName,
                    ci.RegistrationNumber
                FROM student_course_results scr
                INNER JOIN courses c ON c.CourseId = scr.CourseId
                INNER JOIN child_information ci ON ci.ChildId = scr.ChildId
                WHERE {whereClause}
                ORDER BY scr.UpdatedOn DESC, c.Name, ci.FirstName, ci.LastName";

            parameterize(cmd);

            using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                results.Add(new StudentCourseResultResponse
                {
                    StudentCourseResultId = reader.GetGuidFromByteArray("StudentCourseResultId"),
                    ChildId = reader.GetGuidFromByteArray("ChildId"),
                    FamilyId = reader.GetGuidFromByteArray("FamilyId"),
                    CourseId = reader.GetGuidFromByteArray("CourseId"),
                    InstituteId = reader.GetGuidFromByteArray("InstituteId"),
                    CourseName = reader.GetStringOrDefault("CourseName", string.Empty),
                    ChildName = $"{reader.GetStringOrDefault("ChildFirstName", string.Empty)} {reader.GetStringOrDefault("ChildLastName", string.Empty)}".Trim(),
                    ArabicName = reader.GetStringOrDefault("ChildArabicName", string.Empty),
                    RegistrationNumber = reader.GetStringOrDefault("RegistrationNumber", string.Empty),
                    AttendancePercentage = reader.IsDBNull("AttendancePercentage")
                        ? null
                        : reader.GetDecimal(reader.GetOrdinal("AttendancePercentage")),
                    HasAttendanceRecords = !reader.IsDBNull("AttendancePercentage"),
                    HasResult = true,
                    ResultStatus = (StudentCourseResultStatus)reader.GetInt32("ResultStatus"),
                    Remarks = reader.GetStringOrDefault("Remarks", string.Empty),
                    RecordedByUserId = reader.IsDBNull("RecordedByUserId")
                        ? null
                        : reader.GetGuidFromByteArray("RecordedByUserId"),
                    IsActive = reader.GetBoolean("IsActive"),
                    CreatedAt = reader.GetDateTimeUtc("CreatedAt"),
                    UpdatedOn = reader.GetDateTimeUtc("UpdatedOn")
                });
            }

            return results;
        }
    }
}
