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

            var courseId = Guid.NewGuid();
            cmd.CommandText = @"
                INSERT INTO courses 
                (CourseId, InstituteId, AddressId, Name, NameFr, Description, DescriptionFr, Details, DetailsFr, StartDate, EndDate, IsActive, CreatedAt, UpdatedOn, CanSelectMultipleEnrollmentGroups, PolicyHyperLink, IsCourseCompleted, IsRegistrationOpened)
                VALUES 
                (@CourseId, @InstituteId, @AddressId, @Name, @NameFr, @Description, @DescriptionFr, @Details, @DetailsFr, @StartDate, @EndDate, @IsActive, @CreatedAt, @UpdatedOn, @CanSelectMultipleEnrollmentGroups, @PolicyHyperLink, @IsCourseCompleted, @IsRegistrationOpened)";

            cmd.AddParameter("@CourseId", courseId.ToByteArray());
            cmd.AddParameter("@InstituteId", course.InstituteId.ToByteArray());
            cmd.AddParameter("@AddressId", course.AddressId.HasValue ? course.AddressId.Value.ToByteArray() : (object)DBNull.Value);
            cmd.AddParameter("@Name", course.Name);
            cmd.AddParameter("@NameFr", course.NameFr);
            cmd.AddParameter("@Description", course.Description);
            cmd.AddParameter("@DescriptionFr", course.DescriptionFr);
            cmd.AddParameter("@Details", course.Details);
            cmd.AddParameter("@DetailsFr", course.DetailsFr);
            cmd.AddParameter("@StartDate", course.StartDate);
            cmd.AddParameter("@EndDate", course.EndDate);
            cmd.AddParameter("@IsActive", course.IsActive);
            cmd.AddParameter("@CreatedAt", DateTime.UtcNow);
            cmd.AddParameter("@UpdatedOn", DateTime.UtcNow);
            cmd.AddParameter("@CanSelectMultipleEnrollmentGroups", course.CanSelectMultipleEnrollmentGroups);
            cmd.AddParameter("@PolicyHyperLink", course.PolicyHyperLink);
            cmd.AddParameter("@IsCourseCompleted", course.IsCourseCompleted);
            cmd.AddParameter("@IsRegistrationOpened", course.IsRegistrationOpened);

            await cmd.ExecuteNonQueryAsync();
            return await GetCourse(courseId);
        }

        public async Task<bool> UpdateCourse(Guid courseId, AddCourse course)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                UPDATE courses
                SET InstituteId = @InstituteId,
                    AddressId = @AddressId,
                    Name = @Name,
                    NameFr = @NameFr,
                    Description = @Description,
                    DescriptionFr = @DescriptionFr,
                    Details = @Details,
                    DetailsFr = @DetailsFr,
                    StartDate = @StartDate,
                    EndDate = @EndDate,
                    UpdatedOn = @UpdatedOn,
                    CanSelectMultipleEnrollmentGroups = @CanSelectMultipleEnrollmentGroups,
                    PolicyHyperLink = @PolicyHyperLink,
                    IsRegistrationOpened = @IsRegistrationOpened,
                    IsActive = @IsActive,
                    IsCourseCompleted = @IsCourseCompleted
                WHERE CourseId = @CourseId";

            cmd.AddParameter("@CourseId", courseId.ToByteArray());
            cmd.AddParameter("@InstituteId", course.InstituteId.ToByteArray());
            cmd.AddParameter("@AddressId", course.AddressId.HasValue ? course.AddressId.Value.ToByteArray() : (object)DBNull.Value);
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
            cmd.AddParameter("@IsActive", course.IsActive);
            cmd.AddParameter("@IsCourseCompleted", course.IsCourseCompleted);

            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        private CourseResponseDetailed MapToCourseResponse(DbDataReader reader)
        {
            return new CourseResponseDetailed
            {
                CourseId = reader.GetGuidFromByteArray("CourseId"),
                InstituteId = reader.GetGuidFromByteArray("InstituteId"),
                AddressId = reader.IsDBNull(reader.GetOrdinal("AddressId")) ? (Guid?)null : reader.GetGuidFromByteArray("AddressId"),
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
                IsRegistrationOpened = reader.GetBoolean("IsRegistrationOpened")
            };
        }
    }
}
