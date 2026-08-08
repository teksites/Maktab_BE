using Cumulus.Data;
using Data;
using MaktabDataContracts.Enums;
using MaktabDataContracts.Requests.Course;
using MaktabDataContracts.Responses.Attendance;
using MaktabDataContracts.Responses.Course;
using MaktabDataContracts.Responses.InstituteStaff;
using System.Data;
using System.Data.Common;

namespace Courses.Repository.Implementation
{
    public class CourseStaffAssignmentRepository : DbRepository, ICourseStaffAssignmentRepository
    {
        public CourseStaffAssignmentRepository(IDatabase database) : base(database)
        {
        }

        public async Task<IReadOnlyList<CourseStaffAssignmentResponse>> GetCourseAssignments(Guid courseId, bool onlyActive = false)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false);
            return await GetCourseAssignmentsInternal(conn, null, courseId, onlyActive).ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<CourseGroupStaffAssignmentResponse>> GetCourseGroupAssignments(Guid courseEnrollmentGroupId, bool onlyActive = false)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false);
            return await GetCourseGroupAssignmentsInternal(conn, null, courseEnrollmentGroupId, onlyActive).ConfigureAwait(false);
        }

        public async Task<bool> HasActiveDirectGroupAssignments(Guid courseId)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false);
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT 1
                FROM course_group_staff_assignments
                WHERE CourseId = @CourseId
                  AND IsActive = 1
                  AND StartDate <= @EffectiveOn
                  AND (EndDate IS NULL OR EndDate >= @EffectiveOn)
                LIMIT 1";
            cmd.AddParameter("@CourseId", courseId.ToByteArray());
            cmd.AddParameter("@EffectiveOn", DateTime.UtcNow);
            var result = await cmd.ExecuteScalarAsync().ConfigureAwait(false);
            return result != null;
        }

        public async Task<IReadOnlyList<TeacherAssignedCourseGroupResponse>> GetAssignedCourseGroups(Guid userId, bool onlyActive = true)
        {
            var results = new List<TeacherAssignedCourseGroupResponse>();

            using var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false);
            using var cmd = conn.CreateCommand();
            cmd.CommandText = BuildAssignedCourseGroupsSql(onlyActive);
            cmd.AddParameter("@UserId", userId.ToByteArray());
            if (onlyActive)
            {
                cmd.AddParameter("@EffectiveOn", DateTime.UtcNow);
            }

            using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                results.Add(new TeacherAssignedCourseGroupResponse
                {
                    CourseId = reader.GetGuidFromByteArray("CourseId"),
                    CourseEnrollmentGroupId = reader.GetGuidFromByteArray("CourseEnrollmentGroupId"),
                    InstituteId = reader.GetGuidFromByteArray("InstituteId"),
                    CourseName = reader.GetStringOrDefault("CourseName", string.Empty),
                    CourseNameFr = reader.GetStringOrDefault("CourseNameFr", string.Empty),
                    GroupTitle = reader.GetStringOrDefault("GroupTitle", string.Empty),
                    GroupTitleFr = reader.GetStringOrDefault("GroupTitleFr", string.Empty),
                    CourseStartDate = reader.GetDateTimeUtc("CourseStartDate"),
                    CourseEndDate = reader.GetDateTimeUtc("CourseEndDate"),
                    CourseSession = (CourseSessionType)reader.GetByte("CourseSession"),
                    CanSelectMultipleEnrollmentGroups = reader.GetBoolean("CanSelectMultipleEnrollmentGroups"),
                    IsInheritedFromCourse = reader.GetBoolean("IsInheritedFromCourse"),
                    AssignmentRoles = (UserRoleType)reader.GetLongOrDefault("AssignmentRoles")
                });
            }

            return results;
        }

        public async Task<CourseStaffAssignmentsResponse> ReplaceCourseAssignments(SetCourseStaffAssignmentsRequest request)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false);
            using var tx = await conn.BeginTransactionAsync().ConfigureAwait(false);

            try
            {
                var now = DateTime.UtcNow;
                var existingAssignments = await GetCourseAssignmentsInternal(conn, tx, request.CourseId, onlyActive: false).ConfigureAwait(false);
                var existingById = existingAssignments.ToDictionary(item => item.CourseStaffAssignmentId);
                var requestIds = request.StaffAssignments
                    .Where(item => item.AssignmentId.HasValue)
                    .Select(item => item.AssignmentId!.Value)
                    .ToHashSet();

                foreach (var assignment in request.StaffAssignments)
                {
                    if (assignment.AssignmentId.HasValue && existingById.ContainsKey(assignment.AssignmentId.Value))
                    {
                        await UpdateCourseAssignment(conn, tx, assignment.AssignmentId.Value, assignment, now).ConfigureAwait(false);
                    }
                    else
                    {
                        await InsertCourseAssignment(conn, tx, request.CourseId, request.InstituteId, assignment, now).ConfigureAwait(false);
                    }
                }

                foreach (var existingAssignment in existingAssignments.Where(item => !requestIds.Contains(item.CourseStaffAssignmentId)))
                {
                    await DeactivateCourseAssignment(conn, tx, existingAssignment.CourseStaffAssignmentId, now).ConfigureAwait(false);
                }

                await tx.CommitAsync().ConfigureAwait(false);
            }
            catch
            {
                await tx.RollbackAsync().ConfigureAwait(false);
                throw;
            }

            var updatedAssignments = await GetCourseAssignments(request.CourseId, onlyActive: false).ConfigureAwait(false);
            return new CourseStaffAssignmentsResponse
            {
                CourseId = request.CourseId,
                InstituteId = request.InstituteId,
                StaffAssignments = updatedAssignments.ToList()
            };
        }

        public async Task<CourseGroupStaffAssignmentsResponse> ReplaceCourseGroupAssignments(SetCourseGroupStaffAssignmentsRequest request)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false);
            using var tx = await conn.BeginTransactionAsync().ConfigureAwait(false);

            try
            {
                var now = DateTime.UtcNow;
                var existingAssignments = await GetCourseGroupAssignmentsInternal(conn, tx, request.CourseEnrollmentGroupId, onlyActive: false).ConfigureAwait(false);
                var existingById = existingAssignments.ToDictionary(item => item.CourseGroupStaffAssignmentId);
                var requestIds = request.StaffAssignments
                    .Where(item => item.AssignmentId.HasValue)
                    .Select(item => item.AssignmentId!.Value)
                    .ToHashSet();

                foreach (var assignment in request.StaffAssignments)
                {
                    if (assignment.AssignmentId.HasValue && existingById.ContainsKey(assignment.AssignmentId.Value))
                    {
                        await UpdateCourseGroupAssignment(conn, tx, assignment.AssignmentId.Value, assignment, now).ConfigureAwait(false);
                    }
                    else
                    {
                        await InsertCourseGroupAssignment(conn, tx, request.CourseEnrollmentGroupId, request.CourseId, request.InstituteId, assignment, now).ConfigureAwait(false);
                    }
                }

                foreach (var existingAssignment in existingAssignments.Where(item => !requestIds.Contains(item.CourseGroupStaffAssignmentId)))
                {
                    await DeactivateCourseGroupAssignment(conn, tx, existingAssignment.CourseGroupStaffAssignmentId, now).ConfigureAwait(false);
                }

                await tx.CommitAsync().ConfigureAwait(false);
            }
            catch
            {
                await tx.RollbackAsync().ConfigureAwait(false);
                throw;
            }

            var updatedAssignments = await GetCourseGroupAssignments(request.CourseEnrollmentGroupId, onlyActive: false).ConfigureAwait(false);
            return new CourseGroupStaffAssignmentsResponse
            {
                CourseEnrollmentGroupId = request.CourseEnrollmentGroupId,
                CourseId = request.CourseId,
                InstituteId = request.InstituteId,
                UsesCourseLevelAssignments = false,
                StaffAssignments = updatedAssignments.ToList()
            };
        }

        private static async Task InsertCourseAssignment(
            DbConnection connection,
            DbTransaction? transaction,
            Guid courseId,
            Guid instituteId,
            CourseStaffAssignmentItemRequest assignment,
            DateTime now)
        {
            using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = @"
                INSERT INTO course_staff_assignments
                (CourseStaffAssignmentId, CourseId, InstituteId, UserId, AssignmentRoles, StartDate, EndDate, IsActive, CreatedAt, UpdatedOn)
                VALUES
                (@CourseStaffAssignmentId, @CourseId, @InstituteId, @UserId, @AssignmentRoles, @StartDate, @EndDate, @IsActive, @CreatedAt, @UpdatedOn)";

            cmd.AddParameter("@CourseStaffAssignmentId", Guid.NewGuid().ToByteArray());
            cmd.AddParameter("@CourseId", courseId.ToByteArray());
            cmd.AddParameter("@InstituteId", instituteId.ToByteArray());
            cmd.AddParameter("@UserId", assignment.UserId.ToByteArray());
            cmd.AddParameter("@AssignmentRoles", (long)assignment.AssignmentRoles);
            cmd.AddParameter("@StartDate", assignment.StartDate);
            cmd.AddParameter("@EndDate", (object?)assignment.EndDate ?? DBNull.Value);
            cmd.AddParameter("@IsActive", assignment.IsActive);
            cmd.AddParameter("@CreatedAt", now);
            cmd.AddParameter("@UpdatedOn", now);

            await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        private static async Task UpdateCourseAssignment(
            DbConnection connection,
            DbTransaction? transaction,
            Guid assignmentId,
            CourseStaffAssignmentItemRequest assignment,
            DateTime now)
        {
            using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = @"
                UPDATE course_staff_assignments
                SET AssignmentRoles = @AssignmentRoles,
                    StartDate = @StartDate,
                    EndDate = @EndDate,
                    IsActive = @IsActive,
                    UpdatedOn = @UpdatedOn
                WHERE CourseStaffAssignmentId = @CourseStaffAssignmentId";

            cmd.AddParameter("@CourseStaffAssignmentId", assignmentId.ToByteArray());
            cmd.AddParameter("@AssignmentRoles", (long)assignment.AssignmentRoles);
            cmd.AddParameter("@StartDate", assignment.StartDate);
            cmd.AddParameter("@EndDate", (object?)assignment.EndDate ?? DBNull.Value);
            cmd.AddParameter("@IsActive", assignment.IsActive);
            cmd.AddParameter("@UpdatedOn", now);

            await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        private static async Task DeactivateCourseAssignment(
            DbConnection connection,
            DbTransaction? transaction,
            Guid assignmentId,
            DateTime now)
        {
            using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = @"
                UPDATE course_staff_assignments
                SET IsActive = 0,
                    EndDate = COALESCE(EndDate, @EndDate),
                    UpdatedOn = @UpdatedOn
                WHERE CourseStaffAssignmentId = @CourseStaffAssignmentId";

            cmd.AddParameter("@CourseStaffAssignmentId", assignmentId.ToByteArray());
            cmd.AddParameter("@EndDate", now);
            cmd.AddParameter("@UpdatedOn", now);
            await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        private static async Task InsertCourseGroupAssignment(
            DbConnection connection,
            DbTransaction? transaction,
            Guid courseEnrollmentGroupId,
            Guid courseId,
            Guid instituteId,
            CourseStaffAssignmentItemRequest assignment,
            DateTime now)
        {
            using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = @"
                INSERT INTO course_group_staff_assignments
                (CourseGroupStaffAssignmentId, CourseEnrollmentGroupId, CourseId, InstituteId, UserId, AssignmentRoles, StartDate, EndDate, IsActive, CreatedAt, UpdatedOn)
                VALUES
                (@CourseGroupStaffAssignmentId, @CourseEnrollmentGroupId, @CourseId, @InstituteId, @UserId, @AssignmentRoles, @StartDate, @EndDate, @IsActive, @CreatedAt, @UpdatedOn)";

            cmd.AddParameter("@CourseGroupStaffAssignmentId", Guid.NewGuid().ToByteArray());
            cmd.AddParameter("@CourseEnrollmentGroupId", courseEnrollmentGroupId.ToByteArray());
            cmd.AddParameter("@CourseId", courseId.ToByteArray());
            cmd.AddParameter("@InstituteId", instituteId.ToByteArray());
            cmd.AddParameter("@UserId", assignment.UserId.ToByteArray());
            cmd.AddParameter("@AssignmentRoles", (long)assignment.AssignmentRoles);
            cmd.AddParameter("@StartDate", assignment.StartDate);
            cmd.AddParameter("@EndDate", (object?)assignment.EndDate ?? DBNull.Value);
            cmd.AddParameter("@IsActive", assignment.IsActive);
            cmd.AddParameter("@CreatedAt", now);
            cmd.AddParameter("@UpdatedOn", now);

            await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        private static async Task UpdateCourseGroupAssignment(
            DbConnection connection,
            DbTransaction? transaction,
            Guid assignmentId,
            CourseStaffAssignmentItemRequest assignment,
            DateTime now)
        {
            using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = @"
                UPDATE course_group_staff_assignments
                SET AssignmentRoles = @AssignmentRoles,
                    StartDate = @StartDate,
                    EndDate = @EndDate,
                    IsActive = @IsActive,
                    UpdatedOn = @UpdatedOn
                WHERE CourseGroupStaffAssignmentId = @CourseGroupStaffAssignmentId";

            cmd.AddParameter("@CourseGroupStaffAssignmentId", assignmentId.ToByteArray());
            cmd.AddParameter("@AssignmentRoles", (long)assignment.AssignmentRoles);
            cmd.AddParameter("@StartDate", assignment.StartDate);
            cmd.AddParameter("@EndDate", (object?)assignment.EndDate ?? DBNull.Value);
            cmd.AddParameter("@IsActive", assignment.IsActive);
            cmd.AddParameter("@UpdatedOn", now);

            await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        private static async Task DeactivateCourseGroupAssignment(
            DbConnection connection,
            DbTransaction? transaction,
            Guid assignmentId,
            DateTime now)
        {
            using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = @"
                UPDATE course_group_staff_assignments
                SET IsActive = 0,
                    EndDate = COALESCE(EndDate, @EndDate),
                    UpdatedOn = @UpdatedOn
                WHERE CourseGroupStaffAssignmentId = @CourseGroupStaffAssignmentId";

            cmd.AddParameter("@CourseGroupStaffAssignmentId", assignmentId.ToByteArray());
            cmd.AddParameter("@EndDate", now);
            cmd.AddParameter("@UpdatedOn", now);
            await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        private static async Task<IReadOnlyList<CourseStaffAssignmentResponse>> GetCourseAssignmentsInternal(
            DbConnection connection,
            DbTransaction? transaction,
            Guid courseId,
            bool onlyActive)
        {
            var results = new List<CourseStaffAssignmentResponse>();
            using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = BuildCourseAssignmentSelectSql(onlyActive);
            cmd.AddParameter("@CourseId", courseId.ToByteArray());
            if (onlyActive)
            {
                cmd.AddParameter("@EffectiveOn", DateTime.UtcNow);
            }

            using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                results.Add(MapCourseAssignment(reader));
            }

            return results;
        }

        private static async Task<IReadOnlyList<CourseGroupStaffAssignmentResponse>> GetCourseGroupAssignmentsInternal(
            DbConnection connection,
            DbTransaction? transaction,
            Guid courseEnrollmentGroupId,
            bool onlyActive)
        {
            var results = new List<CourseGroupStaffAssignmentResponse>();
            using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = BuildCourseGroupAssignmentSelectSql(onlyActive);
            cmd.AddParameter("@CourseEnrollmentGroupId", courseEnrollmentGroupId.ToByteArray());
            if (onlyActive)
            {
                cmd.AddParameter("@EffectiveOn", DateTime.UtcNow);
            }

            using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                results.Add(MapCourseGroupAssignment(reader));
            }

            return results;
        }

        private static string BuildCourseAssignmentSelectSql(bool onlyActive)
        {
            var activeFilter = onlyActive
                ? "AND csa.IsActive = 1 AND csa.StartDate <= @EffectiveOn AND (csa.EndDate IS NULL OR csa.EndDate >= @EffectiveOn)"
                : string.Empty;

            return $@"
                SELECT
                    csa.CourseStaffAssignmentId,
                    csa.CourseId,
                    csa.InstituteId,
                    csa.UserId,
                    csa.AssignmentRoles,
                    csa.StartDate,
                    csa.EndDate,
                    csa.IsActive,
                    csa.CreatedAt,
                    csa.UpdatedOn,
                    u.FamilyId,
                    u.FirstName,
                    u.LastName,
                    u.UserName,
                    u.Email,
                    u.Phone,
                    u.UserRole AS GlobalUserRoles
                FROM course_staff_assignments csa
                INNER JOIN user_info u ON u.UserId = csa.UserId
                WHERE csa.CourseId = @CourseId
                {activeFilter}
                ORDER BY csa.StartDate, u.FirstName, u.LastName";
        }

        private static string BuildCourseGroupAssignmentSelectSql(bool onlyActive)
        {
            var activeFilter = onlyActive
                ? "AND cgsa.IsActive = 1 AND cgsa.StartDate <= @EffectiveOn AND (cgsa.EndDate IS NULL OR cgsa.EndDate >= @EffectiveOn)"
                : string.Empty;

            return $@"
                SELECT
                    cgsa.CourseGroupStaffAssignmentId,
                    cgsa.CourseEnrollmentGroupId,
                    cgsa.CourseId,
                    cgsa.InstituteId,
                    cgsa.UserId,
                    cgsa.AssignmentRoles,
                    cgsa.StartDate,
                    cgsa.EndDate,
                    cgsa.IsActive,
                    cgsa.CreatedAt,
                    cgsa.UpdatedOn,
                    u.FamilyId,
                    u.FirstName,
                    u.LastName,
                    u.UserName,
                    u.Email,
                    u.Phone,
                    u.UserRole AS GlobalUserRoles
                FROM course_group_staff_assignments cgsa
                INNER JOIN user_info u ON u.UserId = cgsa.UserId
                WHERE cgsa.CourseEnrollmentGroupId = @CourseEnrollmentGroupId
                {activeFilter}
                ORDER BY cgsa.StartDate, u.FirstName, u.LastName";
        }

        private static string BuildAssignedCourseGroupsSql(bool onlyActive)
        {
            var activeCourseAssignmentFilter = onlyActive
                ? @"AND csa.IsActive = 1
                    AND csa.StartDate <= @EffectiveOn
                    AND (csa.EndDate IS NULL OR csa.EndDate >= @EffectiveOn)
                    AND c.IsActive = 1
                    AND ceg.IsActive = 1
                    AND c.StartDate <= @EffectiveOn
                    AND c.EndDate >= @EffectiveOn"
                : string.Empty;

            var activeGroupAssignmentFilter = onlyActive
                ? @"AND cgsa.IsActive = 1
                    AND cgsa.StartDate <= @EffectiveOn
                    AND (cgsa.EndDate IS NULL OR cgsa.EndDate >= @EffectiveOn)
                    AND c.IsActive = 1
                    AND ceg.IsActive = 1
                    AND c.StartDate <= @EffectiveOn
                    AND c.EndDate >= @EffectiveOn"
                : string.Empty;

            return $@"
                SELECT
                    c.CourseId,
                    ceg.CourseEnrollmentGroupId,
                    c.InstituteId,
                    c.Name AS CourseName,
                    c.NameFr AS CourseNameFr,
                    ceg.GroupTitle,
                    ceg.GroupTitleFr,
                    c.StartDate AS CourseStartDate,
                    c.EndDate AS CourseEndDate,
                    c.CourseSession,
                    c.CanSelectMultipleEnrollmentGroups,
                    1 AS IsInheritedFromCourse,
                    csa.AssignmentRoles
                FROM course_staff_assignments csa
                INNER JOIN courses c ON c.CourseId = csa.CourseId
                INNER JOIN course_enrollment_groups ceg ON ceg.CourseId = c.CourseId
                WHERE csa.UserId = @UserId
                {activeCourseAssignmentFilter}

                UNION ALL

                SELECT
                    c.CourseId,
                    ceg.CourseEnrollmentGroupId,
                    c.InstituteId,
                    c.Name AS CourseName,
                    c.NameFr AS CourseNameFr,
                    ceg.GroupTitle,
                    ceg.GroupTitleFr,
                    c.StartDate AS CourseStartDate,
                    c.EndDate AS CourseEndDate,
                    c.CourseSession,
                    c.CanSelectMultipleEnrollmentGroups,
                    0 AS IsInheritedFromCourse,
                    cgsa.AssignmentRoles
                FROM course_group_staff_assignments cgsa
                INNER JOIN course_enrollment_groups ceg ON ceg.CourseEnrollmentGroupId = cgsa.CourseEnrollmentGroupId
                INNER JOIN courses c ON c.CourseId = cgsa.CourseId
                WHERE cgsa.UserId = @UserId
                {activeGroupAssignmentFilter}";
        }

        private static CourseStaffAssignmentResponse MapCourseAssignment(IDataReader reader)
        {
            return new CourseStaffAssignmentResponse
            {
                CourseStaffAssignmentId = reader.GetGuidFromByteArray("CourseStaffAssignmentId"),
                CourseId = reader.GetGuidFromByteArray("CourseId"),
                InstituteId = reader.GetGuidFromByteArray("InstituteId"),
                AssignmentRoles = (UserRoleType)reader.GetLongOrDefault("AssignmentRoles"),
                StartDate = reader.GetDateTimeUtc("StartDate"),
                EndDate = reader.GetNullableDateTimeUtc("EndDate"),
                IsActive = reader.GetBoolean("IsActive"),
                CreatedAt = reader.GetDateTimeUtc("CreatedAt"),
                UpdatedOn = reader.GetDateTimeUtc("UpdatedOn"),
                StaffUser = MapStaffUser(reader)
            };
        }

        private static CourseGroupStaffAssignmentResponse MapCourseGroupAssignment(IDataReader reader)
        {
            return new CourseGroupStaffAssignmentResponse
            {
                CourseGroupStaffAssignmentId = reader.GetGuidFromByteArray("CourseGroupStaffAssignmentId"),
                CourseEnrollmentGroupId = reader.GetGuidFromByteArray("CourseEnrollmentGroupId"),
                CourseId = reader.GetGuidFromByteArray("CourseId"),
                InstituteId = reader.GetGuidFromByteArray("InstituteId"),
                IsInheritedFromCourse = false,
                AssignmentRoles = (UserRoleType)reader.GetLongOrDefault("AssignmentRoles"),
                StartDate = reader.GetDateTimeUtc("StartDate"),
                EndDate = reader.GetNullableDateTimeUtc("EndDate"),
                IsActive = reader.GetBoolean("IsActive"),
                CreatedAt = reader.GetDateTimeUtc("CreatedAt"),
                UpdatedOn = reader.GetDateTimeUtc("UpdatedOn"),
                StaffUser = MapStaffUser(reader)
            };
        }

        private static InstituteStaffUserSummaryResponse MapStaffUser(IDataReader reader)
        {
            return new InstituteStaffUserSummaryResponse
            {
                UserId = reader.GetGuidFromByteArray("UserId"),
                FamilyId = reader.GetGuidFromByteArray("FamilyId"),
                FirstName = reader.GetStringOrDefault("FirstName", string.Empty),
                LastName = reader.GetStringOrDefault("LastName", string.Empty),
                UserName = reader.GetStringOrDefault("UserName", string.Empty),
                Email = reader.GetStringOrDefault("Email", string.Empty),
                Phone = reader.GetStringOrDefault("Phone", string.Empty),
                GlobalUserRoles = (UserRoleType)reader.GetLongOrDefault("GlobalUserRoles")
            };
        }
    }
}
