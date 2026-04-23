using Cumulus.Data;
using Data;
using MaktabDataContracts.Enums;
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
                 WillUseDayCare, DayCareDays, IsActive, CreatedAt, UpdatedOn, EnrollmentIndex,EnrollmentStatus)
                VALUES 
                (@StudentCourseEnrollmentId, @CourseEnrollmentGroupId, @CourseId, @ChildId, @FamilyId, 
                 @WillUseDayCare, @DayCareDays, @IsActive, @CreatedAt, @UpdatedOn, @EnrollmentIndex,@EnrollmentStatus)";

            enrollment.IsActive = true;

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
            cmd.AddParameter("@EnrollmentStatus", enrollment.EnrollmentStatus);  
            
            await cmd.ExecuteNonQueryAsync();
            return await GetEnrollment(enrollmentId) ?? throw new Exception("Failed to retrieve created enrollment");
        }

        // Get a single enrollment by ID
        public async Task<StudentCourseEnrollmentResponse?> GetEnrollment(Guid enrollmentId)
        {
            var results = await GetEnrollmentsByColumnAsync("StudentCourseEnrollmentId", enrollmentId);
            return results.FirstOrDefault();
        }

        public async Task<StudentCourseEnrollmentResponse> GetStudentCourseEnrollment(Guid childId, Guid courseId)
        {
            var lookup = new Dictionary<Guid, StudentCourseEnrollmentResponse>();

            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
        SELECT
            sce.StudentCourseEnrollmentId,
            sce.CourseEnrollmentGroupId,
            sce.CourseId,
            sce.ChildId,
            sce.FamilyId,
            sce.WillUseDayCare,
            sce.DayCareDays,
            sce.IsActive,
            sce.CreatedAt,
            sce.UpdatedOn,
            sce.EnrollmentIndex,
            CAST(sce.EnrollmentStatus AS SIGNED) AS EnrollmentStatus,
            ci.FirstName AS ChildFirstName,
            ci.LastName AS ChildLastName,
            ui.UserId,
            ui.FirstName AS UserFirstName,
            ui.LastName AS UserLastName,
            ui.Email,
            ui.Phone,
            ui.Relationship
        FROM student_course_enrollment sce
        INNER JOIN child_information ci ON ci.ChildId = sce.ChildId
        LEFT JOIN user_info ui ON ui.FamilyId = sce.FamilyId AND ui.IsActive = b'1'
        WHERE sce.ChildId = @ChildId AND sce.CourseId = @CourseId AND sce.IsActive = TRUE";

            cmd.AddParameter("@CourseId", courseId.ToByteArray());
            cmd.AddParameter("@ChildId", childId.ToByteArray());

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var enrollmentId = reader.GetGuidFromByteArray("StudentCourseEnrollmentId");

                if (!lookup.TryGetValue(enrollmentId, out var enrollment))
                {
                    enrollment = MapToEnrollmentResponse(reader);
                    lookup[enrollmentId] = enrollment;
                }

                if (!reader.IsDBNull("UserId"))
                {
                    enrollment.FamilyMembers.Add(MapToFamilyInfo(reader));
                }
            }

            return lookup.Values.FirstOrDefault();
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
                    EnrollmentStatus= @EnrollmentStatus,    
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
            cmd.AddParameter("@EnrollmentStatus", enrollment.EnrollmentStatus);
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
            return await GetEnrollmentsByColumnAsync("CourseEnrollmentGroupId", groupId);
        }

        public Task<IEnumerable<StudentCourseEnrollmentResponse>> GetAllEnrollmentsByCourse(Guid courseId)
        { 
            return GetEnrollmentsByColumnAsync("CourseId", courseId);
        }
        public Task<IEnumerable<StudentCourseEnrollmentResponse>> GetAllEnrollmentsByFamily(Guid familyId)
        { 
            return GetEnrollmentsByColumnAsync("FamilyId", familyId);
        }

        //private async Task<IEnumerable<StudentCourseEnrollmentResponse>> GetEnrollmentsByColumnAsync(string columnName, Guid value)
        //{
        //    var results = new List<StudentCourseEnrollmentResponse>();
        //    using var conn = await Database.CreateAndOpenConnectionAsync();
        //    using var cmd = conn.CreateCommand();

        //    cmd.CommandText = $"SELECT * FROM student_course_enrollment WHERE {columnName}=@Value AND IsActive=TRUE";
        //    cmd.AddParameter("@Value", value.ToByteArray());

        //    using var reader = await cmd.ExecuteReaderAsync();
        //    while (await reader.ReadAsync())
        //        results.Add(MapToEnrollmentResponse(reader));

        //    return results;
        //}


        // Helper: Map DbDataReader to StudentCourseEnrollmentResponse
        //private StudentCourseEnrollmentResponse MapToEnrollmentResponse(DbDataReader reader)
        //{
        //    return new StudentCourseEnrollmentResponse
        //    {
        //        StudentCourseEnrollmentId = reader.GetGuidFromByteArray("StudentCourseEnrollmentId"),
        //        CourseEnrollmentGroupId = reader.GetGuidFromByteArray("CourseEnrollmentGroupId"),
        //        CourseId = reader.GetGuidFromByteArray("CourseId"),
        //        ChildId = reader.GetGuidFromByteArray("ChildId"),
        //        FamilyId = reader.GetGuidFromByteArray("FamilyId"),
        //        WillUseDayCare = reader.GetBoolean("WillUseDayCare"),
        //        DayCareDays = reader.GetInt32("DayCareDays"),
        //        IsActive = reader.GetBoolean("IsActive"),
        //        CreatedAt = reader.GetDateTime("CreatedAt"),
        //        UpdatedOn = reader.GetDateTime("UpdatedOn"),
        //        EnrollmentIndex = reader.GetInt32("EnrollmentIndex")
        //    };
        //}

        private async Task<IEnumerable<StudentCourseEnrollmentResponse>> GetEnrollmentsByColumnAsync(
    string columnName, Guid value)
        {
            var lookup = new Dictionary<Guid, StudentCourseEnrollmentResponse>();

            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = $@"
        SELECT
            sce.StudentCourseEnrollmentId,
            sce.CourseEnrollmentGroupId,
            sce.CourseId,
            sce.ChildId,
            sce.FamilyId,
            sce.WillUseDayCare,
            sce.DayCareDays,
            sce.IsActive,
            sce.CreatedAt,
            sce.UpdatedOn,
            sce.EnrollmentIndex,
            CAST(sce.EnrollmentStatus AS SIGNED) AS EnrollmentStatus,
            ci.FirstName AS ChildFirstName,
            ci.LastName AS ChildLastName,
            ui.UserId,
            ui.FirstName AS UserFirstName,
            ui.LastName AS UserLastName,
            ui.Email,
            ui.Phone,
            ui.Relationship
        FROM student_course_enrollment sce
        INNER JOIN child_information ci ON ci.ChildId = sce.ChildId
        LEFT JOIN user_info ui ON ui.FamilyId = sce.FamilyId AND ui.IsActive = b'1'
        WHERE sce.{columnName} = @Value AND sce.IsActive = TRUE";

            cmd.AddParameter("@Value", value.ToByteArray());

            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var enrollmentId = reader.GetGuidFromByteArray("StudentCourseEnrollmentId");

                if (!lookup.TryGetValue(enrollmentId, out var enrollment))
                {
                    enrollment = MapToEnrollmentResponse(reader);
                    lookup[enrollmentId] = enrollment;
                }

                if (!reader.IsDBNull("UserId"))
                {
                    enrollment.FamilyMembers.Add(MapToFamilyInfo(reader));
                }
            }

            return lookup.Values;
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

                ChildName = $"{reader.GetString("ChildFirstName")} {reader.GetString("ChildLastName")}",

                WillUseDayCare = reader.GetBoolean("WillUseDayCare"),
                DayCareDays = reader.GetInt32("DayCareDays"),
                IsActive = reader.GetBoolean("IsActive"),
                CreatedAt = reader.GetDateTime("CreatedAt"),
                UpdatedOn = reader.GetDateTime("UpdatedOn"),
                EnrollmentIndex = reader.GetInt32("EnrollmentIndex"),
                EnrollmentStatus= (EnrollmentStatus)reader.GetInt32("EnrollmentStatus"),

               FamilyMembers = new List<FamilyInfo>()
            };
        }

        private FamilyInfo MapToFamilyInfo(DbDataReader reader)
        {
            return new FamilyInfo
            {
                UserId = reader.GetGuidFromByteArray("UserId"),
                UserName = $"{reader.GetString("UserFirstName")} {reader.GetString("UserLastName")}",
                Email = reader.GetString("Email"),
                Phone = reader.GetString("Phone"),
                Relationship = (Relationship)reader.GetInt32("Relationship")
            };
        }

        public async Task<IEnumerable<CourseEnrollmentGroupInformationResponse>> GetCourseEnrollmentGroupsInformation(Guid courseId)
        {
            var results = new Dictionary<Guid, CourseEnrollmentGroupInformationResponse>();

            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
        SELECT
            ceg.CourseEnrollmentGroupId,
            ceg.CourseId,
            ceg.MaxStudents,
            ceg.IfRegistrationOpen,
            SUM(CASE WHEN sce.EnrollmentStatus = 0 THEN 1 ELSE 0 END) AS UnknownCount,
            SUM(CASE WHEN sce.EnrollmentStatus = 1 THEN 1 ELSE 0 END) AS EnrolledCount,
            SUM(CASE WHEN sce.EnrollmentStatus = 2 THEN 1 ELSE 0 END) AS AwaitingCount,
            SUM(CASE WHEN sce.EnrollmentStatus = 3 THEN 1 ELSE 0 END) AS RegisteredCount,
            SUM(CASE WHEN sce.EnrollmentStatus = 4 THEN 1 ELSE 0 END) AS CancelledCount,
            SUM(CASE WHEN sce.EnrollmentStatus = 5 THEN 1 ELSE 0 END) AS RefundedCount
        FROM course_enrollment_groups ceg
        LEFT JOIN student_course_enrollment sce ON sce.CourseEnrollmentGroupId = ceg.CourseEnrollmentGroupId 
            AND sce.IsActive = TRUE
        WHERE ceg.CourseId = @CourseId AND ceg.IsActive = TRUE
        GROUP BY ceg.CourseEnrollmentGroupId, ceg.CourseId, ceg.MaxStudents, ceg.IfRegistrationOpen";

            cmd.AddParameter("@CourseId", courseId.ToByteArray());

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var groupId = reader.GetGuidFromByteArray("CourseEnrollmentGroupId");

                if (results.ContainsKey(groupId))
                {
                    continue;
                }

                results[groupId] = new CourseEnrollmentGroupInformationResponse
                {
                    CourseEnrollmentGroupId = groupId,
                    CourseId = reader.GetGuidFromByteArray("CourseId"),
                    MaxStudents = reader.IsDBNull("MaxStudents") ? 0 : reader.GetInt32("MaxStudents"),
                    IfRegistrationOpen = reader.GetBoolean("IfRegistrationOpen"),
                    EnrollmentStatusCount = CreateEnrollmentStatusCountMap(reader)
                };
            }

            return results.Values;
        }

        public async Task<CourseEnrollmentGroupInformationResponse?> GetCourseEnrollmentGroupInformation(Guid courseGroupId)
        {
            var result = new CourseEnrollmentGroupInformationResponse();
            bool found = false;

            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
        SELECT
            ceg.CourseEnrollmentGroupId,
            ceg.CourseId,
            ceg.MaxStudents,
            ceg.IfRegistrationOpen,
            SUM(CASE WHEN sce.EnrollmentStatus = 0 THEN 1 ELSE 0 END) AS UnknownCount,
            SUM(CASE WHEN sce.EnrollmentStatus = 1 THEN 1 ELSE 0 END) AS EnrolledCount,
            SUM(CASE WHEN sce.EnrollmentStatus = 2 THEN 1 ELSE 0 END) AS AwaitingCount,
            SUM(CASE WHEN sce.EnrollmentStatus = 3 THEN 1 ELSE 0 END) AS RegisteredCount,
            SUM(CASE WHEN sce.EnrollmentStatus = 4 THEN 1 ELSE 0 END) AS CancelledCount,
            SUM(CASE WHEN sce.EnrollmentStatus = 5 THEN 1 ELSE 0 END) AS RefundedCount
        FROM course_enrollment_groups ceg
        LEFT JOIN student_course_enrollment sce ON sce.CourseEnrollmentGroupId = ceg.CourseEnrollmentGroupId 
            AND sce.IsActive = TRUE
        WHERE ceg.CourseEnrollmentGroupId = @CourseGroupId AND ceg.IsActive = TRUE
        GROUP BY ceg.CourseEnrollmentGroupId, ceg.CourseId, ceg.MaxStudents, ceg.IfRegistrationOpen";

            cmd.AddParameter("@CourseGroupId", courseGroupId.ToByteArray());

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                if (!found)
                {
                    result.CourseEnrollmentGroupId = reader.GetGuidFromByteArray("CourseEnrollmentGroupId");
                    result.CourseId = reader.GetGuidFromByteArray("CourseId");
                    result.MaxStudents = reader.IsDBNull("MaxStudents") ? 0 : reader.GetInt32("MaxStudents");
                    result.IfRegistrationOpen = reader.GetBoolean("IfRegistrationOpen");
                    result.EnrollmentStatusCount = CreateEnrollmentStatusCountMap(reader);
                    found = true;
                }
            }

            return found ? result : null;
        }

        private static Dictionary<EnrollmentStatus, int> CreateEnrollmentStatusCountMap(DbDataReader reader)
        {
            return new Dictionary<EnrollmentStatus, int>
            {
                [EnrollmentStatus.Unknown] = Convert.ToInt32(reader["UnknownCount"]),
                [EnrollmentStatus.Enrolled] = Convert.ToInt32(reader["EnrolledCount"]),
                [EnrollmentStatus.Awaiting] = Convert.ToInt32(reader["AwaitingCount"]),
                [EnrollmentStatus.Registered] = Convert.ToInt32(reader["RegisteredCount"]),
                [EnrollmentStatus.Cancelled] = Convert.ToInt32(reader["CancelledCount"]),
                [EnrollmentStatus.Refunded] = Convert.ToInt32(reader["RefundedCount"])
            };
        }

        public async Task<bool> UpdateEnrollmentStatus(Guid enrollmentId, EnrollmentStatus status)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                UPDATE student_course_enrollment
                SET EnrollmentStatus = @EnrollmentStatus,
                    UpdatedOn = @UpdatedOn
                WHERE StudentCourseEnrollmentId = @EnrollmentId";

            cmd.AddParameter("@EnrollmentId", enrollmentId.ToByteArray());
            cmd.AddParameter("@EnrollmentStatus", status);
            cmd.AddParameter("@UpdatedOn", DateTime.UtcNow);

            return await cmd.ExecuteNonQueryAsync() > 0;
        }

    }
}
