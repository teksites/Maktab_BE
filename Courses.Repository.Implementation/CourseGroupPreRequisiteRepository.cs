using Cumulus.Data;
using Data;
using System.Data.Common;
using AddCourseGroupPreRequisite = MaktabDataContracts.Requests.Course.AddCourseGroupPreRequisite;
using CourseGroupPreRequisiteResponse = MaktabDataContracts.Responses.Course.CourseGroupPreRequisiteResponse;

namespace Courses.Repository.Implementation
{
    public class CourseGroupPreRequisiteRepository : DbRepository, ICourseGroupPreRequisiteRepository
    {
        public CourseGroupPreRequisiteRepository(IDatabase database) : base(database)
        {
        }

        public async Task<CourseGroupPreRequisiteResponse> Add(AddCourseGroupPreRequisite preRequisite)
        {
            ArgumentNullException.ThrowIfNull(preRequisite);

            var id = preRequisite.CourseGroupPreRequisiteId == Guid.Empty
                ? Guid.NewGuid()
                : preRequisite.CourseGroupPreRequisiteId;

            using var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false);
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                INSERT INTO course_group_prerequisites
                (
                    CourseGroupPreRequisiteId,
                    CourseGroupId,
                    PreRequisiteCourseGroupId,
                    IsActive,
                    CreatedAt,
                    UpdatedOn
                )
                VALUES
                (
                    @CourseGroupPreRequisiteId,
                    @CourseGroupId,
                    @PreRequisiteCourseGroupId,
                    @IsActive,
                    @CreatedAt,
                    @UpdatedOn
                )";

            var now = DateTime.UtcNow;
            cmd.AddParameter("@CourseGroupPreRequisiteId", id.ToByteArray());
            cmd.AddParameter("@CourseGroupId", preRequisite.CourseGroupId.ToByteArray());
            cmd.AddParameter("@PreRequisiteCourseGroupId", preRequisite.PreRequisiteCourseGroupId.ToByteArray());
            cmd.AddParameter("@IsActive", true);
            cmd.AddParameter("@CreatedAt", now);
            cmd.AddParameter("@UpdatedOn", now);

            await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);

            return await Get(id).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Failed to retrieve created course group prerequisite.");
        }

        public async Task<CourseGroupPreRequisiteResponse?> Get(Guid courseGroupPreRequisiteId)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false);
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                SELECT *
                FROM course_group_prerequisites
                WHERE CourseGroupPreRequisiteId = @CourseGroupPreRequisiteId";
            cmd.AddParameter("@CourseGroupPreRequisiteId", courseGroupPreRequisiteId.ToByteArray());

            using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
            if (!await reader.ReadAsync().ConfigureAwait(false))
            {
                return null;
            }

            return Map(reader);
        }

        public async Task<IEnumerable<CourseGroupPreRequisiteResponse>> GetByCourseGroup(Guid courseGroupId, bool onlyActive = true)
        {
            var results = new List<CourseGroupPreRequisiteResponse>();

            using var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false);
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                SELECT *
                FROM course_group_prerequisites
                WHERE CourseGroupId = @CourseGroupId";

            cmd.AddParameter("@CourseGroupId", courseGroupId.ToByteArray());

            if (onlyActive)
            {
                cmd.CommandText += " AND IsActive = TRUE";
            }

            cmd.CommandText += " ORDER BY CreatedAt ASC";

            using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                results.Add(Map(reader));
            }

            return results;
        }

        public async Task<bool> Delete(Guid courseGroupPreRequisiteId, bool hardDelete = false)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false);
            using var cmd = conn.CreateCommand();

            if (hardDelete)
            {
                cmd.CommandText = @"
                    DELETE FROM course_group_prerequisites
                    WHERE CourseGroupPreRequisiteId = @CourseGroupPreRequisiteId";
            }
            else
            {
                cmd.CommandText = @"
                    UPDATE course_group_prerequisites
                    SET IsActive = FALSE,
                        UpdatedOn = @UpdatedOn
                    WHERE CourseGroupPreRequisiteId = @CourseGroupPreRequisiteId";
                cmd.AddParameter("@UpdatedOn", DateTime.UtcNow);
            }

            cmd.AddParameter("@CourseGroupPreRequisiteId", courseGroupPreRequisiteId.ToByteArray());
            return await cmd.ExecuteNonQueryAsync().ConfigureAwait(false) > 0;
        }

        private static CourseGroupPreRequisiteResponse Map(DbDataReader reader)
        {
            return new CourseGroupPreRequisiteResponse
            {
                CourseGroupPreRequisiteId = reader.GetGuidFromByteArray("CourseGroupPreRequisiteId"),
                CourseGroupId = reader.GetGuidFromByteArray("CourseGroupId"),
                PreRequisiteCourseGroupId = reader.GetGuidFromByteArray("PreRequisiteCourseGroupId"),
                IsActive = reader.GetBoolean("IsActive"),
                CreatedAt = reader.GetDateTimeUtc("CreatedAt"),
                UpdatedOn = reader.GetDateTimeUtc("UpdatedOn")
            };
        }
    }
}
