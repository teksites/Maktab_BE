using Cumulus.Data;
using Data;
using MaktabDataContracts.Enums;
using MaktabDataContracts.Requests.InstituteStaff;
using MaktabDataContracts.Responses.InstituteStaff;
using System.Data;

namespace Courses.Repository.Implementation
{
    public class InstituteStaffAssignmentRepository : DbRepository, IInstituteStaffAssignmentRepository
    {
        public InstituteStaffAssignmentRepository(IDatabase database) : base(database)
        {
        }

        public async Task<InstituteStaffAssignmentResponse?> AddAssignment(AddInstituteStaffAssignmentRequest request)
        {
            var assignmentId = Guid.NewGuid();
            var now = DateTime.UtcNow;

            using var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false);
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO institute_staff_assignments
                (InstituteStaffAssignmentId, InstituteId, UserId, StaffRoles, StartDate, EndDate, IsActive, CreatedAt, UpdatedOn)
                VALUES
                (@InstituteStaffAssignmentId, @InstituteId, @UserId, @StaffRoles, @StartDate, @EndDate, @IsActive, @CreatedAt, @UpdatedOn)";

            cmd.AddParameter("@InstituteStaffAssignmentId", assignmentId.ToByteArray());
            cmd.AddParameter("@InstituteId", request.InstituteId.ToByteArray());
            cmd.AddParameter("@UserId", request.UserId.ToByteArray());
            cmd.AddParameter("@StaffRoles", (long)request.StaffRoles);
            cmd.AddParameter("@StartDate", request.StartDate);
            cmd.AddParameter("@EndDate", (object?)request.EndDate ?? DBNull.Value);
            cmd.AddParameter("@IsActive", request.IsActive);
            cmd.AddParameter("@CreatedAt", now);
            cmd.AddParameter("@UpdatedOn", now);

            await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            return await GetAssignment(assignmentId).ConfigureAwait(false);
        }

        public async Task<InstituteStaffAssignmentResponse?> UpdateAssignment(UpdateInstituteStaffAssignmentRequest request)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false);
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                UPDATE institute_staff_assignments
                SET StaffRoles = @StaffRoles,
                    StartDate = @StartDate,
                    EndDate = @EndDate,
                    IsActive = @IsActive,
                    UpdatedOn = @UpdatedOn
                WHERE InstituteStaffAssignmentId = @InstituteStaffAssignmentId";

            cmd.AddParameter("@InstituteStaffAssignmentId", request.InstituteStaffAssignmentId.ToByteArray());
            cmd.AddParameter("@StaffRoles", (long)request.StaffRoles);
            cmd.AddParameter("@StartDate", request.StartDate);
            cmd.AddParameter("@EndDate", (object?)request.EndDate ?? DBNull.Value);
            cmd.AddParameter("@IsActive", request.IsActive);
            cmd.AddParameter("@UpdatedOn", DateTime.UtcNow);

            await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            return await GetAssignment(request.InstituteStaffAssignmentId).ConfigureAwait(false);
        }

        public async Task<bool> DeleteAssignment(Guid assignmentId, bool hardDelete = false)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false);
            using var cmd = conn.CreateCommand();

            if (hardDelete)
            {
                cmd.CommandText = "DELETE FROM institute_staff_assignments WHERE InstituteStaffAssignmentId = @InstituteStaffAssignmentId";
            }
            else
            {
                cmd.CommandText = @"
                    UPDATE institute_staff_assignments
                    SET IsActive = 0,
                        EndDate = COALESCE(EndDate, @EndDate),
                        UpdatedOn = @UpdatedOn
                    WHERE InstituteStaffAssignmentId = @InstituteStaffAssignmentId";
                cmd.AddParameter("@EndDate", DateTime.UtcNow);
                cmd.AddParameter("@UpdatedOn", DateTime.UtcNow);
            }

            cmd.AddParameter("@InstituteStaffAssignmentId", assignmentId.ToByteArray());
            return await cmd.ExecuteNonQueryAsync().ConfigureAwait(false) > 0;
        }

        public async Task<InstituteStaffAssignmentResponse?> GetAssignment(Guid assignmentId)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false);
            using var cmd = conn.CreateCommand();
            cmd.CommandText = BuildAssignmentSelectSql("WHERE isa.InstituteStaffAssignmentId = @InstituteStaffAssignmentId");
            cmd.AddParameter("@InstituteStaffAssignmentId", assignmentId.ToByteArray());

            using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
            if (!await reader.ReadAsync().ConfigureAwait(false))
            {
                return null;
            }

            return MapAssignment(reader);
        }

        public async Task<IReadOnlyList<InstituteStaffAssignmentResponse>> GetAssignments(GetInstituteStaffRequest request)
        {
            var results = new List<InstituteStaffAssignmentResponse>();

            using var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false);
            using var cmd = conn.CreateCommand();

            var conditions = new List<string> { "isa.InstituteId = @InstituteId" };
            cmd.AddParameter("@InstituteId", request.InstituteId.ToByteArray());

            if (request.OnlyActive)
            {
                conditions.Add("isa.IsActive = 1");
            }

            if (request.EffectiveOn.HasValue)
            {
                conditions.Add("isa.StartDate <= @EffectiveOn");
                conditions.Add("(isa.EndDate IS NULL OR isa.EndDate >= @EffectiveOn)");
                cmd.AddParameter("@EffectiveOn", request.EffectiveOn.Value);
            }

            if (request.RoleFilter != UserRoleType.None)
            {
                conditions.Add("(isa.StaffRoles & @RoleFilter) <> 0");
                cmd.AddParameter("@RoleFilter", (long)request.RoleFilter);
            }

            if (!string.IsNullOrWhiteSpace(request.SearchText))
            {
                conditions.Add(@"(
                    LOWER(u.FirstName) LIKE @SearchText OR
                    LOWER(u.LastName) LIKE @SearchText OR
                    LOWER(u.UserName) LIKE @SearchText OR
                    LOWER(u.Email) LIKE @SearchText OR
                    LOWER(u.Phone) LIKE @SearchText)");
                cmd.AddParameter("@SearchText", $"%{request.SearchText.Trim().ToLowerInvariant()}%");
            }

            cmd.CommandText = BuildAssignmentSelectSql($"WHERE {string.Join(" AND ", conditions)}");

            using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                results.Add(MapAssignment(reader));
            }

            return results;
        }

        public async Task<IReadOnlyList<InstituteStaffAssignmentResponse>> GetAssignments(Guid instituteId, Guid? userId = null, bool onlyActive = false)
        {
            var request = new GetInstituteStaffRequest
            {
                InstituteId = instituteId,
                OnlyActive = onlyActive
            };

            var assignments = await GetAssignments(request).ConfigureAwait(false);
            if (!userId.HasValue)
            {
                return assignments;
            }

            return assignments.Where(item => item.StaffUser.UserId == userId.Value).ToList();
        }

        public async Task<IReadOnlyList<InstituteStaffUserSummaryResponse>> GetCandidateUsers(GetInstituteStaffCandidatesRequest request)
        {
            var results = new List<InstituteStaffUserSummaryResponse>();
            using var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false);
            using var cmd = conn.CreateCommand();

            var conditions = new List<string>
            {
                "(u.UserRole & @EligibleRoleMask) <> 0"
            };

            cmd.AddParameter("@EligibleRoleMask", (long)(
                UserRoleType.Assistant |
                UserRoleType.SchoolTeacher |
                UserRoleType.SchoolSupervisor |
                UserRoleType.SchoolAdmin |
                UserRoleType.SuperUser |
                UserRoleType.Manager |
                UserRoleType.Admin));

            if (request.OnlyActive)
            {
                conditions.Add("u.IsActive = 1");
            }

            if (!string.IsNullOrWhiteSpace(request.SearchText))
            {
                conditions.Add(@"(
                    LOWER(u.FirstName) LIKE @SearchText OR
                    LOWER(u.LastName) LIKE @SearchText OR
                    LOWER(u.UserName) LIKE @SearchText OR
                    LOWER(u.Email) LIKE @SearchText OR
                    LOWER(u.Phone) LIKE @SearchText)");
                cmd.AddParameter("@SearchText", $"%{request.SearchText.Trim().ToLowerInvariant()}%");
            }

            cmd.CommandText = $@"
                SELECT
                    u.UserId,
                    u.FamilyId,
                    u.FirstName,
                    u.LastName,
                    u.UserName,
                    u.Email,
                    u.Phone,
                    u.UserRole AS GlobalUserRoles
                FROM user_info u
                WHERE {string.Join(" AND ", conditions)}
                ORDER BY u.FirstName, u.LastName, u.UserName";

            using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                results.Add(MapUserSummary(reader));
            }

            return results;
        }

        public async Task<InstituteStaffUserSummaryResponse?> GetUserSummary(Guid userId)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false);
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT
                    u.UserId,
                    u.FamilyId,
                    u.FirstName,
                    u.LastName,
                    u.UserName,
                    u.Email,
                    u.Phone,
                    u.UserRole AS GlobalUserRoles
                FROM user_info u
                WHERE u.UserId = @UserId";
            cmd.AddParameter("@UserId", userId.ToByteArray());

            using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
            if (!await reader.ReadAsync().ConfigureAwait(false))
            {
                return null;
            }

            return MapUserSummary(reader);
        }

        private static string BuildAssignmentSelectSql(string whereClause)
        {
            return $@"
                SELECT
                    isa.InstituteStaffAssignmentId,
                    isa.InstituteId,
                    isa.UserId,
                    isa.StaffRoles,
                    isa.StartDate,
                    isa.EndDate,
                    isa.IsActive,
                    isa.CreatedAt,
                    isa.UpdatedOn,
                    u.FamilyId,
                    u.FirstName,
                    u.LastName,
                    u.UserName,
                    u.Email,
                    u.Phone,
                    u.UserRole AS GlobalUserRoles
                FROM institute_staff_assignments isa
                INNER JOIN user_info u ON u.UserId = isa.UserId
                {whereClause}
                ORDER BY isa.StartDate, u.FirstName, u.LastName";
        }

        private static InstituteStaffAssignmentResponse MapAssignment(IDataReader reader)
        {
            return new InstituteStaffAssignmentResponse
            {
                InstituteStaffAssignmentId = reader.GetGuidFromByteArray("InstituteStaffAssignmentId"),
                InstituteId = reader.GetGuidFromByteArray("InstituteId"),
                StaffRoles = (UserRoleType)reader.GetLongOrDefault("StaffRoles"),
                StartDate = reader.GetDateTimeUtc("StartDate"),
                EndDate = reader.GetNullableDateTimeUtc("EndDate"),
                IsActive = reader.GetBoolean("IsActive"),
                CreatedAt = reader.GetDateTimeUtc("CreatedAt"),
                UpdatedOn = reader.GetDateTimeUtc("UpdatedOn"),
                StaffUser = new InstituteStaffUserSummaryResponse
                {
                    UserId = reader.GetGuidFromByteArray("UserId"),
                    FamilyId = reader.GetGuidFromByteArray("FamilyId"),
                    FirstName = reader.GetStringOrDefault("FirstName", string.Empty),
                    LastName = reader.GetStringOrDefault("LastName", string.Empty),
                    UserName = reader.GetStringOrDefault("UserName", string.Empty),
                    Email = reader.GetStringOrDefault("Email", string.Empty),
                    Phone = reader.GetStringOrDefault("Phone", string.Empty),
                    GlobalUserRoles = (UserRoleType)reader.GetLongOrDefault("GlobalUserRoles")
                }
            };
        }

        private static InstituteStaffUserSummaryResponse MapUserSummary(IDataReader reader)
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
