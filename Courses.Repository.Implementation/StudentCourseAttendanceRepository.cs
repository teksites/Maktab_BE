using Cumulus.Data;
using Data;
using MaktabDataContracts.Enums;
using MaktabDataContracts.Requests.Attendance;
using MaktabDataContracts.Responses.Attendance;
using System.Data;
using System.Data.Common;

namespace Courses.Repository.Implementation
{
    public class StudentCourseAttendanceRepository : DbRepository, IStudentCourseAttendanceRepository
    {
        public StudentCourseAttendanceRepository(IDatabase database) : base(database)
        {
        }

        public async Task<IReadOnlyList<StudentCourseAttendanceResponse>> GetCourseGroupAttendance(Guid courseEnrollmentGroupId, DateTime attendanceDate, Guid? childId = null)
        {
            var results = new List<StudentCourseAttendanceResponse>();

            using var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false);
            using var cmd = conn.CreateCommand();
            cmd.CommandText = BuildAttendanceSelectSql(childId.HasValue);
            cmd.AddParameter("@CourseEnrollmentGroupId", courseEnrollmentGroupId.ToByteArray());
            cmd.AddParameter("@AttendanceDate", attendanceDate.Date);
            if (childId.HasValue)
            {
                cmd.AddParameter("@ChildId", childId.Value.ToByteArray());
            }

            using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                results.Add(MapAttendance(reader));
            }

            return results;
        }

        public async Task<IReadOnlyList<AttendanceRecordResponse>> GetAttendanceRecords(GetAttendanceRecordsRequest request, IReadOnlyCollection<Guid>? accessibleGroupIds = null)
        {
            var results = new List<AttendanceRecordResponse>();

            if (accessibleGroupIds != null && accessibleGroupIds.Count == 0)
            {
                return results;
            }

            using var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false);
            using var cmd = conn.CreateCommand();

            var conditions = new List<string>();

            if (request.FamilyId.HasValue && request.FamilyId.Value != Guid.Empty)
            {
                conditions.Add("sca.FamilyId = @FamilyId");
                cmd.AddParameter("@FamilyId", request.FamilyId.Value.ToByteArray());
            }

            if (request.ChildId.HasValue && request.ChildId.Value != Guid.Empty)
            {
                conditions.Add("sca.ChildId = @ChildId");
                cmd.AddParameter("@ChildId", request.ChildId.Value.ToByteArray());
            }

            if (request.CourseId.HasValue && request.CourseId.Value != Guid.Empty)
            {
                conditions.Add("sca.CourseId = @CourseId");
                cmd.AddParameter("@CourseId", request.CourseId.Value.ToByteArray());
            }

            if (request.CourseEnrollmentGroupId.HasValue && request.CourseEnrollmentGroupId.Value != Guid.Empty)
            {
                conditions.Add("sca.CourseEnrollmentGroupId = @CourseEnrollmentGroupId");
                cmd.AddParameter("@CourseEnrollmentGroupId", request.CourseEnrollmentGroupId.Value.ToByteArray());
            }

            if (request.StartDate.HasValue)
            {
                conditions.Add("sca.AttendanceDate >= @StartDate");
                cmd.AddParameter("@StartDate", request.StartDate.Value.Date);
            }

            if (request.EndDate.HasValue)
            {
                conditions.Add("sca.AttendanceDate <= @EndDate");
                cmd.AddParameter("@EndDate", request.EndDate.Value.Date);
            }

            if (accessibleGroupIds != null)
            {
                var groupConditions = new List<string>();
                var groupIndex = 0;
                foreach (var groupId in accessibleGroupIds)
                {
                    var parameterName = $"@AccessibleGroupId{groupIndex++}";
                    groupConditions.Add($"sca.CourseEnrollmentGroupId = {parameterName}");
                    cmd.AddParameter(parameterName, groupId.ToByteArray());
                }

                if (groupConditions.Count > 0)
                {
                    conditions.Add($"({string.Join(" OR ", groupConditions)})");
                }
            }

            var whereClause = conditions.Count > 0
                ? $"WHERE {string.Join(" AND ", conditions)}"
                : string.Empty;

            cmd.CommandText = $@"
                SELECT
                    sca.StudentCourseAttendanceId,
                    sca.StudentCourseEnrollmentId,
                    sca.CourseEnrollmentGroupId,
                    sca.CourseId,
                    sca.InstituteId,
                    sca.FamilyId,
                    sca.ChildId,
                    sca.AttendanceDate,
                    sca.AttendanceStatus,
                    sca.LateArrivalTime,
                    sca.EarlyPickupTime,
                    sca.PickupContactType,
                    sca.PickupUserId,
                    sca.PickupOtherContactId,
                    sca.Notes,
                    sca.RecordedByUserId,
                    sca.IsActive,
                    sca.CreatedAt,
                    sca.UpdatedOn,
                    ci.FirstName AS ChildFirstName,
                    ci.LastName AS ChildLastName,
                    ci.RegistrationNumber,
                    c.Name AS CourseName,
                    ceg.GroupTitle,
                    COALESCE(
                        CONCAT(ui.FirstName, ' ', ui.LastName),
                        CONCAT(tui.FirstName, ' ', tui.LastName),
                        CONCAT(oci.FirstName, ' ', oci.LastName),
                        ''
                    ) AS PickupDisplayName
                FROM student_course_attendance sca
                INNER JOIN child_information ci ON ci.ChildId = sca.ChildId
                INNER JOIN courses c ON c.CourseId = sca.CourseId
                LEFT JOIN course_enrollment_groups ceg ON ceg.CourseEnrollmentGroupId = sca.CourseEnrollmentGroupId
                LEFT JOIN user_info ui ON ui.UserId = sca.PickupUserId
                LEFT JOIN temp_user_info tui ON tui.UserId = sca.PickupUserId
                LEFT JOIN other_contacts_information oci ON oci.ContactId = sca.PickupOtherContactId
                {whereClause}
                ORDER BY sca.AttendanceDate DESC, c.Name, ceg.GroupTitle, ci.FirstName, ci.LastName";

            using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                results.Add(new AttendanceRecordResponse
                {
                    StudentCourseAttendanceId = reader.GetGuidFromByteArray("StudentCourseAttendanceId"),
                    StudentCourseEnrollmentId = reader.GetGuidFromByteArray("StudentCourseEnrollmentId"),
                    CourseEnrollmentGroupId = reader.GetGuidFromByteArray("CourseEnrollmentGroupId"),
                    CourseId = reader.GetGuidFromByteArray("CourseId"),
                    InstituteId = reader.GetGuidFromByteArray("InstituteId"),
                    FamilyId = reader.GetGuidFromByteArray("FamilyId"),
                    ChildId = reader.GetGuidFromByteArray("ChildId"),
                    ChildName = $"{reader.GetStringOrDefault("ChildFirstName", string.Empty)} {reader.GetStringOrDefault("ChildLastName", string.Empty)}".Trim(),
                    RegistrationNumber = reader.GetStringOrDefault("RegistrationNumber", string.Empty),
                    CourseName = reader.GetStringOrDefault("CourseName", string.Empty),
                    GroupTitle = reader.GetStringOrDefault("GroupTitle", string.Empty),
                    AttendanceDate = reader.GetDateTimeUtc("AttendanceDate"),
                    AttendanceStatus = (AttendanceStatus)reader.GetInt32("AttendanceStatus"),
                    LateArrivalTime = reader.GetNullableDateTimeUtc("LateArrivalTime"),
                    EarlyPickupTime = reader.GetNullableDateTimeUtc("EarlyPickupTime"),
                    PickupContactType = (PickupContactType)reader.GetInt32("PickupContactType"),
                    PickupUserId = reader.GetNullableGuidFromByteArray("PickupUserId"),
                    PickupOtherContactId = reader.GetNullableGuidFromByteArray("PickupOtherContactId"),
                    PickupDisplayName = reader.GetStringOrDefault("PickupDisplayName", string.Empty),
                    Notes = reader.GetStringOrDefault("Notes", string.Empty),
                    RecordedByUserId = reader.GetGuidFromByteArray("RecordedByUserId"),
                    IsActive = reader.GetBoolean("IsActive"),
                    CreatedAt = reader.GetDateTimeUtc("CreatedAt"),
                    UpdatedOn = reader.GetDateTimeUtc("UpdatedOn")
                });
            }

            return results;
        }

        public async Task<(int TotalRecords, int PresentCount)> GetAttendanceSummary(Guid studentCourseEnrollmentId)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false);
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                SELECT
                    COUNT(*) AS TotalRecords,
                    SUM(CASE WHEN AttendanceStatus = @PresentStatus THEN 1 ELSE 0 END) AS PresentCount
                FROM student_course_attendance
                WHERE StudentCourseEnrollmentId = @StudentCourseEnrollmentId
                  AND IsActive = TRUE";

            cmd.AddParameter("@StudentCourseEnrollmentId", studentCourseEnrollmentId.ToByteArray());
            cmd.AddParameter("@PresentStatus", (int)AttendanceStatus.Present);

            using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
            if (!await reader.ReadAsync().ConfigureAwait(false))
            {
                return (0, 0);
            }

            return (
                reader.GetIntOrDefault("TotalRecords", 0),
                reader.GetIntOrDefault("PresentCount", 0));
        }

        public async Task<CourseGroupAttendanceResponse> UpsertCourseGroupAttendance(UpsertCourseGroupAttendanceRequest request)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false);
            using var tx = await conn.BeginTransactionAsync().ConfigureAwait(false);

            try
            {
                var attendanceDate = request.AttendanceDate.Date;
                var now = DateTime.UtcNow;
                var existingRows = (await GetExistingRows(conn, tx, request.CourseEnrollmentGroupId, attendanceDate).ConfigureAwait(false))
                    .ToDictionary(row => row.StudentCourseEnrollmentId, row => row.StudentCourseAttendanceId);

                foreach (var student in request.Students)
                {
                    var attendanceId = existingRows.TryGetValue(student.StudentCourseEnrollmentId, out var existingId)
                        ? existingId
                        : student.StudentCourseAttendanceId.GetValueOrDefault(Guid.NewGuid());

                    using var cmd = conn.CreateCommand();
                    cmd.Transaction = tx;
                    cmd.CommandText = @"
                        INSERT INTO student_course_attendance
                        (StudentCourseAttendanceId, StudentCourseEnrollmentId, CourseEnrollmentGroupId, CourseId, InstituteId, FamilyId, ChildId,
                         AttendanceDate, AttendanceStatus, LateArrivalTime, EarlyPickupTime, PickupContactType, PickupUserId, PickupOtherContactId,
                         Notes, RecordedByUserId, IsActive, CreatedAt, UpdatedOn)
                        VALUES
                        (@StudentCourseAttendanceId, @StudentCourseEnrollmentId, @CourseEnrollmentGroupId, @CourseId, @InstituteId, @FamilyId, @ChildId,
                         @AttendanceDate, @AttendanceStatus, @LateArrivalTime, @EarlyPickupTime, @PickupContactType, @PickupUserId, @PickupOtherContactId,
                         @Notes, @RecordedByUserId, @IsActive, @CreatedAt, @UpdatedOn)
                        ON DUPLICATE KEY UPDATE
                            CourseEnrollmentGroupId = VALUES(CourseEnrollmentGroupId),
                            CourseId = VALUES(CourseId),
                            InstituteId = VALUES(InstituteId),
                            FamilyId = VALUES(FamilyId),
                            ChildId = VALUES(ChildId),
                            AttendanceStatus = VALUES(AttendanceStatus),
                            LateArrivalTime = VALUES(LateArrivalTime),
                            EarlyPickupTime = VALUES(EarlyPickupTime),
                            PickupContactType = VALUES(PickupContactType),
                            PickupUserId = VALUES(PickupUserId),
                            PickupOtherContactId = VALUES(PickupOtherContactId),
                            Notes = VALUES(Notes),
                            RecordedByUserId = VALUES(RecordedByUserId),
                            IsActive = VALUES(IsActive),
                            UpdatedOn = VALUES(UpdatedOn)";

                    cmd.AddParameter("@StudentCourseAttendanceId", attendanceId.ToByteArray());
                    cmd.AddParameter("@StudentCourseEnrollmentId", student.StudentCourseEnrollmentId.ToByteArray());
                    cmd.AddParameter("@CourseEnrollmentGroupId", request.CourseEnrollmentGroupId.ToByteArray());
                    cmd.AddParameter("@CourseId", request.CourseId.ToByteArray());
                    cmd.AddParameter("@InstituteId", request.InstituteId.ToByteArray());
                    cmd.AddParameter("@FamilyId", student.FamilyId.ToByteArray());
                    cmd.AddParameter("@ChildId", student.ChildId.ToByteArray());
                    cmd.AddParameter("@AttendanceDate", attendanceDate);
                    cmd.AddParameter("@AttendanceStatus", (int)student.AttendanceStatus);
                    cmd.AddParameter("@LateArrivalTime", (object?)student.LateArrivalTime ?? DBNull.Value);
                    cmd.AddParameter("@EarlyPickupTime", (object?)student.EarlyPickupTime ?? DBNull.Value);
                    cmd.AddParameter("@PickupContactType", (int)student.PickupContactType);
                    cmd.AddParameter("@PickupUserId", student.PickupUserId.HasValue ? student.PickupUserId.Value.ToByteArray() : DBNull.Value);
                    cmd.AddParameter("@PickupOtherContactId", student.PickupOtherContactId.HasValue ? student.PickupOtherContactId.Value.ToByteArray() : DBNull.Value);
                    cmd.AddParameter("@Notes", student.Notes ?? string.Empty);
                    cmd.AddParameter("@RecordedByUserId", request.RecordedByUserId.ToByteArray());
                    cmd.AddParameter("@IsActive", student.IsActive);
                    cmd.AddParameter("@CreatedAt", now);
                    cmd.AddParameter("@UpdatedOn", now);

                    await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                }

                await tx.CommitAsync().ConfigureAwait(false);
            }
            catch
            {
                await tx.RollbackAsync().ConfigureAwait(false);
                throw;
            }

            var students = await GetCourseGroupAttendance(request.CourseEnrollmentGroupId, request.AttendanceDate.Date).ConfigureAwait(false);
            return new CourseGroupAttendanceResponse
            {
                CourseId = request.CourseId,
                CourseEnrollmentGroupId = request.CourseEnrollmentGroupId,
                InstituteId = request.InstituteId,
                AttendanceDate = request.AttendanceDate.Date,
                RecordedByUserId = request.RecordedByUserId,
                Students = students.ToList()
            };
        }

        private static string BuildAttendanceSelectSql(bool includeChildFilter)
        {
            var childFilter = includeChildFilter ? "AND sca.ChildId = @ChildId" : string.Empty;

            return $@"
                SELECT
                    sca.StudentCourseAttendanceId,
                    sca.StudentCourseEnrollmentId,
                    sca.CourseEnrollmentGroupId,
                    sca.CourseId,
                    sca.InstituteId,
                    sca.FamilyId,
                    sca.ChildId,
                    sca.AttendanceDate,
                    sca.AttendanceStatus,
                    sca.LateArrivalTime,
                    sca.EarlyPickupTime,
                    sca.PickupContactType,
                    sca.PickupUserId,
                    sca.PickupOtherContactId,
                    sca.Notes,
                    sca.RecordedByUserId,
                    sca.IsActive,
                    sca.CreatedAt,
                    sca.UpdatedOn,
                    ci.FirstName AS ChildFirstName,
                    ci.LastName AS ChildLastName,
                    COALESCE(
                        CONCAT(ui.FirstName, ' ', ui.LastName),
                        CONCAT(tui.FirstName, ' ', tui.LastName),
                        CONCAT(oci.FirstName, ' ', oci.LastName),
                        ''
                    ) AS PickupDisplayName
                FROM student_course_attendance sca
                INNER JOIN child_information ci ON ci.ChildId = sca.ChildId
                LEFT JOIN user_info ui ON ui.UserId = sca.PickupUserId
                LEFT JOIN temp_user_info tui ON tui.UserId = sca.PickupUserId
                LEFT JOIN other_contacts_information oci ON oci.ContactId = sca.PickupOtherContactId
                WHERE sca.CourseEnrollmentGroupId = @CourseEnrollmentGroupId
                  AND sca.AttendanceDate = @AttendanceDate
                  {childFilter}
                ORDER BY ci.FirstName, ci.LastName";
        }

        private static StudentCourseAttendanceResponse MapAttendance(IDataReader reader)
        {
            return new StudentCourseAttendanceResponse
            {
                StudentCourseAttendanceId = reader.GetGuidFromByteArray("StudentCourseAttendanceId"),
                StudentCourseEnrollmentId = reader.GetGuidFromByteArray("StudentCourseEnrollmentId"),
                ChildId = reader.GetGuidFromByteArray("ChildId"),
                FamilyId = reader.GetGuidFromByteArray("FamilyId"),
                ChildName = $"{reader.GetStringOrDefault("ChildFirstName", string.Empty)} {reader.GetStringOrDefault("ChildLastName", string.Empty)}".Trim(),
                AttendanceStatus = (AttendanceStatus)reader.GetInt32("AttendanceStatus"),
                LateArrivalTime = reader.GetNullableDateTimeUtc("LateArrivalTime"),
                EarlyPickupTime = reader.GetNullableDateTimeUtc("EarlyPickupTime"),
                PickupContactType = (PickupContactType)reader.GetInt32("PickupContactType"),
                PickupUserId = reader.GetNullableGuidFromByteArray("PickupUserId"),
                PickupOtherContactId = reader.GetNullableGuidFromByteArray("PickupOtherContactId"),
                PickupDisplayName = reader.GetStringOrDefault("PickupDisplayName", string.Empty),
                Notes = reader.GetStringOrDefault("Notes", string.Empty),
                IsActive = reader.GetBoolean("IsActive"),
                RecordedByUserId = reader.GetGuidFromByteArray("RecordedByUserId"),
                CreatedAt = reader.GetDateTimeUtc("CreatedAt"),
                UpdatedOn = reader.GetDateTimeUtc("UpdatedOn")
            };
        }

        private static async Task<IReadOnlyList<StudentCourseAttendanceResponse>> GetExistingRows(
            DbConnection connection,
            DbTransaction transaction,
            Guid courseEnrollmentGroupId,
            DateTime attendanceDate)
        {
            var results = new List<StudentCourseAttendanceResponse>();
            using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = @"
                SELECT
                    StudentCourseAttendanceId,
                    StudentCourseEnrollmentId,
                    ChildId,
                    FamilyId,
                    AttendanceStatus,
                    LateArrivalTime,
                    EarlyPickupTime,
                    PickupContactType,
                    PickupUserId,
                    PickupOtherContactId,
                    Notes,
                    RecordedByUserId,
                    IsActive,
                    CreatedAt,
                    UpdatedOn
                FROM student_course_attendance
                WHERE CourseEnrollmentGroupId = @CourseEnrollmentGroupId
                  AND AttendanceDate = @AttendanceDate";
            cmd.AddParameter("@CourseEnrollmentGroupId", courseEnrollmentGroupId.ToByteArray());
            cmd.AddParameter("@AttendanceDate", attendanceDate.Date);

            using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                results.Add(new StudentCourseAttendanceResponse
                {
                    StudentCourseAttendanceId = reader.GetGuidFromByteArray("StudentCourseAttendanceId"),
                    StudentCourseEnrollmentId = reader.GetGuidFromByteArray("StudentCourseEnrollmentId")
                });
            }

            return results;
        }
    }
}
