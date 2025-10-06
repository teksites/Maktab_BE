using Cumulus.Data;
using Data;
using MaktabDataContracts.Requests.Course;
using MaktabDataContracts.Responses.Course;
using System.Data.Common;

namespace Courses.Repository.Implementation
{
    public class StudentCourseEnrollmentRepository : DbRepository, IStudentCourseEnrollmentRepository
    {
        public StudentCourseEnrollmentRepository(IDatabase database) : base(database) { }

        public async Task<StudentCourseEnrollmentResponse> AddEnrollment(AddStudentCourseEnrollment enrollment)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
            INSERT INTO student_course_enrollment 
            (StudentCourseEnrollmentId, CourseEnrollmentGroupId, CourseId, ChildId, FamilyId, 
             WillUseDayCare, DayCareDays, IsActive, CreatedAt, UpdatedOn)
            VALUES 
            (@StudentCourseEnrollmentId, @CourseEnrollmentGroupId, @CourseId, @ChildId, @FamilyId, 
             @WillUseDayCare, @DayCareDays, @IsActive, @CreatedAt, @UpdatedOn)";

            var enrollmentId = Guid.NewGuid();
            cmd.AddParameter("@StudentCourseEnrollmentId", enrollmentId.ToByteArray());
            cmd.AddParameter("@CourseEnrollmentGroupId", enrollment.CourseEnrollmentGroupId.ToByteArray());
            cmd.AddParameter("@CourseId", enrollment.CourseId.ToByteArray());
            cmd.AddParameter("@ChildId", enrollment.ChildId.ToByteArray());
            cmd.AddParameter("@FamilyId", enrollment.FamilyId.ToByteArray());
            cmd.AddParameter("@WillUseDayCare", enrollment.WillUseDayCare);
            cmd.AddParameter("@DayCareDays", enrollment.DayCareDays);
            cmd.AddParameter("@IsActive", enrollment.IsActive);
            cmd.AddParameter("@CreatedAt", DateTime.UtcNow);
            cmd.AddParameter("@UpdatedOn", DateTime.UtcNow);

            await cmd.ExecuteNonQueryAsync();
            return await GetEnrollment(enrollmentId);
        }

        public async Task<StudentCourseEnrollmentResponse> GetEnrollment(Guid enrollmentId)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"SELECT * FROM student_course_enrollment WHERE StudentCourseEnrollmentId = @StudentCourseEnrollmentId";
            cmd.AddParameter("@StudentCourseEnrollmentId", enrollmentId.ToByteArray());

            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return null;
            return MapToEnrollmentResponse(reader);
        }

        public async Task<IEnumerable<StudentCourseEnrollmentResponse>> GetAllEnrollments(Guid courseId)
        {
            var results = new List<StudentCourseEnrollmentResponse>();
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"SELECT * FROM student_course_enrollment WHERE CourseId = @CourseId AND IsActive = TRUE";
            cmd.AddParameter("@CourseId", courseId.ToByteArray());

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(MapToEnrollmentResponse(reader));
            }
            return results;
        }

        public async Task<bool> UpdateEnrollment(Guid enrollmentId, AddStudentCourseEnrollment enrollment)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
            UPDATE student_course_enrollment 
            SET CourseEnrollmentGroupId = @CourseEnrollmentGroupId,
                CourseId = @CourseId,
                ChildId = @ChildId,
                FamilyId = @FamilyId,
                WillUseDayCare = @WillUseDayCare,
                DayCareDays = @DayCareDays,
                UpdatedOn = @UpdatedOn
            WHERE StudentCourseEnrollmentId = @StudentCourseEnrollmentId";

            cmd.AddParameter("@StudentCourseEnrollmentId", enrollmentId.ToByteArray());
            cmd.AddParameter("@CourseEnrollmentGroupId", enrollment.CourseEnrollmentGroupId.ToByteArray());
            cmd.AddParameter("@CourseId", enrollment.CourseId.ToByteArray());
            cmd.AddParameter("@ChildId", enrollment.ChildId.ToByteArray());
            cmd.AddParameter("@FamilyId", enrollment.FamilyId.ToByteArray());
            cmd.AddParameter("@WillUseDayCare", enrollment.WillUseDayCare);
            cmd.AddParameter("@DayCareDays", enrollment.DayCareDays);
            cmd.AddParameter("@UpdatedOn", DateTime.UtcNow);

            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        public async Task<bool> DeleteEnrollment(Guid enrollmentId, bool hardDelete = false)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            if (hardDelete)
                cmd.CommandText = @"DELETE FROM student_course_enrollment WHERE StudentCourseEnrollmentId = @StudentCourseEnrollmentId";
            else
                cmd.CommandText = @"UPDATE student_course_enrollment SET IsActive = FALSE, UpdatedOn = @UpdatedOn WHERE StudentCourseEnrollmentId = @StudentCourseEnrollmentId";

            cmd.AddParameter("@StudentCourseEnrollmentId", enrollmentId.ToByteArray());
            if (!hardDelete)
                cmd.AddParameter("@UpdatedOn", DateTime.UtcNow);

            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        private StudentCourseEnrollmentResponse MapToEnrollmentResponse(DbDataReader reader)
        {
            return new StudentCourseEnrollmentResponse
            {
                StudentCourseEnrollmentId = reader.GetGuidFromByteArray("StudentCourseEnrollmentId"),
                CourseEnrollmentGroupId = reader.GetGuidFromByteArray("CourseEnrollmentGroupId"),
                CourseId = reader.GetGuidFromByteArray("CourseId"),
                ChildId = reader.GetGuidFromByteArray("ChildId"),
                FamilyId = reader.GetGuidFromByteArray("FamilyId"),
                WillUseDayCare = reader.GetBoolean("WillUseDayCare"),
                DayCareDays = reader.GetInt32("DayCareDays"),
                IsActive = reader.GetBoolean("IsActive"),
                CreatedAt = reader.GetDateTime("CreatedAt"),
                UpdatedOn = reader.GetDateTime("UpdatedOn")
            };
        }
    }

}
