using Cumulus.Data;
using Data;
using MaktabDataContracts.Enums;
using MaktabDataContracts.Helpers;
using MaktabDataContracts.Requests.Course;
using MaktabDataContracts.Responses.Course;
using System.Data.Common;
using System.Text;

namespace Courses.Repository.Implementation
{
    public class CourseRepository : DbRepository, ICourseRepository
    {
        private readonly ICourseEnrollmentGroupRepository _groupRepo;

        public CourseRepository(IDatabase database, ICourseEnrollmentGroupRepository groupRepo)
            : base(database)
        {
            _groupRepo = groupRepo;
        }

        // Add a new course
        public async Task<CourseResponseDetailed> AddCourse(AddCourse course)
        {
            var courseId = Guid.NewGuid();
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                INSERT INTO courses
                (CourseId, InstituteId, Name, NameFr, Description, DescriptionFr,
                 Details, DetailsFr, StartDate, EndDate, IsActive,
                 CreatedAt, UpdatedOn, CanSelectMultipleEnrollmentGroups,
                 PolicyHyperLink, IsCourseCompleted, IsRegistrationOpened,
                 RegistrationStartDate, RegistrationEndDate, CourseSession, RegistrationFee, OfferDaycare,
                 IsManualEnrollment, IsCourseHasPrequisite, IsAdultRestricted, IsCourseAnEvent)
                VALUES
                (@CourseId, @InstituteId, @Name, @NameFr, @Description, @DescriptionFr,
                 @Details, @DetailsFr, @StartDate, @EndDate, @IsActive,
                 @CreatedAt, @UpdatedOn, @CanSelectMultipleEnrollmentGroups,
                 @PolicyHyperLink, @IsCourseCompleted, @IsRegistrationOpened,
                 @RegistrationStartDate, @RegistrationEndDate, @CourseSession, @RegistrationFee,@OfferDaycare,
                 @IsManualEnrollment, @IsCourseHasPrequisite, @IsAdultRestricted, @IsCourseAnEvent)";

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
            cmd.AddParameter("@OfferDaycare", course.OfferDaycare);
            cmd.AddParameter("@IsManualEnrollment", course.IsManualEnrollment);
            cmd.AddParameter("@IsCourseHasPrequisite", course.IsCourseHasPrequisite);
            cmd.AddParameter("@IsAdultRestricted", course.IsAdultRestricted);
            cmd.AddParameter("@IsCourseAnEvent", course.IsCourseAnEvent);

            // ✅ FIX: DBNull-safe nullable DateTime parameters
            cmd.AddParameter("@RegistrationStartDate", (object?)course.RegistrationStartDate ?? DBNull.Value);
            cmd.AddParameter("@RegistrationEndDate", (object?)course.RegistrationEndDate ?? DBNull.Value);

            cmd.AddParameter("@CourseSession", (int)course.CourseSession);
            cmd.AddParameter("@RegistrationFee", (int)course.RegistrationFee);

            await cmd.ExecuteNonQueryAsync();
            await ReplaceCourseCustomRequirements(courseId, course.CustomRequirements);
            return await GetCourse(courseId) ?? throw new Exception("Failed to retrieve created course");
        }

        // Get course by Id
        public async Task<CourseResponseDetailed?> GetCourse(Guid courseId)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT
                    c.*,
                    i.Name AS InstituteName,
                    i.NameFr AS InstituteNameFr,
                    i.Email AS InstituteEmail,
                    i.Phone AS InstitutePhone
                FROM courses c
                LEFT JOIN institutes i ON i.InstituteId = c.InstituteId
                WHERE c.CourseId = @CourseId";
            cmd.AddParameter("@CourseId", courseId.ToByteArray());

            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return null;

            return await MapToCourseResponse(reader);
        }

        public async Task<int?> GetHelcimTerminalId(Guid courseId)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT i.TerminalId
                FROM courses c
                INNER JOIN institutes i ON i.InstituteId = c.InstituteId
                WHERE c.CourseId = @CourseId";
            cmd.AddParameter("@CourseId", courseId.ToByteArray());

            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                return null;
            }

            var terminalIdColumn = FindColumn(reader, "TerminalId");
            if (terminalIdColumn == null || reader.IsDBNull(terminalIdColumn.Value))
            {
                return null;
            }

            var rawValue = Convert.ToString(reader.GetValue(terminalIdColumn.Value));
            return int.TryParse(rawValue, out var terminalId)
                ? terminalId
                : null;
        }

        // Get all courses with options
        public async Task<IEnumerable<CourseResponseDetailed>> GetAllCourses(GetCourseOptions options)
        {
            var results = new List<CourseResponseDetailed>();
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            var sql = new StringBuilder(@"
                SELECT
                    c.*,
                    i.Name AS InstituteName,
                    i.NameFr AS InstituteNameFr,
                    i.Email AS InstituteEmail,
                    i.Phone AS InstitutePhone
                FROM courses c
                LEFT JOIN institutes i ON i.InstituteId = c.InstituteId
                WHERE 1=1");
            var loadCourseType = NormalizeLoadCourseType(options);

            if (options.IsActive.HasValue)
            {
                sql.Append(" AND c.IsActive=@IsActive");
                cmd.AddParameter("@IsActive", options.IsActive.Value);
            }

            if (IncludesLoadCourseType(loadCourseType, NormalCourseTypeFlag) &&
                !IncludesLoadCourseType(loadCourseType, EventCourseTypeFlag))
            {
                sql.Append(" AND c.IsCourseAnEvent=@IsCourseAnEvent");
                cmd.AddParameter("@IsCourseAnEvent", false);
            }
            else if (!IncludesLoadCourseType(loadCourseType, NormalCourseTypeFlag) &&
                     IncludesLoadCourseType(loadCourseType, EventCourseTypeFlag))
            {
                sql.Append(" AND c.IsCourseAnEvent=@IsCourseAnEvent");
                cmd.AddParameter("@IsCourseAnEvent", true);
            }

            if (options.InstituteIds?.Any() == true)
            {
                sql.Append(" AND c.InstituteId IN (");
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
                sql.Append(" AND c.StartDate>=@OfferedFromDate");
                cmd.AddParameter("@OfferedFromDate", options.OfferedFromDate.Value);
            }

            if (options.OfferedToDate.HasValue)
            {
                sql.Append(" AND c.EndDate<=@OfferedToDate");
                cmd.AddParameter("@OfferedToDate", options.OfferedToDate.Value);
            }

            if (options.AcedemicGroups?.Any() == true)
            {
                int groupMask = (int)AcedemicGroupHelper.FromStrings(options.AcedemicGroups);
                sql.Append(@" AND c.CourseId IN (
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
            var options = new GetCourseOptions { IsActive = onlyActive };
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
                    PolicyHyperLink=@PolicyHyperLink, IsCourseCompleted=@IsCourseCompleted, IsRegistrationOpened=@IsRegistrationOpened,
                    RegistrationStartDate=@RegistrationStartDate, RegistrationEndDate=@RegistrationEndDate, CourseSession=@CourseSession, 
                    RegistrationFee=@RegistrationFee, OfferDaycare =@OfferDaycare,
                    IsManualEnrollment=@IsManualEnrollment, IsCourseHasPrequisite=@IsCourseHasPrequisite,
                    IsAdultRestricted=@IsAdultRestricted, IsCourseAnEvent=@IsCourseAnEvent
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

            // ✅ FIX: DBNull-safe nullable DateTime parameters
            cmd.AddParameter("@RegistrationStartDate", (object?)course.RegistrationStartDate ?? DBNull.Value);
            cmd.AddParameter("@RegistrationEndDate", (object?)course.RegistrationEndDate ?? DBNull.Value);

            cmd.AddParameter("@CourseSession", (int)course.CourseSession);
            cmd.AddParameter("@RegistrationFee", (int)course.RegistrationFee);
            cmd.AddParameter("@OfferDaycare", course.OfferDaycare);
            cmd.AddParameter("@IsManualEnrollment", course.IsManualEnrollment);
            cmd.AddParameter("@IsCourseHasPrequisite", course.IsCourseHasPrequisite);
            cmd.AddParameter("@IsAdultRestricted", course.IsAdultRestricted);
            cmd.AddParameter("@IsCourseAnEvent", course.IsCourseAnEvent);

            var updated = await cmd.ExecuteNonQueryAsync() > 0;
            if (updated)
            {
                await ReplaceCourseCustomRequirements(courseId, course.CustomRequirements);
            }

            return updated;
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
        public async Task<CourseResponseDetailed?> SetCourseRegistrationStatus(Guid courseId, bool ifRegistrationOpen)
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

            // Keep your original behavior:
            var registrationStart = reader.GetNullableDateTimeUtc("RegistrationStartDate") ?? reader.GetDateTimeUtc("StartDate");
            var registrationEnd = reader.GetNullableDateTimeUtc("RegistrationEndDate") ?? reader.GetDateTimeUtc("EndDate");

            var course = new CourseResponseDetailed
            {
                CourseId = courseId,
                InstituteId = reader.GetGuidFromByteArray("InstituteId"),
                InstituteName = ReadStringColumn(reader, "InstituteName"),
                InstituteNameFr = ReadStringColumn(reader, "InstituteNameFr"),
                InstituteEmail = ReadStringColumn(reader, "InstituteEmail"),
                InstitutePhone = ReadStringColumn(reader, "InstitutePhone"),
                Name = reader.GetString("Name"),
                NameFr = reader.GetString("NameFr"),
                Description = reader.IsDBNull("Description") ? string.Empty : reader.GetString("Description"),
                DescriptionFr = reader.IsDBNull("DescriptionFr") ? string.Empty : reader.GetString("DescriptionFr"),
                Details = reader.IsDBNull("Details") ? string.Empty : reader.GetString("Details"),
                DetailsFr = reader.IsDBNull("DetailsFr") ? string.Empty : reader.GetString("DetailsFr"),
                StartDate = reader.GetDateTimeUtc("StartDate"),
                EndDate = reader.GetDateTimeUtc("EndDate"),
                IsActive = reader.GetBoolean("IsActive"),
                CanSelectMultipleEnrollmentGroups = reader.GetBoolean("CanSelectMultipleEnrollmentGroups"),
                CreatedAt = reader.GetDateTimeUtc("CreatedAt"),
                UpdatedOn = reader.GetDateTimeUtc("UpdatedOn"),
                PolicyHyperLink = reader.IsDBNull("PolicyHyperLink") ? string.Empty : reader.GetString("PolicyHyperLink"),
                IsCourseCompleted = reader.GetBoolean("IsCourseCompleted"),
                IsCourseHasPrequisite = ReadBooleanColumn(reader, "IsCourseHasPrequisite"),
                IsManualEnrollment = ReadBooleanColumn(reader, "IsManualEnrollment"),
                IsAdultRestricted = ReadBooleanColumn(reader, "IsAdultRestricted"),
                IsCourseAnEvent = ReadBooleanColumn(reader, "IsCourseAnEvent"),
                IsRegistrationOpened = reader.GetBoolean("IsRegistrationOpened"),
                RegistrationStartDate = registrationStart,
                RegistrationEndDate = registrationEnd,
                CourseSession = (CourseSessionType)reader.GetByte("CourseSession"),
                RegistrationFee = (int)reader.GetInt32("RegistrationFee"),
                OfferDaycare = reader.GetBoolean("OfferDaycare")
            };

            // Load enrollment groups using DI (kept as-is)
            course.CourseEnrollmentGroups = (await _groupRepo.GetAllGroups(courseId, true))
                .OrderBy(group => group.GroupIndex)
                .ThenBy(group => group.CreatedAt)
                .ToList();

            // Merge unique academic groups
            course.AcedemicGroups = course.CourseEnrollmentGroups
                                          .SelectMany(g => g.AcedemicGroups)
                                          .Distinct()
                                          .ToList();
            course.CustomRequirements = await GetCourseCustomRequirements(courseId);
            return course;
        }

        private async Task<List<CourseCustomRequirements>> GetCourseCustomRequirements(Guid courseId)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT RequirementType
                FROM course_custom_requirements
                WHERE CourseId = @CourseId
                ORDER BY RequirementType";
            cmd.AddParameter("@CourseId", courseId.ToByteArray());

            using var reader = await cmd.ExecuteReaderAsync();
            var requirementColumn = FindColumn(reader, "RequirementType");
            if (requirementColumn == null)
            {
                return new List<CourseCustomRequirements>();
            }

            var requirements = new List<CourseCustomRequirements>();
            while (await reader.ReadAsync())
            {
                if (reader.IsDBNull(requirementColumn.Value))
                {
                    continue;
                }

                var requirement = (CourseCustomRequirements)Convert.ToInt32(reader.GetValue(requirementColumn.Value));
                if (requirement != CourseCustomRequirements.None && Enum.IsDefined(requirement))
                {
                    requirements.Add(requirement);
                }
            }

            return requirements;
        }

        private async Task ReplaceCourseCustomRequirements(
            Guid courseId,
            IEnumerable<CourseCustomRequirements>? requestedRequirements)
        {
            var requirements = NormalizeCustomRequirements(requestedRequirements);

            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM course_custom_requirements WHERE CourseId = @CourseId";
            cmd.AddParameter("@CourseId", courseId.ToByteArray());
            await cmd.ExecuteNonQueryAsync();

            foreach (var requirement in requirements)
            {
                cmd.Parameters.Clear();
                cmd.CommandText = @"
                    INSERT INTO course_custom_requirements (CourseId, RequirementType, CreatedAt)
                    VALUES (@CourseId, @RequirementType, @CreatedAt)";
                cmd.AddParameter("@CourseId", courseId.ToByteArray());
                cmd.AddParameter("@RequirementType", (int)requirement);
                cmd.AddParameter("@CreatedAt", DateTime.UtcNow);
                await cmd.ExecuteNonQueryAsync();
            }
        }

        private static List<CourseCustomRequirements> NormalizeCustomRequirements(
            IEnumerable<CourseCustomRequirements>? requestedRequirements)
        {
            var requirements = requestedRequirements?
                .Where(requirement => requirement != CourseCustomRequirements.None)
                .Distinct()
                .ToList() ?? new List<CourseCustomRequirements>();

            if (requirements.Any(requirement => !Enum.IsDefined(requirement)))
            {
                throw new ArgumentOutOfRangeException(nameof(requestedRequirements), "Unsupported course custom requirement.");
            }

            return requirements;
        }

        private const int NormalCourseTypeFlag = 1;
        private const int EventCourseTypeFlag = 2;

        private static int NormalizeLoadCourseType(GetCourseOptions options)
        {
            var loadCourseType = Convert.ToInt32(options.LoadCourseType);
            return loadCourseType == 0
                ? NormalCourseTypeFlag
                : loadCourseType;
        }

        private static bool IncludesLoadCourseType(int value, int flag)
            => (value & flag) == flag;

        private static int? FindColumn(DbDataReader reader, string columnName)
        {
            for (var index = 0; index < reader.FieldCount; index++)
            {
                if (string.Equals(reader.GetName(index), columnName, StringComparison.OrdinalIgnoreCase))
                {
                    return index;
                }
            }

            return null;
        }

        private static bool ReadBooleanColumn(DbDataReader reader, string columnName)
        {
            var ordinal = FindColumn(reader, columnName);
            return ordinal.HasValue && !reader.IsDBNull(ordinal.Value) && reader.GetBoolean(ordinal.Value);
        }

        private static string ReadStringColumn(DbDataReader reader, string columnName)
        {
            var ordinal = FindColumn(reader, columnName);
            return ordinal.HasValue && !reader.IsDBNull(ordinal.Value)
                ? Convert.ToString(reader.GetValue(ordinal.Value)) ?? string.Empty
                : string.Empty;
        }

        // ✅ FIXED: prevents duplicates + returns deterministic EnrollmentGroupActive
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
                    MAX(ceg.IsActive) AS EnrollmentGroupActive
                FROM courses c
                INNER JOIN course_enrollment_groups ceg
                    ON c.CourseId = ceg.CourseId
                INNER JOIN student_course_enrollment sce
                    ON ceg.CourseEnrollmentGroupId = sce.CourseEnrollmentGroupId
                WHERE sce.FamilyId = @FamilyId
                  AND sce.IsActive = 1
            ");

            cmd.AddParameter("@FamilyId", familyId.ToByteArray());

            if (instituteId.HasValue)
            {
                sql.Append(" AND c.InstituteId = @InstituteId");
                cmd.AddParameter("@InstituteId", instituteId.Value.ToByteArray());
            }

            sql.Append(@"
                GROUP BY
                    sce.FamilyId,
                    c.InstituteId,
                    c.CourseId,
                    c.CourseSession,
                    c.IsCourseCompleted,
                    c.IsRegistrationOpened,
                    c.RegistrationStartDate,
                    c.RegistrationEndDate,
                    c.IsActive
                ORDER BY MAX(c.CreatedAt) DESC;
            ");

            cmd.CommandText = sql.ToString();

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(MapToCourseSessionInfoResponse(reader));
            }

            return results;
        }

        // ✅ FIXED mapper: reads aggregated EnrollmentGroupActive and uses nullable UTC helper
        private CourseSessionInfoResponse MapToCourseSessionInfoResponse(DbDataReader reader)
        {
            // EnrollmentGroupActive comes from MAX(ceg.IsActive) → could be 0/1, and may come back as sbyte/byte/int depending on provider.
            object rawActive = reader["EnrollmentGroupActive"];
            bool enrollmentGroupActive =
                rawActive != null &&
                rawActive != DBNull.Value &&
                Convert.ToInt32(rawActive) == 1;

            return new CourseSessionInfoResponse
            {
                FamilyId = reader.GetGuidFromByteArray("FamilyId"),
                InstituteId = reader.GetGuidFromByteArray("InstituteId"),
                CourseId = reader.GetGuidFromByteArray("CourseId"),

                CourseSession = (CourseSessionType)reader.GetByte("CourseSession"),
                IsCourseCompleted = reader.GetBoolean("IsCourseCompleted"),
                IsRegistrationOpened = reader.GetBoolean("IsRegistrationOpened"),

                RegistrationStartDate = reader.GetNullableDateTimeUtc("RegistrationStartDate"),
                RegistrationEndDate = reader.GetNullableDateTimeUtc("RegistrationEndDate"),

                CourseActive = reader.GetBoolean("CourseActive"),
                EnrollmentGroupActive = enrollmentGroupActive
            };
        }
    }
}
