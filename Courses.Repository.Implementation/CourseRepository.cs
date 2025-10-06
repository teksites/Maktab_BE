using Cumulus.Data;
using Data;
using MaktabDataContracts.Helpers;
using MaktabDataContracts.Requests.Course;
using MaktabDataContracts.Responses.Course;
using System.Data.Common;
using System.Text;

namespace Courses.Repository.Implementation
{
    public class CourseRepository : DbRepository, ICourseRepository
    {
        public CourseRepository(IDatabase database) : base(database) { }

        // Add a new course
        public async Task<CourseResponseDetailed> AddCourse(AddCourse course)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            var courseId = Guid.NewGuid();

            cmd.CommandText = @"
                INSERT INTO courses
                (CourseId, InstituteId, Name, NameFr, Description, DescriptionFr,
                 Details, DetailsFr, StartDate, EndDate, IsActive,
                 CreatedAt, UpdatedOn, CanSelectMultipleEnrollmentGroups,
                 PolicyHyperLink, IsCourseCompleted, IsRegistrationOpened)
                VALUES
                (@CourseId, @InstituteId, @Name, @NameFr, @Description, @DescriptionFr,
                 @Details, @DetailsFr, @StartDate, @EndDate, @IsActive,
                 @CreatedAt, @UpdatedOn, @CanSelectMultipleEnrollmentGroups,
                 @PolicyHyperLink, @IsCourseCompleted, @IsRegistrationOpened)";

            cmd.AddParameter("@CourseId", courseId.ToByteArray());
            cmd.AddParameter("@InstituteId", course.InstituteId.ToByteArray());
            cmd.AddParameter("@Name", course.Name);
            cmd.AddParameter("@NameFr", course.NameFr);
            cmd.AddParameter("@Description", (object?)course.Description ?? DBNull.Value);
            cmd.AddParameter("@DescriptionFr", (object?)course.DescriptionFr ?? DBNull.Value);
            cmd.AddParameter("@Details", (object?)course.Details ?? DBNull.Value);
            cmd.AddParameter("@DetailsFr", (object?)course.DetailsFr ?? DBNull.Value);
            cmd.AddParameter("@StartDate", course.StartDate);
            cmd.AddParameter("@EndDate", course.EndDate);
            cmd.AddParameter("@IsActive", course.IsActive);
            cmd.AddParameter("@CreatedAt", DateTime.UtcNow);
            cmd.AddParameter("@UpdatedOn", DateTime.UtcNow);
            cmd.AddParameter("@CanSelectMultipleEnrollmentGroups", course.CanSelectMultipleEnrollmentGroups);
            cmd.AddParameter("@PolicyHyperLink", (object?)course.PolicyHyperLink ?? DBNull.Value);
            cmd.AddParameter("@IsCourseCompleted", course.IsCourseCompleted);
            cmd.AddParameter("@IsRegistrationOpened", course.IsRegistrationOpened);

            await cmd.ExecuteNonQueryAsync();
            return await GetCourse(courseId);
        }

        // Get course by Id
        public async Task<CourseResponseDetailed> GetCourse(Guid courseId)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = "SELECT * FROM courses WHERE CourseId = @CourseId";
            cmd.AddParameter("@CourseId", courseId.ToByteArray());

            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return null;

            return await MapToCourseResponse(reader);
        }

        // Get all courses with options
        public async Task<IEnumerable<CourseResponseDetailed>> GetAllCourses(GetCourseOptions options)
        {
            var results = new List<CourseResponseDetailed>();
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            var sql = new StringBuilder("SELECT * FROM courses WHERE 1=1");

            if (options.IsActive.HasValue)
            {
                sql.Append(" AND IsActive = @IsActive");
                cmd.AddParameter("@IsActive", options.IsActive.Value);
            }

            if (options.InstituteIds?.Any() == true)
            {
                sql.Append(" AND InstituteId IN (");
                for (int i = 0; i < options.InstituteIds.Count; i++)
                {
                    var param = $"@InstituteId{i}";
                    sql.Append(param);
                    if (i < options.InstituteIds.Count - 1) sql.Append(", ");
                    cmd.AddParameter(param, options.InstituteIds[i].ToByteArray());
                }
                sql.Append(")");
            }

            if (options.OfferedFromDate.HasValue)
            {
                sql.Append(" AND StartDate >= @OfferedFromDate");
                cmd.AddParameter("@OfferedFromDate", options.OfferedFromDate.Value);
            }

            if (options.OfferedToDate.HasValue)
            {
                sql.Append(" AND EndDate <= @OfferedToDate");
                cmd.AddParameter("@OfferedToDate", options.OfferedToDate.Value);
            }

            if (options.AcedemicGroups?.Any() == true)
            {
                int groupMask = (int)AcedemicGroupHelper.FromStrings(options.AcedemicGroups);
                sql.Append(@" AND CourseId IN (
                                SELECT DISTINCT CourseId
                                FROM course_enrollment_groups
                                WHERE AcedemicGroup & @AcedemicGroupMask > 0
                             )");
                cmd.AddParameter("@AcedemicGroupMask", groupMask);
            }

            cmd.CommandText = sql.ToString();

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(await MapToCourseResponse(reader));
            }

            return results;
        }

        // Overload to get all courses (onlyActive default)
        public async Task<IEnumerable<CourseResponseDetailed>> GetAllCourses(bool onlyActive = true)
        {
            var options = new GetCourseOptions
            {
                IsActive = onlyActive
            };
            return await GetAllCourses(options);
        }

        // Update course
        public async Task<bool> UpdateCourse(Guid courseId, AddCourse course)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                UPDATE courses
                SET Name=@Name, NameFr=@NameFr, Description=@Description, DescriptionFr=@DescriptionFr,
                    Details=@Details, DetailsFr=@DetailsFr, StartDate=@StartDate, EndDate=@EndDate,
                    IsActive=@IsActive, UpdatedOn=@UpdatedOn, CanSelectMultipleEnrollmentGroups=@CanSelectMultipleEnrollmentGroups,
                    PolicyHyperLink=@PolicyHyperLink, IsCourseCompleted=@IsCourseCompleted, IsRegistrationOpened=@IsRegistrationOpened
                WHERE CourseId=@CourseId";

            cmd.AddParameter("@CourseId", courseId.ToByteArray());
            cmd.AddParameter("@Name", course.Name);
            cmd.AddParameter("@NameFr", course.NameFr);
            cmd.AddParameter("@Description", (object?)course.Description ?? DBNull.Value);
            cmd.AddParameter("@DescriptionFr", (object?)course.DescriptionFr ?? DBNull.Value);
            cmd.AddParameter("@Details", (object?)course.Details ?? DBNull.Value);
            cmd.AddParameter("@DetailsFr", (object?)course.DetailsFr ?? DBNull.Value);
            cmd.AddParameter("@StartDate", course.StartDate);
            cmd.AddParameter("@EndDate", course.EndDate);
            cmd.AddParameter("@IsActive", course.IsActive);
            cmd.AddParameter("@UpdatedOn", DateTime.UtcNow);
            cmd.AddParameter("@CanSelectMultipleEnrollmentGroups", course.CanSelectMultipleEnrollmentGroups);
            cmd.AddParameter("@PolicyHyperLink", (object?)course.PolicyHyperLink ?? DBNull.Value);
            cmd.AddParameter("@IsCourseCompleted", course.IsCourseCompleted);
            cmd.AddParameter("@IsRegistrationOpened", course.IsRegistrationOpened);

            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        // Delete course
        public async Task<bool> DeleteCourse(Guid courseId, bool hardDelete = false)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            if (hardDelete)
            {
                cmd.CommandText = "DELETE FROM courses WHERE CourseId=@CourseId";
            }
            else
            {
                cmd.CommandText = "UPDATE courses SET IsActive=0, UpdatedOn=@UpdatedOn WHERE CourseId=@CourseId";
                cmd.AddParameter("@UpdatedOn", DateTime.UtcNow);
            }

            cmd.AddParameter("@CourseId", courseId.ToByteArray());
            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        // Set course registration status
        public async Task<CourseResponseDetailed> SetCourseRegistrationStatus(Guid courseId, bool ifRegistrationOpen)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                UPDATE courses
                SET IsRegistrationOpened=@IsRegistrationOpened, UpdatedOn=@UpdatedOn
                WHERE CourseId=@CourseId";

            cmd.AddParameter("@CourseId", courseId.ToByteArray());
            cmd.AddParameter("@IsRegistrationOpened", ifRegistrationOpen);
            cmd.AddParameter("@UpdatedOn", DateTime.UtcNow);

            await cmd.ExecuteNonQueryAsync();
            return await GetCourse(courseId);
        }

        // Map DbDataReader to CourseResponseDetailed
        private async Task<CourseResponseDetailed> MapToCourseResponse(DbDataReader reader)
        {
            var courseId = reader.GetGuidFromByteArray("CourseId");
            var course = new CourseResponseDetailed
            {
                CourseId = courseId,
                InstituteId = reader.GetGuidFromByteArray("InstituteId"),
                Name = reader.GetString("Name"),
                NameFr = reader.GetString("NameFr"),
                Description = reader.IsDBNull("Description") ? string.Empty : reader.GetString("Description"),
                DescriptionFr = reader.IsDBNull("DescriptionFr") ? string.Empty : reader.GetString("DescriptionFr"),
                Details = reader.IsDBNull("Details") ? string.Empty : reader.GetString("Details"),
                DetailsFr = reader.IsDBNull("DetailsFr") ? string.Empty : reader.GetString("DetailsFr"),
                StartDate = reader.GetDateTime("StartDate"),
                EndDate = reader.GetDateTime("EndDate"),
                IsActive = reader.GetBoolean("IsActive"),
                CanSelectMultipleEnrollmentGroups = reader.GetBoolean("CanSelectMultipleEnrollmentGroups"),
                CreatedAt = reader.GetDateTime("CreatedAt"),
                UpdatedOn = reader.GetDateTime("UpdatedOn"),
                PolicyHyperLink = reader.IsDBNull("PolicyHyperLink") ? string.Empty : reader.GetString("PolicyHyperLink"),
                IsCourseCompleted = reader.GetBoolean("IsCourseCompleted"),
                IsRegistrationOpened = reader.GetBoolean("IsRegistrationOpened")
            };

            // Load enrollment groups
            course.CourseEnrollmentGroups = (await new CourseEnrollmentGroupRepository(Database)
                                              .GetAllGroups(courseId, true)).ToList();

            // Merge unique academic groups
            course.AcedemicGroups = course.CourseEnrollmentGroups
                                          .SelectMany(g => g.AcedemicGroups)
                                          .Distinct()
                                          .ToList();

            return course;
        }
    }
}
