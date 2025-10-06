using Cumulus.Data;
using Data;
using MaktabDataContracts.Requests.Course;
using MaktabDataContracts.Responses.Course;
using System.Data.Common;
using System.Text;

namespace Courses.Repository.Implementation
{
    public class CourseRepository : DbRepository, ICourseRepository
    {
        public CourseRepository(IDatabase database) : base(database) { }

        public async Task<CourseResponseDetailed> AddCourse(AddCourse course)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO courses (CourseId, InstituteId, AddressId, Name, NameFr, Description, DescriptionFr, Details, DetailsFr, StartDate, EndDate, IsActive, CreatedAt, UpdatedOn, CanSelectMultipleEnrollmentGroups, PolicyHyperLink, IsCourseCompleted, IsRegistrationOpened)
                            VALUES (@CourseId, @InstituteId, @AddressId, @Name, @NameFr, @Description, @DescriptionFr, @Details, @DetailsFr, @StartDate, @EndDate, @IsActive, @CreatedAt, @UpdatedOn, @CanSelectMultipleEnrollmentGroups, @PolicyHyperLink, @IsCourseCompleted, @IsRegistrationOpened)";
            var courseId = Guid.NewGuid();
            cmd.AddParameter("@CourseId", courseId.ToByteArray());
            cmd.AddParameter("@InstituteId", course.InstituteId.ToByteArray());
            cmd.AddParameter("@AddressId", Guid.Empty.ToByteArray());
            cmd.AddParameter("@Name", course.Name);
            cmd.AddParameter("@NameFr", course.NameFr);
            cmd.AddParameter("@Description", course.Description);
            cmd.AddParameter("@DescriptionFr", course.DescriptionFr);
            cmd.AddParameter("@Details", course.Details);
            cmd.AddParameter("@DetailsFr", course.DetailsFr);
            cmd.AddParameter("@StartDate", course.StartDate);
            cmd.AddParameter("@EndDate", course.EndDate);
            cmd.AddParameter("@IsActive", true);
            cmd.AddParameter("@CreatedAt", DateTime.UtcNow);
            cmd.AddParameter("@UpdatedOn", DateTime.UtcNow);
            cmd.AddParameter("@CanSelectMultipleEnrollmentGroups", course.CanSelectMultipleEnrollmentGroups);
            cmd.AddParameter("@PolicyHyperLink", course.PolicyHyperLink);
            cmd.AddParameter("@IsCourseCompleted", false);
            cmd.AddParameter("@IsRegistrationOpened", true);
            await cmd.ExecuteNonQueryAsync();
            return await GetCourse(courseId);
        }

        public async Task<CourseResponseDetailed> GetCourse(Guid courseId)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"SELECT * FROM courses WHERE CourseId = @CourseId";
            cmd.AddParameter("@CourseId", courseId.ToByteArray());
            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return null;
            return MapToCourseResponse(reader);
        }

        public async Task<IEnumerable<CourseResponseDetailed>> GetAllCourses(bool onlyActive = true)
        {
            var results = new List<CourseResponseDetailed>();
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"SELECT * FROM courses";
            if (onlyActive) cmd.CommandText += " WHERE IsActive = TRUE";
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(MapToCourseResponse(reader));
            }
            return results;
        }

        public async Task<bool> UpdateCourse(Guid courseId, AddCourse course)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"UPDATE courses SET InstituteId = @InstituteId, AddressId = @AddressId, Name = @Name, NameFr = @NameFr, Description = @Description, DescriptionFr = @DescriptionFr, Details = @Details, DetailsFr = @DetailsFr, StartDate = @StartDate, EndDate = @EndDate, UpdatedOn = @UpdatedOn, CanSelectMultipleEnrollmentGroups = @CanSelectMultipleEnrollmentGroups, PolicyHyperLink = @PolicyHyperLink, @IsRegistrationOpened = IsRegistrationOpened WHERE CourseId = @CourseId";
            cmd.AddParameter("@CourseId", courseId.ToByteArray());
            cmd.AddParameter("@InstituteId", course.InstituteId.ToByteArray());
            cmd.AddParameter("@AddressId", Guid.Empty.ToByteArray());
            cmd.AddParameter("@Name", course.Name);
            cmd.AddParameter("@NameFr", course.NameFr);
            cmd.AddParameter("@Description", course.Description);
            cmd.AddParameter("@DescriptionFr", course.DescriptionFr);
            cmd.AddParameter("@Details", course.Details);
            cmd.AddParameter("@DetailsFr", course.DetailsFr);
            cmd.AddParameter("@StartDate", course.StartDate);
            cmd.AddParameter("@EndDate", course.EndDate);
            cmd.AddParameter("@UpdatedOn", DateTime.UtcNow);
            cmd.AddParameter("@CanSelectMultipleEnrollmentGroups", course.CanSelectMultipleEnrollmentGroups);
            cmd.AddParameter("@PolicyHyperLink", course.PolicyHyperLink);
            cmd.AddParameter("@IsRegistrationOpened", course.IsRegistrationOpened);
            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        public async Task<bool> DeleteCourse(Guid courseId, bool hardDelete = false)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();
            if (hardDelete)
                cmd.CommandText = @"DELETE FROM courses WHERE CourseId = @CourseId";
            else
                cmd.CommandText = @"UPDATE courses SET IsActive = FALSE WHERE CourseId = @CourseId";
            cmd.AddParameter("@CourseId", courseId.ToByteArray());
            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        public async Task<IEnumerable<CourseResponseDetailed>> GetAllCourses(GetCourseOptions options)
        {
            var results = new List<CourseResponseDetailed>();
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            var sql = new StringBuilder("SELECT * FROM courses WHERE 1=1");

            // Filter by IsActive
            if (options.IsActive.HasValue)
            {
                sql.Append(" AND IsActive = @IsActive");
                cmd.AddParameter("@IsActive", options.IsActive.Value);
            }

            // Filter by InstituteIds
            if (options.InstituteIds != null && options.InstituteIds.Any())
            {
                sql.Append(" AND InstituteId IN (");
                for (int i = 0; i < options.InstituteIds.Count; i++)
                {
                    var param = $"@InstituteId{i}";
                    sql.Append(param);
                    if (i < options.InstituteIds.Count - 1)
                        sql.Append(", ");
                    cmd.AddParameter(param, options.InstituteIds[i].ToByteArray());
                }
                sql.Append(")");
            }

            // Filter by AcedemicGroups
            if (options.AcedemicGroups != null && options.AcedemicGroups.Any())
            {
                // Assuming "AcedemicGroups" is stored in courses.Details or another column as JSON or CSV
                // Adjust column name accordingly. Here we check if any group exists in a JSON string column "Details"
                // You may need to change this based on your actual storage of AcedemicGroups
                sql.Append(" AND (");
                for (int i = 0; i < options.AcedemicGroups.Count; i++)
                {
                    var param = $"@Group{i}";
                    sql.Append($"JSON_CONTAINS(Details, '\"{options.AcedemicGroups[i]}\"')");
                    if (i < options.AcedemicGroups.Count - 1)
                        sql.Append(" OR ");
                }
                sql.Append(")");
            }

            // Filter by OfferedFromDate and OfferedToDate (both must be provided)
            if (options.OfferedFromDate.HasValue && options.OfferedToDate.HasValue)
            {
                sql.Append(" AND StartDate >= @OfferedFromDate AND EndDate <= @OfferedToDate");
                cmd.AddParameter("@OfferedFromDate", options.OfferedFromDate.Value);
                cmd.AddParameter("@OfferedToDate", options.OfferedToDate.Value);
            }

            cmd.CommandText = sql.ToString();

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(MapToCourseResponse(reader));
            }

            return results;
        }

        // Set registration open/close for all groups of a course
        public async Task<CourseResponseDetailed> SetCourseRegistrationStatus(Guid courseId, bool ifRegistrationOpen)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                UPDATE course_enrollment_groups
                SET IfRegistrationOpen = @IfRegistrationOpen, UpdatedOn = @UpdatedOn
                WHERE CourseId = @CourseId";
            cmd.AddParameter("@CourseId", courseId.ToByteArray());
            cmd.AddParameter("@IfRegistrationOpen", ifRegistrationOpen);
            cmd.AddParameter("@UpdatedOn", DateTime.UtcNow);

            await cmd.ExecuteNonQueryAsync();

            // Return updated course (just reading course info, not groups)
            using var courseCmd = conn.CreateCommand();
            courseCmd.CommandText = @"SELECT * FROM courses WHERE CourseId = @CourseId";
            courseCmd.AddParameter("@CourseId", courseId.ToByteArray());

            using var reader = await courseCmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                return null;

            return MapToCourseResponse(reader);
        }

        private CourseResponseDetailed MapToCourseResponse(DbDataReader reader)
        {
            return new CourseResponseDetailed
            {
                CourseId = reader.GetGuidFromByteArray("CourseId"),
                InstituteId = reader.GetGuidFromByteArray("InstituteId"),
                //AddressId = reader.IsDBNull(reader.GetOrdinal("AddressId")) ? (Guid?)null : reader.GetGuidFromByteArray("AddressId"),
                Name = reader.GetString("Name"),
                NameFr = reader.GetString("NameFr"),
                Description = reader.GetString("Description"),
                DescriptionFr = reader.GetString("DescriptionFr"),
                Details = reader.GetString("Details"),
                DetailsFr = reader.GetString("DetailsFr"),
                StartDate = reader.GetDateTime("StartDate"),
                EndDate = reader.GetDateTime("EndDate"),
                IsActive = reader.GetBoolean("IsActive"),
                CreatedAt = reader.GetDateTime("CreatedAt"),
                UpdatedOn = reader.GetDateTime("UpdatedOn"),
                CanSelectMultipleEnrollmentGroups = reader.GetBoolean("CanSelectMultipleEnrollmentGroups"),
                PolicyHyperLink = reader.GetString("PolicyHyperLink"),
                IsCourseCompleted = reader.GetBoolean("IsCourseCompleted"),
                IsRegistrationOpened = reader.GetBoolean("IsRegistrationOpened"),
            };
        }
    }
}
