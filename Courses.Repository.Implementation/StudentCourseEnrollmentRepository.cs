using Cumulus.Data;
using Data;
using MaktabDataContracts.Enums;
using MaktabDataContracts.Requests.Course;
using MaktabDataContracts.Responses.Course;
using System.Collections.Generic;
using System.Data.Common;
using System.Runtime.Intrinsics.X86;
using System.Text;

namespace Courses.Repository.Implementation
{
    public class StudentCourseEnrollmentRepository : DbRepository, IStudentCourseEnrollmentRepository
    {
        public StudentCourseEnrollmentRepository(IDatabase database) : base(database) { }

        // Add a new enrollment
        public async Task<StudentCourseEnrollmentResponse> AddEnrollment(AddStudentCourseEnrollment enrollment)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            var enrollmentId = Guid.NewGuid();
            cmd.CommandText = @"
                INSERT INTO student_course_enrollment 
                (StudentCourseEnrollmentId, CourseEnrollmentGroupId, CourseId, ChildId, FamilyId, 
                 WillUseDayCare, DayCareDays, IsActive, CreatedAt, UpdatedOn)
                VALUES 
                (@StudentCourseEnrollmentId, @CourseEnrollmentGroupId, @CourseId, @ChildId, @FamilyId, 
                 @WillUseDayCare, @DayCareDays, @IsActive, @CreatedAt, @UpdatedOn)";

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
            return await GetEnrollment(enrollmentId) ?? throw new Exception("Failed to retrieve created enrollment");
        }

        // Get a single enrollment by ID
        public async Task<StudentCourseEnrollmentResponse?> GetEnrollment(Guid enrollmentId)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"SELECT * FROM student_course_enrollment WHERE StudentCourseEnrollmentId=@EnrollmentId";
            cmd.AddParameter("@EnrollmentId", enrollmentId.ToByteArray());

            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return null;
            return MapToEnrollmentResponse(reader);
        }
/*
        // Get all enrollments for a specific course
        public async Task<IEnumerable<StudentCourseEnrollmentResponse>> GetAllEnrollmentsByCourse(Guid courseId)
        {
            var results = new List<StudentCourseEnrollmentResponse>();
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"SELECT * FROM student_course_enrollment WHERE CourseId=@CourseId AND IsActive=TRUE";
            cmd.AddParameter("@CourseId", courseId.ToByteArray());

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(MapToEnrollmentResponse(reader));
            }

            return results;
        }

        // Get all enrollments for a specific family
        public async Task<IEnumerable<StudentCourseEnrollmentResponse>> GetAllEnrollmentsByFamily(Guid familyId)
        {
            var results = new List<StudentCourseEnrollmentResponse>();
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"SELECT * FROM student_course_enrollment WHERE FamilyId=@FamilyId AND IsActive=TRUE";
            cmd.AddParameter("@FamilyId", familyId.ToByteArray());

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(MapToEnrollmentResponse(reader));
            }

            return results;
        }

*/        // Update an existing enrollment
        public async Task<bool> UpdateEnrollment(Guid enrollmentId, AddStudentCourseEnrollment enrollment)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                UPDATE student_course_enrollment 
                SET CourseEnrollmentGroupId=@CourseEnrollmentGroupId,
                    CourseId=@CourseId,
                    ChildId=@ChildId,
                    FamilyId=@FamilyId,
                    WillUseDayCare=@WillUseDayCare,
                    DayCareDays=@DayCareDays,
                    UpdatedOn=@UpdatedOn
                WHERE StudentCourseEnrollmentId=@EnrollmentId";

            cmd.AddParameter("@EnrollmentId", enrollmentId.ToByteArray());
            cmd.AddParameter("@CourseEnrollmentGroupId", enrollment.CourseEnrollmentGroupId.ToByteArray());
            cmd.AddParameter("@CourseId", enrollment.CourseId.ToByteArray());
            cmd.AddParameter("@ChildId", enrollment.ChildId.ToByteArray());
            cmd.AddParameter("@FamilyId", enrollment.FamilyId.ToByteArray());
            cmd.AddParameter("@WillUseDayCare", enrollment.WillUseDayCare);
            cmd.AddParameter("@DayCareDays", enrollment.DayCareDays);
            cmd.AddParameter("@UpdatedOn", DateTime.UtcNow);

            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        // Delete an enrollment (soft or hard delete)
        public async Task<bool> DeleteEnrollment(Guid enrollmentId, bool hardDelete = false)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            if (hardDelete)
                cmd.CommandText = @"DELETE FROM student_course_enrollment WHERE StudentCourseEnrollmentId=@EnrollmentId";
            else
                cmd.CommandText = @"UPDATE student_course_enrollment SET IsActive=FALSE, UpdatedOn=@UpdatedOn WHERE StudentCourseEnrollmentId=@EnrollmentId";

            cmd.AddParameter("@EnrollmentId", enrollmentId.ToByteArray());
            if (!hardDelete)
                cmd.AddParameter("@UpdatedOn", DateTime.UtcNow);

            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        public async Task<IEnumerable<StudentCourseEnrollmentResponse>> GetAllEnrollmentsByGroup(Guid groupId)
        {
            var results = new List<StudentCourseEnrollmentResponse>();
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"SELECT * FROM student_course_enrollment WHERE CourseEnrollmentGroupId=@GroupId AND IsActive=TRUE";
            cmd.AddParameter("@GroupId", groupId.ToByteArray());

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(MapToEnrollmentResponse(reader));
            }

            return results;
        }

        public async Task<IEnumerable<CourseSessionInfoResponse>> GetFamilyCourseSessionInfo(Guid familyId, Guid? instituteId)
        {
            var results = new List<CourseSessionInfoResponse>();

            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            var sql = new StringBuilder(@"
        SELECT
            sce.FamilyId AS FamilyId,
            c.InstituteId AS InstituteId,
            c.CourseId AS CourseId,
            c.CourseSession AS CourseSession,
            c.IsCourseCompleted AS IsCourseCompleted,
            c.IsRegistrationOpened AS IsRegistrationOpened,
            c.RegistrationStartDate AS RegistrationStartDate,
            c.RegistrationEndDate AS RegistrationEndDate,
            c.IsActive AS CourseActive,
            ceg.IsActive AS EnrollmentGroupActive
        FROM courses c
        INNER JOIN course_enrollment_groups ceg
            ON c.CourseId = ceg.CourseId
        INNER JOIN student_course_enrollment sce
            ON ceg.CourseEnrollmentGroupId = sce.CourseEnrollmentGroupId
        WHERE sce.FamilyId = @FamilyId");

            cmd.AddParameter("@FamilyId", familyId.ToByteArray());

            // Only add InstituteId filter if provided
            if (instituteId.HasValue)
            {
                sql.Append(" AND c.InstituteId = @InstituteId");
                cmd.AddParameter("@InstituteId", instituteId.Value.ToByteArray());
            }

            sql.Append(" ORDER BY c.CreatedAt DESC");

            cmd.CommandText = sql.ToString();

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(MapToCourseSessionInfoResponse(reader));
            }

            return results;
        }

        public Task<IEnumerable<StudentCourseEnrollmentResponse>> GetAllEnrollmentsByCourse(Guid courseId)
        { 
            return GetEnrollmentsByColumnAsync("CourseId", courseId);
        }
        public Task<IEnumerable<StudentCourseEnrollmentResponse>> GetAllEnrollmentsByFamily(Guid familyId)
        { 
            return GetEnrollmentsByColumnAsync("FamilyId", familyId);
        }

        private async Task<IEnumerable<StudentCourseEnrollmentResponse>> GetEnrollmentsByColumnAsync(string columnName, Guid value)
        {
            var results = new List<StudentCourseEnrollmentResponse>();
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = $"SELECT * FROM student_course_enrollment WHERE {columnName}=@Value AND IsActive=TRUE";
            cmd.AddParameter("@Value", value.ToByteArray());

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                results.Add(MapToEnrollmentResponse(reader));

            return results;
        }


        // Helper: Map DbDataReader to StudentCourseEnrollmentResponse
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

        private CourseSessionInfoResponse MapToCourseSessionInfoResponse(DbDataReader reader)
        {
            return new CourseSessionInfoResponse
            {
                FamilyId = reader.GetGuidFromByteArray("FamilyId"),
                InstituteId = reader.GetGuidFromByteArray("InstituteId"),
                CourseId = reader.GetGuidFromByteArray("CourseId"),

                CourseSession = (CourseSessionType)reader.GetByte("CourseSession"),
                IsCourseCompleted = reader.GetBoolean("IsCourseCompleted"),
                IsRegistrationOpened = reader.GetBoolean("IsRegistrationOpened"),

                RegistrationStartDate = reader.IsDBNull(reader.GetOrdinal("RegistrationStartDate"))
                    ? null
                    : reader.GetDateTime("RegistrationStartDate"),

                RegistrationEndDate = reader.IsDBNull(reader.GetOrdinal("RegistrationEndDate"))
                    ? null
                    : reader.GetDateTime("RegistrationEndDate"),

                CourseActive = reader.GetBoolean("CourseActive"),
                EnrollmentGroupActive = reader.GetBoolean("EnrollmentGroupActive")
            };
        }

    }
}
