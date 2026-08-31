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
        private const int MotherRelationship = (int)Relationship.Mother;
        private const int FatherRelationship = (int)Relationship.Father;
        private const int GuardianRelationship = (int)Relationship.Guardian;

        private static readonly string FamilyMemberJoinSql = $@"
        LEFT JOIN (
            SELECT
                ui.UserId,
                ui.FamilyId,
                ui.FirstName,
                ui.LastName,
                ui.Email,
                ui.Phone,
                ui.Relationship
            FROM user_info ui
            WHERE ui.IsActive = b'1'
              AND (
                ui.Relationship NOT IN ({MotherRelationship}, {FatherRelationship})
                OR NOT EXISTS (
                    SELECT 1
                    FROM user_info ui2
                    WHERE ui2.IsActive = b'1'
                      AND ui2.FamilyId = ui.FamilyId
                      AND ui2.Relationship = ui.Relationship
                      AND ui2.Relationship IN ({MotherRelationship}, {FatherRelationship})
                      AND (
                        ui2.UpdatedOn > ui.UpdatedOn
                        OR (ui2.UpdatedOn = ui.UpdatedOn AND ui2.CreatedAt > ui.CreatedAt)
                        OR (ui2.UpdatedOn = ui.UpdatedOn AND ui2.CreatedAt = ui.CreatedAt AND ui2.UserId > ui.UserId)
                      )
                )
              )

            UNION ALL

            SELECT
                tui.UserId,
                tui.FamilyId,
                tui.FirstName,
                tui.LastName,
                tui.Email,
                tui.Phone,
                tui.Relationship
            FROM temp_user_info tui
            WHERE tui.IsActive = b'1'
              AND (
                tui.Relationship NOT IN ({MotherRelationship}, {FatherRelationship}, {GuardianRelationship})
                OR (
                    tui.Relationship = {GuardianRelationship}
                    AND NOT EXISTS (
                        SELECT 1
                        FROM user_info ui2
                        WHERE ui2.IsActive = b'1'
                          AND ui2.FamilyId = tui.FamilyId
                          AND ui2.Relationship = tui.Relationship
                    )
                )
                OR (
                    tui.Relationship IN ({MotherRelationship}, {FatherRelationship})
                    AND NOT EXISTS (
                        SELECT 1
                        FROM user_info ui2
                        WHERE ui2.IsActive = b'1'
                          AND ui2.FamilyId = tui.FamilyId
                          AND ui2.Relationship = tui.Relationship
                    )
                    AND NOT EXISTS (
                        SELECT 1
                        FROM temp_user_info tui2
                        WHERE tui2.IsActive = b'1'
                          AND tui2.FamilyId = tui.FamilyId
                          AND tui2.Relationship = tui.Relationship
                          AND (
                            tui2.UpdatedOn > tui.UpdatedOn
                            OR (tui2.UpdatedOn = tui.UpdatedOn AND tui2.CreatedAt > tui.CreatedAt)
                            OR (tui2.UpdatedOn = tui.UpdatedOn AND tui2.CreatedAt = tui.CreatedAt AND tui2.UserId > tui.UserId)
                          )
                    )
                )
              )
        ) ui ON ui.FamilyId = sce.FamilyId";

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

            cmd.CommandText = $@"
        SELECT
            sce.StudentCourseEnrollmentId,
            sce.CourseEnrollmentGroupId,
            sce.CourseId,
            sce.ChildId,
            sce.FamilyId,
            ins.Name AS InstituteName,
            c.Name AS CourseName,
            c.NameFr AS CourseNameFr,
            ceg.GroupTitle,
            ceg.GroupTitleFr,
            sce.WillUseDayCare,
            sce.DayCareDays,
            sce.IsActive,
            sce.CreatedAt,
            sce.UpdatedOn,
            sce.EnrollmentIndex,
            CAST(sce.EnrollmentStatus AS SIGNED) AS EnrollmentStatus,
            ceg.GroupIndex AS GroupIndex,
            ci.FirstName AS ChildFirstName,
            ci.LastName AS ChildLastName,
            ci.ArabicName AS ChildArabicName,
            ci.RegistrationNumber AS ChildRegistrationNumber,
            ci.DateOfBirth AS ChildDateOfBirth,
            ci.Gender AS ChildGender,
            ci.RAMQNumber AS ChildRamqNumber,
            ci.RAMQExpiry AS ChildRamqExpiry,
            ci.Allergies AS ChildAllergies,
            ci.OtherHealthConditions AS ChildOtherHealthConditions,
            ci.Consent AS ChildConsent,
            ui.UserId,
            ui.FirstName AS UserFirstName,
            ui.LastName AS UserLastName,
            ui.Email,
            ui.Phone,
            ui.Relationship
        FROM student_course_enrollment sce
        LEFT JOIN course_enrollment_groups ceg ON ceg.CourseEnrollmentGroupId = sce.CourseEnrollmentGroupId
        LEFT JOIN courses c ON c.CourseId = sce.CourseId
        LEFT JOIN institutes ins ON ins.InstituteId = c.InstituteId
        INNER JOIN child_information ci ON ci.ChildId = sce.ChildId
        {FamilyMemberJoinSql}
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
            ins.Name AS InstituteName,
            c.Name AS CourseName,
            c.NameFr AS CourseNameFr,
            ceg.GroupTitle,
            ceg.GroupTitleFr,
            sce.WillUseDayCare,
            sce.DayCareDays,
            sce.IsActive,
            sce.CreatedAt,
            sce.UpdatedOn,
            sce.EnrollmentIndex,
            CAST(sce.EnrollmentStatus AS SIGNED) AS EnrollmentStatus,
            ceg.GroupIndex AS GroupIndex,
            ci.FirstName AS ChildFirstName,
            ci.LastName AS ChildLastName,
            ci.RegistrationNumber AS ChildRegistrationNumber,
            ci.DateOfBirth AS ChildDateOfBirth,
            ci.Gender AS ChildGender,
            ci.RAMQNumber AS ChildRamqNumber,
            ci.RAMQExpiry AS ChildRamqExpiry,
            ci.Allergies AS ChildAllergies,
            ci.OtherHealthConditions AS ChildOtherHealthConditions,
            ci.Consent AS ChildConsent,
            ci.ArabicName AS ChildArabicName,
            ui.UserId,
            ui.FirstName AS UserFirstName,
            ui.LastName AS UserLastName,
            ui.Email,
            ui.Phone,
            ui.Relationship
        FROM student_course_enrollment sce
        LEFT JOIN course_enrollment_groups ceg ON ceg.CourseEnrollmentGroupId = sce.CourseEnrollmentGroupId
        LEFT JOIN courses c ON c.CourseId = sce.CourseId
        LEFT JOIN institutes ins ON ins.InstituteId = c.InstituteId
        INNER JOIN child_information ci ON ci.ChildId = sce.ChildId
        {FamilyMemberJoinSql}
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
            var response = new StudentCourseEnrollmentResponse
            {
                StudentCourseEnrollmentId = reader.GetGuidFromByteArray("StudentCourseEnrollmentId"),
                CourseEnrollmentGroupId = reader.GetGuidFromByteArray("CourseEnrollmentGroupId"),
                CourseId = reader.GetGuidFromByteArray("CourseId"),
                ChildId = reader.GetGuidFromByteArray("ChildId"),
                FamilyId = reader.GetGuidFromByteArray("FamilyId"),
                InstituteName = reader.GetStringOrDefault("InstituteName"),
                CourseName = reader.GetStringOrDefault("CourseName"),
                CourseNameFr = reader.GetStringOrDefault("CourseNameFr"),

                ChildName = $"{reader.GetString("ChildFirstName")} {reader.GetString("ChildLastName")}",
                ArabicName = reader.GetStringOrDefault("ChildArabicName"),
                RegistrationNumber = reader.GetStringOrDefault("ChildRegistrationNumber"),
                DateOfBirth = reader.GetDateTimeUtcOrDefault("ChildDateOfBirth", DateTime.MinValue),
                Gender = (Gender)reader.GetIntOrDefault("ChildGender", (int)Gender.Unknown),
                RAMQNumber = reader.GetStringOrDefault("ChildRamqNumber"),
                RAMQExpiry = reader.GetDateTimeUtcOrDefault("ChildRamqExpiry", DateTime.MinValue),
                Allergies = reader.GetStringOrDefault("ChildAllergies"),
                OtherHealthConditions = reader.GetStringOrDefault("ChildOtherHealthConditions"),

                WillUseDayCare = reader.GetBoolean("WillUseDayCare"),
                DayCareDays = reader.GetInt32("DayCareDays"),
                IsActive = reader.GetBoolean("IsActive"),
                CreatedAt = reader.GetDateTime("CreatedAt"),
                UpdatedOn = reader.GetDateTime("UpdatedOn"),
                EnrollmentIndex = reader.GetInt32("EnrollmentIndex"),
                CourseEnrollmentGroupName = reader.GetStringOrDefault("GroupTitle"),
                CourseEnrollmentGroupNameFr = reader.GetStringOrDefault("GroupTitleFr"),
                GroupTitle = reader.GetStringOrDefault("GroupTitle"),
                GroupTitleFr = reader.GetStringOrDefault("GroupTitleFr"),
                GroupIndex = reader.GetInt32("GroupIndex"),
                EnrollmentStatus= (EnrollmentStatus)reader.GetInt32("EnrollmentStatus"),

               FamilyMembers = new List<FamilyInfo>()
            };

            SetConsent(response, GetChildConsent(reader));
            return response;
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

        private static string GetChildConsent(DbDataReader reader)
        {
            var ordinal = reader.GetOrdinal("ChildConsent");
            return reader.IsDBNull(ordinal) ? string.Empty : reader.GetString(ordinal);
        }

        private static void SetConsent(StudentCourseEnrollmentResponse response, string consent)
        {
            var responseType = typeof(StudentCourseEnrollmentResponse);

            var consentProperty = responseType.GetProperty("Consent");
            if (consentProperty?.CanWrite == true)
            {
                consentProperty.SetValue(response, consent);
                return;
            }

            var consntProperty = responseType.GetProperty("Consnt");
            if (consntProperty?.CanWrite == true)
            {
                consntProperty.SetValue(response, consent);
            }
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
            ceg.GroupIndex,
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
        GROUP BY ceg.CourseEnrollmentGroupId, ceg.CourseId, ceg.GroupIndex, ceg.MaxStudents, ceg.IfRegistrationOpen
        ORDER BY ceg.GroupIndex ASC, ceg.CreatedAt ASC";

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
                    GroupIndex = reader.GetInt32("GroupIndex"),
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
            ceg.GroupIndex,
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
        GROUP BY ceg.CourseEnrollmentGroupId, ceg.CourseId, ceg.GroupIndex, ceg.MaxStudents, ceg.IfRegistrationOpen";

            cmd.AddParameter("@CourseGroupId", courseGroupId.ToByteArray());

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                if (!found)
                {
                    result.CourseEnrollmentGroupId = reader.GetGuidFromByteArray("CourseEnrollmentGroupId");
                    result.CourseId = reader.GetGuidFromByteArray("CourseId");
                    result.GroupIndex = reader.GetInt32("GroupIndex");
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
