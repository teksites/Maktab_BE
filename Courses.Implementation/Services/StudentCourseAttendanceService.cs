using MaktabDataContracts.Enums;
using MaktabDataContracts.Requests.Attendance;
using MaktabDataContracts.Responses.Attendance;
using Courses.Repository;
using Courses.Services;
using Email;
using Users.Services;

namespace Courses.Implementation.Services
{
    public class StudentCourseAttendanceService : IStudentCourseAttendanceService
    {
        private readonly IStudentCourseAttendanceRepository _repository;
        private readonly ICourseStaffAssignmentService _courseStaffAssignmentService;
        private readonly ICourseService _courseService;
        private readonly ISendEmailService _sendEmailService;
        private readonly IUserService _userService;

        public StudentCourseAttendanceService(
            IStudentCourseAttendanceRepository repository,
            ICourseStaffAssignmentService courseStaffAssignmentService,
            ISendEmailService sendEmailService,
            IUserService userService,
            ICourseService courseService)
        {
            _repository = repository;
            _courseStaffAssignmentService = courseStaffAssignmentService;
            _courseService = courseService;
            _sendEmailService = sendEmailService;
            _userService = userService;
        }

        public async Task<CourseGroupAttendanceResponse> GetCourseGroupAttendance(Guid userId, UserRoleType userRoles, GetCourseGroupAttendanceRequest request)
        {
            var attendanceDate = NormalizeAttendanceDate(request.AttendanceDate);
            var roster = await _courseStaffAssignmentService
                .GetAssignedCourseGroupRoster(userId, userRoles, request.CourseEnrollmentGroupId)
                .ConfigureAwait(false);

            if (request.ChildId.HasValue && !roster.Students.Any(student => student.ChildId == request.ChildId.Value))
            {
                throw new InvalidOperationException("The selected child does not belong to this course group.");
            }

            var savedAttendance = await _repository
                .GetCourseGroupAttendance(request.CourseEnrollmentGroupId, attendanceDate, request.ChildId)
                .ConfigureAwait(false);

            var attendanceByEnrollmentId = savedAttendance.ToDictionary(item => item.StudentCourseEnrollmentId);
            var rosterStudents = roster.Students
                .Where(student => !request.ChildId.HasValue || student.ChildId == request.ChildId.Value)
                .Select(student => attendanceByEnrollmentId.TryGetValue(student.StudentCourseEnrollmentId, out var existing)
                    ? existing
                    : new StudentCourseAttendanceResponse
                    {
                        StudentCourseAttendanceId = Guid.Empty,
                        StudentCourseEnrollmentId = student.StudentCourseEnrollmentId,
                        ChildId = student.ChildId,
                        FamilyId = student.FamilyId,
                        ChildName = student.ChildName,
                        AttendanceStatus = AttendanceStatus.Present,
                        PickupContactType = PickupContactType.Unknown,
                        PickupDisplayName = string.Empty,
                        Notes = string.Empty,
                        IsActive = false,
                        RecordedByUserId = userId
                    })
                .ToList();

            return new CourseGroupAttendanceResponse
            {
                CourseId = roster.CourseId,
                CourseEnrollmentGroupId = roster.CourseEnrollmentGroupId,
                InstituteId = roster.InstituteId,
                AttendanceDate = attendanceDate,
                RecordedByUserId = savedAttendance.FirstOrDefault()?.RecordedByUserId ?? userId,
                Students = rosterStudents
            };
        }

        public async Task<IReadOnlyList<AttendanceRecordResponse>> GetFamilyAttendanceRecords(Guid familyId, GetAttendanceRecordsRequest request)
        {
            if (familyId == Guid.Empty)
            {
                throw new InvalidOperationException("FamilyId is required.");
            }

            var normalizedRequest = NormalizeRecordsRequest(request);
            normalizedRequest.FamilyId = familyId;

            var records = await _repository.GetAttendanceRecords(normalizedRequest).ConfigureAwait(false);
            return ApplyAttendanceFilter(records, normalizedRequest.Filter);
        }

        public async Task<StudentCourseAttendanceResponse> UpsertStudentAttendance(
            Guid userId,
            UserRoleType userRoles,
            Guid courseEnrollmentGroupId,
            Guid studentCourseEnrollmentId,
            UpsertStudentAttendanceRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            var roster = await _courseStaffAssignmentService
                .GetAssignedCourseGroupRoster(userId, userRoles, courseEnrollmentGroupId)
                .ConfigureAwait(false);
            var rosterStudent = roster.Students.SingleOrDefault(student =>
                student.StudentCourseEnrollmentId == studentCourseEnrollmentId);

            if (rosterStudent == null)
            {
                throw new InvalidOperationException("The selected student enrollment does not belong to this course group.");
            }

            var groupAttendance = await UpsertCourseGroupAttendance(userId, userRoles, new UpsertCourseGroupAttendanceRequest
            {
                CourseId = request.CourseId,
                CourseEnrollmentGroupId = courseEnrollmentGroupId,
                InstituteId = request.InstituteId,
                AttendanceDate = request.AttendanceDate,
                Students = new List<UpsertStudentCourseAttendanceRequest>
                {
                    new()
                    {
                        StudentCourseAttendanceId = request.StudentCourseAttendanceId,
                        StudentCourseEnrollmentId = rosterStudent.StudentCourseEnrollmentId,
                        ChildId = rosterStudent.ChildId,
                        FamilyId = rosterStudent.FamilyId,
                        AttendanceStatus = request.AttendanceStatus,
                        LateArrivalTime = request.LateArrivalTime,
                        EarlyPickupTime = request.EarlyPickupTime,
                        PickupContactType = request.PickupContactType,
                        PickupUserId = request.PickupUserId,
                        PickupOtherContactId = request.PickupOtherContactId,
                        Notes = request.Notes,
                        IsActive = true
                    }
                }
            }).ConfigureAwait(false);

            return groupAttendance.Students.Single(student =>
                student.StudentCourseEnrollmentId == studentCourseEnrollmentId);
        }

        public async Task<AttendanceReportResponse> GetFamilyAttendanceReport(Guid familyId, GetAttendanceReportRequest request)
        {
            if (familyId == Guid.Empty)
            {
                throw new InvalidOperationException("FamilyId is required.");
            }

            ArgumentNullException.ThrowIfNull(request);

            var records = await GetFamilyAttendanceRecords(familyId, new GetAttendanceRecordsRequest
            {
                FamilyId = familyId,
                ChildId = request.ChildId,
                CourseId = request.CourseId,
                CourseEnrollmentGroupId = request.CourseEnrollmentGroupId,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                Filter = request.Filter
            }).ConfigureAwait(false);

            return BuildAttendanceReport(request, records);
        }

        public async Task<CourseGroupAttendanceResponse> UpsertCourseGroupAttendance(Guid userId, UserRoleType userRoles, UpsertCourseGroupAttendanceRequest request)
        {
            request.AttendanceDate = NormalizeAttendanceDate(request.AttendanceDate);
            request.RecordedByUserId = userId;

            var roster = await _courseStaffAssignmentService
                .GetAssignedCourseGroupRoster(userId, userRoles, request.CourseEnrollmentGroupId)
                .ConfigureAwait(false);

            if (roster.CourseId != request.CourseId)
            {
                throw new InvalidOperationException("CourseId does not match the selected course group.");
            }

            if (roster.InstituteId != request.InstituteId)
            {
                throw new InvalidOperationException("InstituteId does not match the selected course group.");
            }

            var students = request.Students ?? new List<UpsertStudentCourseAttendanceRequest>();
            var existingAttendance = await _repository
                .GetCourseGroupAttendance(request.CourseEnrollmentGroupId, request.AttendanceDate)
                .ConfigureAwait(false);
            var duplicateEnrollmentId = students
                .GroupBy(student => student.StudentCourseEnrollmentId)
                .FirstOrDefault(group => group.Count() > 1)?.Key ?? Guid.Empty;

            if (duplicateEnrollmentId != Guid.Empty)
            {
                throw new InvalidOperationException("Duplicate attendance rows for the same student enrollment are not allowed.");
            }

            var rosterByEnrollmentId = roster.Students.ToDictionary(student => student.StudentCourseEnrollmentId);
            var existingAttendanceByEnrollmentId = existingAttendance.ToDictionary(item => item.StudentCourseEnrollmentId);

            foreach (var student in students)
            {
                if (!rosterByEnrollmentId.TryGetValue(student.StudentCourseEnrollmentId, out var rosterStudent))
                {
                    throw new InvalidOperationException("One or more student enrollments do not belong to the selected course group.");
                }

                if (student.ChildId != rosterStudent.ChildId || student.FamilyId != rosterStudent.FamilyId)
                {
                    throw new InvalidOperationException("Attendance payload does not match the selected student enrollment.");
                }

                NormalizeStudentAttendance(student);
                ValidatePickup(student, rosterStudent);
            }

            await _repository.UpsertCourseGroupAttendance(request).ConfigureAwait(false);
            await SendAttendanceNotificationEmailsAsync(
                roster,
                request.AttendanceDate,
                students,
                existingAttendanceByEnrollmentId).ConfigureAwait(false);
            return await GetCourseGroupAttendance(userId, userRoles, new GetCourseGroupAttendanceRequest
            {
                CourseEnrollmentGroupId = request.CourseEnrollmentGroupId,
                AttendanceDate = request.AttendanceDate
            }).ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<AttendanceRecordResponse>> GetStaffAttendanceRecords(Guid userId, UserRoleType userRoles, GetAttendanceRecordsRequest request)
        {
            var normalizedRequest = NormalizeRecordsRequest(request);
            var accessibleGroupIds = await ResolveAccessibleGroupIds(userId, userRoles, normalizedRequest).ConfigureAwait(false);
            var records = await _repository.GetAttendanceRecords(normalizedRequest, accessibleGroupIds).ConfigureAwait(false);
            return ApplyAttendanceFilter(records, normalizedRequest.Filter);
        }

        public async Task<AttendanceReportResponse> GetStaffAttendanceReport(Guid userId, UserRoleType userRoles, GetAttendanceReportRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            var records = await GetStaffAttendanceRecords(userId, userRoles, new GetAttendanceRecordsRequest
            {
                FamilyId = request.FamilyId,
                ChildId = request.ChildId,
                CourseId = request.CourseId,
                CourseEnrollmentGroupId = request.CourseEnrollmentGroupId,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                Filter = request.Filter
            }).ConfigureAwait(false);

            return BuildAttendanceReport(request, records);
        }

        private static AttendanceReportResponse BuildAttendanceReport(GetAttendanceReportRequest request, IReadOnlyList<AttendanceRecordResponse> records)
        {
            var items = request.Grouping switch
            {
                AttendanceReportGroupingType.Daily => records
                    .GroupBy(record => record.AttendanceDate.Date)
                    .OrderByDescending(group => group.Key)
                    .Select(group => BuildReportItem(
                        group.Key.ToString("yyyy-MM-dd"),
                        group.Key.ToString("yyyy-MM-dd"),
                        group,
                        group.Key,
                        group.Key))
                    .ToList(),
                AttendanceReportGroupingType.Weekly => records
                    .GroupBy(record => GetWeekStart(record.AttendanceDate))
                    .OrderByDescending(group => group.Key)
                    .Select(group => BuildReportItem(
                        group.Key.ToString("yyyy-MM-dd"),
                        $"{group.Key:yyyy-MM-dd} to {group.Key.AddDays(6):yyyy-MM-dd}",
                        group,
                        group.Key,
                        group.Key.AddDays(6)))
                    .ToList(),
                AttendanceReportGroupingType.Monthly => records
                    .GroupBy(record => new DateTime(record.AttendanceDate.Year, record.AttendanceDate.Month, 1, 0, 0, 0, DateTimeKind.Utc))
                    .OrderByDescending(group => group.Key)
                    .Select(group => BuildReportItem(
                        group.Key.ToString("yyyy-MM"),
                        group.Key.ToString("yyyy-MM"),
                        group,
                        group.Key,
                        group.Key.AddMonths(1).AddDays(-1)))
                    .ToList(),
                AttendanceReportGroupingType.Course => records
                    .GroupBy(record => new { record.CourseId, record.CourseName })
                    .OrderBy(group => group.Key.CourseName)
                    .Select(group => BuildReportItem(
                        group.Key.CourseId.ToString(),
                        group.Key.CourseName,
                        group,
                        group.Min(item => item.AttendanceDate),
                        group.Max(item => item.AttendanceDate),
                        group.Key.CourseId,
                        group.Key.CourseName))
                    .ToList(),
                _ => throw new InvalidOperationException("Unsupported attendance report grouping.")
            };

            return new AttendanceReportResponse
            {
                StartDate = request.StartDate?.Date,
                EndDate = request.EndDate?.Date,
                Filter = request.Filter,
                Grouping = request.Grouping,
                Items = items
            };
        }

        private static DateTime NormalizeAttendanceDate(DateTime attendanceDate)
            => attendanceDate.Kind == DateTimeKind.Utc
                ? attendanceDate.Date
                : DateTime.SpecifyKind(attendanceDate.Date, DateTimeKind.Utc);

        private static void NormalizeStudentAttendance(UpsertStudentCourseAttendanceRequest student)
        {
            student.LateArrivalTime = student.LateArrivalTime.HasValue
                ? NormalizeDateTime(student.LateArrivalTime.Value)
                : null;
            student.EarlyPickupTime = student.EarlyPickupTime.HasValue
                ? NormalizeDateTime(student.EarlyPickupTime.Value)
                : null;
        }

        private static DateTime NormalizeDateTime(DateTime value)
            => value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);

        private static void ValidatePickup(UpsertStudentCourseAttendanceRequest student, TeacherCourseGroupStudentResponse rosterStudent)
        {
            if (student.LateArrivalTime.HasValue && student.AttendanceStatus != AttendanceStatus.Present)
            {
                throw new InvalidOperationException("Late arrival time can only be recorded for a present student.");
            }

            var hasPickupTime = student.EarlyPickupTime.HasValue;
            var hasPickupReference = student.PickupUserId.HasValue || student.PickupOtherContactId.HasValue || student.PickupContactType != PickupContactType.Unknown;

            if (!hasPickupTime && hasPickupReference)
            {
                throw new InvalidOperationException("Pickup contact information requires an early pickup time.");
            }

            if (hasPickupTime && student.PickupContactType == PickupContactType.Unknown)
            {
                throw new InvalidOperationException("Pickup contact type is required when early pickup time is provided.");
            }

            if (!hasPickupTime)
            {
                student.PickupContactType = PickupContactType.Unknown;
                student.PickupUserId = null;
                student.PickupOtherContactId = null;
                return;
            }

            var matchingContact = rosterStudent.PickupContacts.FirstOrDefault(contact =>
                contact.PickupContactType == student.PickupContactType &&
                contact.PickupUserId == student.PickupUserId &&
                contact.PickupOtherContactId == student.PickupOtherContactId);

            if (matchingContact == null)
            {
                throw new InvalidOperationException("Pickup contact is not valid for the selected student.");
            }

            switch (student.PickupContactType)
            {
                case PickupContactType.OtherContact:
                    if (!student.PickupOtherContactId.HasValue || student.PickupUserId.HasValue)
                    {
                        throw new InvalidOperationException("Other contact pickup requires PickupOtherContactId only.");
                    }
                    break;
                case PickupContactType.Mother:
                case PickupContactType.Father:
                case PickupContactType.Guardian:
                    if (!student.PickupUserId.HasValue || student.PickupOtherContactId.HasValue)
                    {
                        throw new InvalidOperationException("Family pickup requires PickupUserId only.");
                    }
                    break;
            }
        }

        private async Task SendAttendanceNotificationEmailsAsync(
            TeacherCourseGroupRosterResponse roster,
            DateTime attendanceDate,
            IReadOnlyList<UpsertStudentCourseAttendanceRequest> students,
            IReadOnlyDictionary<Guid, StudentCourseAttendanceResponse> existingAttendanceByEnrollmentId)
        {
            if (students.Count == 0)
            {
                return;
            }

            var familyEmailsByFamilyId = new Dictionary<Guid, List<string>>();
            var rosterByEnrollmentId = roster.Students.ToDictionary(student => student.StudentCourseEnrollmentId);
            var course = await _courseService.GetCourse(roster.CourseId).ConfigureAwait(false);
            var schoolContacts = course == null
                ? Array.Empty<EmailSchoolContact>()
                : new[] { CreateSchoolContact(course) };

            foreach (var student in students)
            {
                if (!rosterByEnrollmentId.TryGetValue(student.StudentCourseEnrollmentId, out var rosterStudent))
                {
                    continue;
                }

                existingAttendanceByEnrollmentId.TryGetValue(student.StudentCourseEnrollmentId, out var existingAttendance);
                if (!ShouldSendAttendanceNotification(existingAttendance, student))
                {
                    continue;
                }

                if (!familyEmailsByFamilyId.TryGetValue(student.FamilyId, out var targetEmails))
                {
                    targetEmails = await GetFamilyNotificationEmailAddressesAsync(student.FamilyId).ConfigureAwait(false);
                    familyEmailsByFamilyId[student.FamilyId] = targetEmails;
                }

                if (!targetEmails.Any())
                {
                    continue;
                }

                var email = BuildAttendanceNotificationEmail(roster, rosterStudent, attendanceDate, student);
                await _sendEmailService.SendBulkEmail(new MultiUserEmailData
                {
                    To = targetEmails,
                    Subject = email.Subject,
                    Body = email.Body,
                    SchoolContacts = schoolContacts
                }).ConfigureAwait(false);
            }
        }

        private static EmailSchoolContact CreateSchoolContact(MaktabDataContracts.Responses.Course.CourseResponseDetailed course)
            => new()
            {
                Name = course.InstituteName,
                NameFr = course.InstituteNameFr,
                Email = course.InstituteEmail,
                Phone = course.InstitutePhone
            };

        private async Task<List<string>> GetFamilyNotificationEmailAddressesAsync(Guid familyId)
        {
            var verifiedEmails = await _userService.GetVerifiedFamilyNotificationEmailAddresses(familyId).ConfigureAwait(false);
            if (verifiedEmails?.Any() == true)
            {
                return verifiedEmails.ToList();
            }

            var familyUsers = await _userService.GetAllFamilyUsersInformation(familyId, true).ConfigureAwait(false)
                ?? Enumerable.Empty<MaktabDataContracts.Responses.Users.UserInformationResponse>();

            return familyUsers
                .Where(user =>
                    !user.IfTempUser &&
                    (user.Relationship == Relationship.Mother ||
                     user.Relationship == Relationship.Father ||
                     user.Relationship == Relationship.Guardian ||
                     user.Relationship == Relationship.Self))
                .Select(user => user.Email?.Trim() ?? string.Empty)
                .Where(emailAddress => !string.IsNullOrWhiteSpace(emailAddress))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static bool ShouldSendAttendanceNotification(
            StudentCourseAttendanceResponse? existingAttendance,
            UpsertStudentCourseAttendanceRequest student)
        {
            var currentSignature = BuildAttendanceNotificationSignature(student);
            if (string.IsNullOrEmpty(currentSignature))
            {
                return false;
            }

            return !string.Equals(
                BuildAttendanceNotificationSignature(existingAttendance),
                currentSignature,
                StringComparison.Ordinal);
        }

        private static string BuildAttendanceNotificationSignature(UpsertStudentCourseAttendanceRequest student)
        {
            if (!IsAttendanceNotificationRequired(student))
            {
                return string.Empty;
            }

            return string.Join("|", new[]
            {
                ((int)student.AttendanceStatus).ToString(),
                student.LateArrivalTime?.ToUniversalTime().ToString("O") ?? string.Empty,
                student.EarlyPickupTime?.ToUniversalTime().ToString("O") ?? string.Empty,
                ((int)student.PickupContactType).ToString(),
                student.PickupUserId?.ToString("N") ?? string.Empty,
                student.PickupOtherContactId?.ToString("N") ?? string.Empty
            });
        }

        private static string BuildAttendanceNotificationSignature(StudentCourseAttendanceResponse? student)
        {
            if (student == null || !IsAttendanceNotificationRequired(student))
            {
                return string.Empty;
            }

            return string.Join("|", new[]
            {
                ((int)student.AttendanceStatus).ToString(),
                student.LateArrivalTime?.ToUniversalTime().ToString("O") ?? string.Empty,
                student.EarlyPickupTime?.ToUniversalTime().ToString("O") ?? string.Empty,
                ((int)student.PickupContactType).ToString(),
                student.PickupUserId?.ToString("N") ?? string.Empty,
                student.PickupOtherContactId?.ToString("N") ?? string.Empty
            });
        }

        private static bool IsAttendanceNotificationRequired(UpsertStudentCourseAttendanceRequest student)
            => student.AttendanceStatus == AttendanceStatus.Absent
                || student.LateArrivalTime.HasValue
                || student.EarlyPickupTime.HasValue;

        private static bool IsAttendanceNotificationRequired(StudentCourseAttendanceResponse student)
            => student.AttendanceStatus == AttendanceStatus.Absent
                || student.LateArrivalTime.HasValue
                || student.EarlyPickupTime.HasValue;

        private static (string Subject, string Body) BuildAttendanceNotificationEmail(
            TeacherCourseGroupRosterResponse roster,
            TeacherCourseGroupStudentResponse student,
            DateTime attendanceDate,
            UpsertStudentCourseAttendanceRequest attendance)
        {
            var attendanceDateLabel = attendanceDate.ToString("yyyy-MM-dd");
            var frenchStatus = BuildFrenchAttendanceStatus(attendance);
            var englishStatus = BuildEnglishAttendanceStatus(attendance);
            var frenchDetails = BuildFrenchAttendanceDetails(attendance);
            var englishDetails = BuildEnglishAttendanceDetails(attendance);
            var frenchGroupLabel = string.IsNullOrWhiteSpace(roster.GroupTitleFr) ? roster.GroupTitle : roster.GroupTitleFr;
            var englishGroupLabel = string.IsNullOrWhiteSpace(roster.GroupTitle) ? roster.GroupTitleFr : roster.GroupTitle;

            return (
                $"Presence update for {student.ChildName} / Mise a jour de presence pour {student.ChildName}",
                $"<p><strong>Assalaamu alaikum,</strong></p>" +
                $"<p>Nous souhaitons vous informer que <strong>{student.ChildName}</strong> a ete note comme <strong>{frenchStatus}</strong> le {attendanceDateLabel} pour le cours <strong>{(string.IsNullOrWhiteSpace(roster.CourseNameFr) ? roster.CourseName : roster.CourseNameFr)}</strong>{BuildGroupClause(frenchGroupLabel, true)}.</p>" +
                frenchDetails +
                $"<div>&nbsp;</div>" +
                $"<p>We would like to inform you that <strong>{student.ChildName}</strong> was marked as <strong>{englishStatus}</strong> on {attendanceDateLabel} for the course <strong>{(string.IsNullOrWhiteSpace(roster.CourseName) ? roster.CourseNameFr : roster.CourseName)}</strong>{BuildGroupClause(englishGroupLabel, false)}.</p>" +
                englishDetails);
        }

        private static string BuildFrenchAttendanceStatus(UpsertStudentCourseAttendanceRequest attendance)
        {
            if (attendance.AttendanceStatus == AttendanceStatus.Absent)
            {
                return "absent";
            }

            if (attendance.LateArrivalTime.HasValue && attendance.EarlyPickupTime.HasValue)
            {
                return "en retard avec depart anticipe";
            }

            if (attendance.LateArrivalTime.HasValue)
            {
                return "en retard";
            }

            if (attendance.EarlyPickupTime.HasValue)
            {
                return "parti plus tot";
            }

            return "present";
        }

        private static string BuildEnglishAttendanceStatus(UpsertStudentCourseAttendanceRequest attendance)
        {
            if (attendance.AttendanceStatus == AttendanceStatus.Absent)
            {
                return "absent";
            }

            if (attendance.LateArrivalTime.HasValue && attendance.EarlyPickupTime.HasValue)
            {
                return "late arrival with early departure";
            }

            if (attendance.LateArrivalTime.HasValue)
            {
                return "late";
            }

            if (attendance.EarlyPickupTime.HasValue)
            {
                return "early departure";
            }

            return "present";
        }

        private static string BuildFrenchAttendanceDetails(UpsertStudentCourseAttendanceRequest attendance)
        {
            var details = new List<string>();

            if (attendance.LateArrivalTime.HasValue)
            {
                details.Add($"<li>Heure d'arrivee: {FormatMontrealTime(attendance.LateArrivalTime.Value)}</li>");
            }

            if (attendance.EarlyPickupTime.HasValue)
            {
                details.Add($"<li>Heure de depart: {FormatMontrealTime(attendance.EarlyPickupTime.Value)}</li>");
            }

            if (!string.IsNullOrWhiteSpace(attendance.Notes))
            {
                details.Add($"<li>Note: {attendance.Notes}</li>");
            }

            return details.Count == 0
                ? string.Empty
                : $"<ul>{string.Join(string.Empty, details)}</ul>";
        }

        private static string BuildEnglishAttendanceDetails(UpsertStudentCourseAttendanceRequest attendance)
        {
            var details = new List<string>();

            if (attendance.LateArrivalTime.HasValue)
            {
                details.Add($"<li>Arrival time: {FormatMontrealTime(attendance.LateArrivalTime.Value)}</li>");
            }

            if (attendance.EarlyPickupTime.HasValue)
            {
                details.Add($"<li>Departure time: {FormatMontrealTime(attendance.EarlyPickupTime.Value)}</li>");
            }

            if (!string.IsNullOrWhiteSpace(attendance.Notes))
            {
                details.Add($"<li>Note: {attendance.Notes}</li>");
            }

            return details.Count == 0
                ? string.Empty
                : $"<ul>{string.Join(string.Empty, details)}</ul>";
        }

        private static string BuildGroupClause(string groupTitle, bool isFrench)
        {
            if (string.IsNullOrWhiteSpace(groupTitle))
            {
                return string.Empty;
            }

            return isFrench
                ? $" dans le groupe <strong>{groupTitle}</strong>"
                : $" in group <strong>{groupTitle}</strong>";
        }

        private static string FormatMontrealTime(DateTime value)
        {
            var timeZone = GetMontrealTimeZone();
            var utcValue = value.Kind == DateTimeKind.Utc
                ? value
                : DateTime.SpecifyKind(value, DateTimeKind.Utc);
            var localTime = TimeZoneInfo.ConvertTimeFromUtc(utcValue, timeZone);
            return $"{localTime:HH:mm} Montreal";
        }

        private static TimeZoneInfo GetMontrealTimeZone()
        {
            foreach (var timeZoneId in new[] { "Eastern Standard Time", "America/Toronto" })
            {
                try
                {
                    return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
                }
                catch (TimeZoneNotFoundException)
                {
                }
                catch (InvalidTimeZoneException)
                {
                }
            }

            return TimeZoneInfo.Utc;
        }

        private async Task<IReadOnlyCollection<Guid>?> ResolveAccessibleGroupIds(Guid userId, UserRoleType userRoles, GetAttendanceRecordsRequest request)
        {
            if (StaffAssignmentRoleHelper.HasElevatedAccess(userRoles))
            {
                return null;
            }

            var assignedGroups = (await _courseStaffAssignmentService.GetAssignedCourseGroups(userId, onlyActive: false).ConfigureAwait(false)).ToList();
            if (!assignedGroups.Any())
            {
                return Array.Empty<Guid>();
            }

            if (request.CourseEnrollmentGroupId.HasValue && request.CourseEnrollmentGroupId.Value != Guid.Empty)
            {
                var hasAccessToGroup = assignedGroups.Any(group => group.CourseEnrollmentGroupId == request.CourseEnrollmentGroupId.Value);
                if (!hasAccessToGroup)
                {
                    throw new InvalidOperationException("You are not assigned to the selected course group.");
                }
            }

            if (request.CourseId.HasValue && request.CourseId.Value != Guid.Empty)
            {
                var hasAccessToCourse = assignedGroups.Any(group => group.CourseId == request.CourseId.Value);
                if (!hasAccessToCourse)
                {
                    throw new InvalidOperationException("You are not assigned to the selected course.");
                }
            }

            return assignedGroups
                .Where(group => !request.CourseId.HasValue || request.CourseId.Value == Guid.Empty || group.CourseId == request.CourseId.Value)
                .Select(group => group.CourseEnrollmentGroupId)
                .Distinct()
                .ToList();
        }

        private static GetAttendanceRecordsRequest NormalizeRecordsRequest(GetAttendanceRecordsRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            return new GetAttendanceRecordsRequest
            {
                FamilyId = request.FamilyId,
                ChildId = request.ChildId,
                CourseId = request.CourseId,
                CourseEnrollmentGroupId = request.CourseEnrollmentGroupId,
                StartDate = request.StartDate.HasValue
                    ? NormalizeAttendanceDate(request.StartDate.Value)
                    : null,
                EndDate = request.EndDate.HasValue
                    ? NormalizeAttendanceDate(request.EndDate.Value)
                    : null,
                Filter = request.Filter
            };
        }

        private static IReadOnlyList<AttendanceRecordResponse> ApplyAttendanceFilter(
            IReadOnlyList<AttendanceRecordResponse> records,
            AttendanceRecordFilterType filter)
        {
            return filter switch
            {
                AttendanceRecordFilterType.All => records,
                AttendanceRecordFilterType.Absent => records
                    .Where(record => record.AttendanceStatus == AttendanceStatus.Absent)
                    .ToList(),
                AttendanceRecordFilterType.LateArrival => records
                    .Where(record => record.LateArrivalTime.HasValue)
                    .ToList(),
                AttendanceRecordFilterType.EarlyDeparture => records
                    .Where(record => record.EarlyPickupTime.HasValue)
                    .ToList(),
                AttendanceRecordFilterType.OnTimePresence => records
                    .Where(record =>
                        record.AttendanceStatus == AttendanceStatus.Present &&
                        !record.LateArrivalTime.HasValue &&
                        !record.EarlyPickupTime.HasValue)
                    .ToList(),
                _ => records
            };
        }

        private static AttendanceReportItemResponse BuildReportItem(
            string groupKey,
            string groupLabel,
            IEnumerable<AttendanceRecordResponse> records,
            DateTime? periodStart,
            DateTime? periodEnd,
            Guid? courseId = null,
            string courseName = "")
        {
            var items = records.ToList();
            return new AttendanceReportItemResponse
            {
                GroupKey = groupKey,
                GroupLabel = groupLabel,
                CourseId = courseId,
                CourseName = courseName,
                PeriodStart = periodStart,
                PeriodEnd = periodEnd,
                TotalRecords = items.Count,
                PresentCount = items.Count(item => item.AttendanceStatus == AttendanceStatus.Present),
                AbsentCount = items.Count(item => item.AttendanceStatus == AttendanceStatus.Absent),
                LateArrivalCount = items.Count(item => item.LateArrivalTime.HasValue),
                EarlyDepartureCount = items.Count(item => item.EarlyPickupTime.HasValue),
                OnTimePresenceCount = items.Count(item =>
                    item.AttendanceStatus == AttendanceStatus.Present &&
                    !item.LateArrivalTime.HasValue &&
                    !item.EarlyPickupTime.HasValue)
            };
        }

        private static DateTime GetWeekStart(DateTime date)
        {
            var normalizedDate = date.Date;
            var offset = ((int)normalizedDate.DayOfWeek + 6) % 7;
            return normalizedDate.AddDays(-offset);
        }
    }
}
