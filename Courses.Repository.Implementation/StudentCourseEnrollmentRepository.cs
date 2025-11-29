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

        // Add a new enrollment
        public async Task<StudentCourseEnrollmentResponse> AddEnrollment(AddStudentCourseEnrollment enrollment)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            var enrollmentId = Guid.NewGuid();
            cmd.CommandText = @"
                INSERT INTO student_course_enrollment 
                (StudentCourseEnrollmentId, CourseEnrollmentGroupId, CourseId, ChildId, FamilyId, 
                 WillUseDayCare, DayCareDays, IsActive, CreatedAt, UpdatedOn, EnrollmentIndex)
                VALUES 
                (@StudentCourseEnrollmentId, @CourseEnrollmentGroupId, @CourseId, @ChildId, @FamilyId, 
                 @WillUseDayCare, @DayCareDays, @IsActive, @CreatedAt, @UpdatedOn, @EnrollmentIndex)";

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
            cmd.AddParameter("@EnrollmentIndex", enrollment.EnrollmentIndex);
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

        public async Task<StudentCourseEnrollmentResponse> GetStudentCourseEnrollment(Guid childId, Guid courseId)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"SELECT * FROM student_course_enrollment WHERE ChildId = @ChildId and CourseId = @CourseId";
            cmd.AddParameter("@CourseId", courseId.ToByteArray());
            cmd.AddParameter("@ChildId", childId.ToByteArray());

            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) 
                return null;
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

            //cmd.CommandText = @"
            //    UPDATE student_course_enrollment 
            //    SET CourseEnrollmentGroupId=@CourseEnrollmentGroupId,
            //        CourseId=@CourseId,
            //        ChildId=@ChildId,
            //        FamilyId=@FamilyId,
            //        WillUseDayCare=@WillUseDayCare,
            //        DayCareDays=@DayCareDays,
            //        UpdatedOn=@UpdatedOn,
            //        EnrollmentIndex = @EnrollmentIndex
            //    WHERE StudentCourseEnrollmentId=@EnrollmentId";

            cmd.CommandText = @"
                UPDATE student_course_enrollment 
                SET WillUseDayCare=@WillUseDayCare,
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
            cmd.AddParameter("@EnrollmentIndex", enrollment.EnrollmentIndex);
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
                UpdatedOn = reader.GetDateTime("UpdatedOn"),
                EnrollmentIndex = reader.GetInt32("EnrollmentIndex")
            };
        }
    }
}
