using Cumulus.Data;
using Data;
using MaktabDataContracts.Helpers;
using MaktabDataContracts.Requests.Course;
using MaktabDataContracts.Responses.Course;

namespace Courses.Repository.Implementation
{
    public class CourseEnrollmentGroupRepository : DbRepository, ICourseEnrollmentGroupRepository
    {
        public CourseEnrollmentGroupRepository(IDatabase database) : base(database) { }

        public async Task<CourseEnrollmentGroupResponse> AddGroup(AddCourseEnrollmentGroup group)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                INSERT INTO course_enrollment_groups 
                (CourseEnrollmentGroupId, CourseId, InstituteId, GroupTitle, GroupTitleFr, 
                 Details, DetailsFr, IsActive, CreatedAt, UpdatedOn, MaxStudents, 
                 AcedemicGroup, Fee, IfRegistrationOpen)
                VALUES 
                (@CourseEnrollmentGroupId, @CourseId, @InstituteId, @GroupTitle, @GroupTitleFr,
                 @Details, @DetailsFr, @IsActive, @CreatedAt, @UpdatedOn, @MaxStudents,
                 @AcedemicGroup, @Fee, @IfRegistrationOpen)";

            var groupId = Guid.NewGuid();

            cmd.AddParameter("@CourseEnrollmentGroupId", groupId.ToByteArray());
            cmd.AddParameter("@CourseId", group.CourseId.ToByteArray());
            cmd.AddParameter("@InstituteId", group.InstituteId.ToByteArray());
            cmd.AddParameter("@GroupTitle", group.GroupTitle);
            cmd.AddParameter("@GroupTitleFr", group.GroupTitleFr);
            cmd.AddParameter("@Details", (object?)group.Details ?? DBNull.Value);
            cmd.AddParameter("@DetailsFr", (object?)group.DetailsFr ?? DBNull.Value);
            cmd.AddParameter("@IsActive", true);
            cmd.AddParameter("@CreatedAt", DateTime.UtcNow);
            cmd.AddParameter("@UpdatedOn", DateTime.UtcNow);
            cmd.AddParameter("@MaxStudents", group.MaxStudents);
            cmd.AddParameter("@AcedemicGroup", (int) AcedemicGroupHelper.FromStrings(group.AcedemicGroups));
            cmd.AddParameter("@Fee", group.Fee);
            cmd.AddParameter("@IfRegistrationOpen", group.IfRegistrationOpen);

            await cmd.ExecuteNonQueryAsync();
            return await GetGroup(groupId);
        }

        public async Task<CourseEnrollmentGroupResponse?> GetGroup(Guid groupId)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"SELECT * FROM course_enrollment_groups WHERE CourseEnrollmentGroupId = @CourseEnrollmentGroupId";
            cmd.AddParameter("@CourseEnrollmentGroupId", groupId.ToByteArray());

            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return null;

            return MapToGroupResponse(reader);
        }

        public async Task<IEnumerable<CourseEnrollmentGroupResponse>> GetAllGroups(Guid courseId, bool isActive = true)
        {
            var results = new List<CourseEnrollmentGroupResponse>();
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"SELECT * FROM course_enrollment_groups WHERE CourseId = @CourseId ";

            if (isActive)
            {
                cmd.CommandText += " AND IsActive = TRUE";
            }
            cmd.AddParameter("@CourseId", courseId.ToByteArray());

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(MapToGroupResponse(reader));
            }
            return results;
        }

        public async Task<bool> UpdateGroup(Guid groupId, AddCourseEnrollmentGroup group)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                UPDATE course_enrollment_groups
                SET GroupTitle = @GroupTitle,
                    GroupTitleFr = @GroupTitleFr,
                    Details = @Details,
                    DetailsFr = @DetailsFr,
                    UpdatedOn = @UpdatedOn,
                    MaxStudents = @MaxStudents,
                    AcedemicGroup = @AcedemicGroup,
                    Fee = @Fee,
                    IfRegistrationOpen = @IfRegistrationOpen
                WHERE CourseEnrollmentGroupId = @CourseEnrollmentGroupId";

            cmd.AddParameter("@CourseEnrollmentGroupId", groupId.ToByteArray());
            cmd.AddParameter("@GroupTitle", group.GroupTitle);
            cmd.AddParameter("@GroupTitleFr", group.GroupTitleFr);
            cmd.AddParameter("@Details", (object?)group.Details ?? DBNull.Value);
            cmd.AddParameter("@DetailsFr", (object?)group.DetailsFr ?? DBNull.Value);
            cmd.AddParameter("@UpdatedOn", DateTime.UtcNow);
            cmd.AddParameter("@MaxStudents", group.MaxStudents);
            cmd.AddParameter("@AcedemicGroup",  AcedemicGroupHelper.FromStrings(group.AcedemicGroups));
            cmd.AddParameter("@Fee", group.Fee);
            cmd.AddParameter("@IfRegistrationOpen", group.IfRegistrationOpen);

            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        public async Task<bool> DeleteGroup(Guid groupId, bool hardDelete = false)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            if (hardDelete)
                cmd.CommandText = @"DELETE FROM course_enrollment_groups WHERE CourseEnrollmentGroupId = @CourseEnrollmentGroupId";
            else
                cmd.CommandText = @"UPDATE course_enrollment_groups SET IsActive = FALSE, UpdatedOn = @UpdatedOn WHERE CourseEnrollmentGroupId = @CourseEnrollmentGroupId";

            cmd.AddParameter("@CourseEnrollmentGroupId", groupId.ToByteArray());
            if (!hardDelete)
                cmd.AddParameter("@UpdatedOn", DateTime.UtcNow);

            return await cmd.ExecuteNonQueryAsync() > 0;
        }
        public async Task<IEnumerable<CourseEnrollmentGroupResponse>> GetAllCourseGroups(Guid courseId, bool isActive)
        {
            var results = new List<CourseEnrollmentGroupResponse>();
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                SELECT * 
                FROM course_enrollment_groups 
                WHERE CourseId = @CourseId";
            cmd.AddParameter("@CourseId", courseId.ToByteArray());

            if (isActive)
            {
                cmd.AddParameter("@IsActive", isActive);
                cmd.CommandText += " AND IsActive = TRUE";
            }


            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(MapToGroupResponse(reader));
            }

            return results;
        }

        // Get a specific group by ID
        public async Task<CourseEnrollmentGroupResponse> GetCourseGroup(Guid groupId)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"SELECT * FROM course_enrollment_groups WHERE CourseEnrollmentGroupId = @GroupId";
            cmd.AddParameter("@GroupId", groupId.ToByteArray());

            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                return null;

            return MapToGroupResponse(reader);
        }

        // Set registration open/close for a specific group
        public async Task<CourseEnrollmentGroupResponse> SetCourseGroupRegistrationStatus(Guid groupId, bool ifRegistrationOpen)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                UPDATE course_enrollment_groups
                SET IfRegistrationOpen = @IfRegistrationOpen, UpdatedOn = @UpdatedOn
                WHERE CourseEnrollmentGroupId = @GroupId";
            cmd.AddParameter("@GroupId", groupId.ToByteArray());
            cmd.AddParameter("@IfRegistrationOpen", ifRegistrationOpen);
            cmd.AddParameter("@UpdatedOn", DateTime.UtcNow);

            await cmd.ExecuteNonQueryAsync();

            // Return updated group
            return await GetCourseGroup(groupId);
        }


        private CourseEnrollmentGroupResponse MapToGroupResponse(dynamic reader)
        {
            return new CourseEnrollmentGroupResponse
            {
                CourseEnrollmentGroupId = reader.GetGuidFromByteArray("CourseEnrollmentGroupId"),
                CourseId = reader.GetGuidFromByteArray("CourseId"),
                InstituteId = reader.GetGuidFromByteArray("InstituteId"),
                GroupTitle = reader.GetString("GroupTitle"),
                GroupTitleFr = reader.GetString("GroupTitleFr"),
                Details = reader.IsDBNull("Details") ? null : reader.GetString("Details"),
                DetailsFr = reader.IsDBNull("DetailsFr") ? null : reader.GetString("DetailsFr"),
                IsActive = reader.GetBoolean("IsActive"),
                CreatedAt = reader.GetDateTime("CreatedAt"),
                UpdatedOn = reader.GetDateTime("UpdatedOn"),
                MaxStudents = reader.IsDBNull("MaxStudents") ? null : reader.GetInt32("MaxStudents"),
                AcedemicGroups = AcedemicGroupHelper.FromInt( reader.GetInt32("AcedemicGroup")),
                Fee = reader.GetInt32("Fee"),
                IfRegistrationOpen = reader.GetBoolean("IfRegistrationOpen")
            };
        }
    }
}