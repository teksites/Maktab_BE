using Courses.Repository;
using Courses.Services;
using MaktabDataContracts.Enums;
using MaktabDataContracts.Requests.Course;
using MaktabDataContracts.Responses.Attendance;
using MaktabDataContracts.Responses.Course;
using Users.Services;

namespace Courses.Implementation.Services
{
    public class CourseStaffAssignmentService : ICourseStaffAssignmentService
    {
        private readonly ICourseStaffAssignmentRepository _repository;
        private readonly ICourseService _courseService;
        private readonly ICourseEnrollmentGroupService _courseEnrollmentGroupService;
        private readonly IInstituteStaffAssignmentService _instituteStaffAssignmentService;
        private readonly IStudentCourseEnrollmentService _studentCourseEnrollmentService;
        private readonly IOtherContactsService _otherContactsService;

        public CourseStaffAssignmentService(
            ICourseStaffAssignmentRepository repository,
            ICourseService courseService,
            ICourseEnrollmentGroupService courseEnrollmentGroupService,
            IInstituteStaffAssignmentService instituteStaffAssignmentService,
            IStudentCourseEnrollmentService studentCourseEnrollmentService,
            IOtherContactsService otherContactsService)
        {
            _repository = repository;
            _courseService = courseService;
            _courseEnrollmentGroupService = courseEnrollmentGroupService;
            _instituteStaffAssignmentService = instituteStaffAssignmentService;
            _studentCourseEnrollmentService = studentCourseEnrollmentService;
            _otherContactsService = otherContactsService;
        }

        public async Task<CourseStaffAssignmentsResponse> GetCourseAssignments(Guid courseId, bool onlyActive = true)
        {
            var course = await _courseService.GetCourse(courseId).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Course was not found.");

            var assignments = await _repository.GetCourseAssignments(courseId, onlyActive).ConfigureAwait(false);
            return new CourseStaffAssignmentsResponse
            {
                CourseId = courseId,
                InstituteId = course.InstituteId,
                StaffAssignments = assignments.OrderBy(item => item.StartDate).ThenBy(item => item.StaffUser.FirstName).ToList()
            };
        }

        public async Task<CourseStaffAssignmentsResponse> SetCourseAssignments(SetCourseStaffAssignmentsRequest request)
        {
            var course = await _courseService.GetCourse(request.CourseId).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Course was not found.");

            if (course.InstituteId != request.InstituteId)
            {
                throw new InvalidOperationException("Course does not belong to the provided institute.");
            }

            var assignments = request.StaffAssignments ?? new List<CourseStaffAssignmentItemRequest>();

            if (assignments.Any(item => item.IsActive) && await _repository.HasActiveDirectGroupAssignments(request.CourseId).ConfigureAwait(false))
            {
                throw new InvalidOperationException("This course already has active group-level staff assignments. Clear group-level assignments before assigning staff at course level.");
            }

            var existingAssignments = await _repository.GetCourseAssignments(request.CourseId, onlyActive: false).ConfigureAwait(false);
            EnsureKnownAssignmentIds(assignments, existingAssignments.Select(item => item.CourseStaffAssignmentId));
            await ValidateCourseAssignments(request.InstituteId, assignments, existingAssignments.Select(item => (item.StaffUser.UserId, item.StartDate, item.EndDate, item.IsActive, item.CourseStaffAssignmentId, item.AssignmentRoles)).ToList()).ConfigureAwait(false);

            return await _repository.ReplaceCourseAssignments(Normalize(request)).ConfigureAwait(false);
        }

        public async Task<CourseGroupStaffAssignmentsResponse> GetCourseGroupAssignments(Guid courseEnrollmentGroupId, bool onlyActive = true)
        {
            var group = await _courseEnrollmentGroupService.GetCourseGroup(courseEnrollmentGroupId).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Course enrollment group was not found.");

            var activeCourseAssignments = await _repository.GetCourseAssignments(group.CourseId, onlyActive: true).ConfigureAwait(false);
            if (activeCourseAssignments.Any())
            {
                return new CourseGroupStaffAssignmentsResponse
                {
                    CourseEnrollmentGroupId = courseEnrollmentGroupId,
                    CourseId = group.CourseId,
                    InstituteId = group.InstituteId,
                    UsesCourseLevelAssignments = true,
                    StaffAssignments = activeCourseAssignments
                        .Select(item => MapInheritedCourseAssignment(item, courseEnrollmentGroupId))
                        .OrderBy(item => item.StartDate)
                        .ThenBy(item => item.StaffUser.FirstName)
                        .ToList()
                };
            }

            var directAssignments = await _repository.GetCourseGroupAssignments(courseEnrollmentGroupId, onlyActive).ConfigureAwait(false);
            return new CourseGroupStaffAssignmentsResponse
            {
                CourseEnrollmentGroupId = courseEnrollmentGroupId,
                CourseId = group.CourseId,
                InstituteId = group.InstituteId,
                UsesCourseLevelAssignments = false,
                StaffAssignments = directAssignments.OrderBy(item => item.StartDate).ThenBy(item => item.StaffUser.FirstName).ToList()
            };
        }

        public async Task<CourseGroupStaffAssignmentsResponse> SetCourseGroupAssignments(SetCourseGroupStaffAssignmentsRequest request)
        {
            var group = await _courseEnrollmentGroupService.GetCourseGroup(request.CourseEnrollmentGroupId).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Course enrollment group was not found.");

            if (group.CourseId != request.CourseId)
            {
                throw new InvalidOperationException("Course enrollment group does not belong to the provided course.");
            }

            if (group.InstituteId != request.InstituteId)
            {
                throw new InvalidOperationException("Course enrollment group does not belong to the provided institute.");
            }

            var assignments = request.StaffAssignments ?? new List<CourseStaffAssignmentItemRequest>();
            if (assignments.Any(item => item.IsActive))
            {
                var activeCourseAssignments = await _repository.GetCourseAssignments(request.CourseId, onlyActive: true).ConfigureAwait(false);
                if (activeCourseAssignments.Any())
                {
                    throw new InvalidOperationException("This group inherits active course-level staff assignments. Update the course staff assignments instead.");
                }
            }

            var existingAssignments = await _repository.GetCourseGroupAssignments(request.CourseEnrollmentGroupId, onlyActive: false).ConfigureAwait(false);
            EnsureKnownAssignmentIds(assignments, existingAssignments.Select(item => item.CourseGroupStaffAssignmentId));
            await ValidateCourseAssignments(request.InstituteId, assignments, existingAssignments.Select(item => (item.StaffUser.UserId, item.StartDate, item.EndDate, item.IsActive, item.CourseGroupStaffAssignmentId, item.AssignmentRoles)).ToList()).ConfigureAwait(false);

            return await _repository.ReplaceCourseGroupAssignments(Normalize(request)).ConfigureAwait(false);
        }

        public async Task<IEnumerable<TeacherAssignedCourseGroupResponse>> GetAssignedCourseGroups(Guid userId, bool onlyActive = true)
        {
            var assignedGroups = await _repository.GetAssignedCourseGroups(userId, onlyActive).ConfigureAwait(false);

            return assignedGroups
                .GroupBy(group => group.CourseEnrollmentGroupId)
                .Select(group => new TeacherAssignedCourseGroupResponse
                {
                    CourseId = group.First().CourseId,
                    CourseEnrollmentGroupId = group.Key,
                    InstituteId = group.First().InstituteId,
                    CourseName = group.First().CourseName,
                    CourseNameFr = group.First().CourseNameFr,
                    GroupTitle = group.First().GroupTitle,
                    GroupTitleFr = group.First().GroupTitleFr,
                    CourseStartDate = group.First().CourseStartDate,
                    CourseEndDate = group.First().CourseEndDate,
                    CourseSession = group.First().CourseSession,
                    CanSelectMultipleEnrollmentGroups = group.First().CanSelectMultipleEnrollmentGroups,
                    IsInheritedFromCourse = group.Any(item => item.IsInheritedFromCourse),
                    AssignmentRoles = StaffAssignmentRoleHelper.CombineRoles(group.Select(item => item.AssignmentRoles))
                })
                .OrderBy(group => group.CourseStartDate)
                .ThenBy(group => group.GroupTitle)
                .ToList();
        }

        public async Task<TeacherCourseGroupRosterResponse> GetAssignedCourseGroupRoster(Guid userId, UserRoleType userRoles, Guid courseEnrollmentGroupId)
        {
            var group = await _courseEnrollmentGroupService.GetCourseGroup(courseEnrollmentGroupId).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Course enrollment group was not found.");

            var course = await _courseService.GetCourse(group.CourseId).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Course was not found.");

            TeacherAssignedCourseGroupResponse? teacherAssignment = null;
            if (!StaffAssignmentRoleHelper.HasElevatedAccess(userRoles))
            {
                teacherAssignment = (await GetAssignedCourseGroups(userId, onlyActive: true).ConfigureAwait(false))
                    .FirstOrDefault(item => item.CourseEnrollmentGroupId == courseEnrollmentGroupId);

                if (teacherAssignment == null)
                {
                    throw new InvalidOperationException("You are not assigned to the selected course group.");
                }
            }
            else
            {
                teacherAssignment = (await _repository.GetAssignedCourseGroups(userId, onlyActive: true).ConfigureAwait(false))
                    .Where(item => item.CourseEnrollmentGroupId == courseEnrollmentGroupId)
                    .GroupBy(item => item.CourseEnrollmentGroupId)
                    .Select(grouping => new TeacherAssignedCourseGroupResponse
                    {
                        CourseId = grouping.First().CourseId,
                        CourseEnrollmentGroupId = grouping.Key,
                        InstituteId = grouping.First().InstituteId,
                        CourseName = grouping.First().CourseName,
                        CourseNameFr = grouping.First().CourseNameFr,
                        GroupTitle = grouping.First().GroupTitle,
                        GroupTitleFr = grouping.First().GroupTitleFr,
                        CourseStartDate = grouping.First().CourseStartDate,
                        CourseEndDate = grouping.First().CourseEndDate,
                        CourseSession = grouping.First().CourseSession,
                        CanSelectMultipleEnrollmentGroups = grouping.First().CanSelectMultipleEnrollmentGroups,
                        IsInheritedFromCourse = grouping.Any(item => item.IsInheritedFromCourse),
                        AssignmentRoles = StaffAssignmentRoleHelper.CombineRoles(grouping.Select(item => item.AssignmentRoles))
                    })
                    .FirstOrDefault();
            }

            var enrollments = (await _studentCourseEnrollmentService.GetEnrollmentsByGroup(courseEnrollmentGroupId).ConfigureAwait(false))
                .OrderBy(enrollment => enrollment.EnrollmentIndex)
                .ThenBy(enrollment => enrollment.ChildName)
                .ToList();

            var familyIds = enrollments.Select(enrollment => enrollment.FamilyId).Distinct().ToList();
            var pickupContactsByFamily = new Dictionary<Guid, IReadOnlyList<AttendancePickupContactResponse>>();

            foreach (var familyId in familyIds)
            {
                pickupContactsByFamily[familyId] = await BuildPickupContacts(familyId, enrollments).ConfigureAwait(false);
            }

            return new TeacherCourseGroupRosterResponse
            {
                CourseId = course.CourseId,
                CourseEnrollmentGroupId = group.CourseEnrollmentGroupId,
                InstituteId = group.InstituteId,
                CourseName = course.Name,
                CourseNameFr = course.NameFr,
                GroupTitle = group.GroupTitle,
                GroupTitleFr = group.GroupTitleFr,
                CourseStartDate = course.StartDate,
                CourseEndDate = course.EndDate,
                CourseSession = course.CourseSession,
                CanSelectMultipleEnrollmentGroups = course.CanSelectMultipleEnrollmentGroups,
                UsesCourseLevelAssignments = teacherAssignment?.IsInheritedFromCourse ?? (await _repository.GetCourseAssignments(course.CourseId, true).ConfigureAwait(false)).Any(),
                AssignmentRoles = teacherAssignment?.AssignmentRoles ?? UserRoleType.None,
                Students = enrollments.Select(enrollment => new TeacherCourseGroupStudentResponse
                {
                    StudentCourseEnrollmentId = enrollment.StudentCourseEnrollmentId,
                    ChildId = enrollment.ChildId,
                    FamilyId = enrollment.FamilyId,
                    ChildName = enrollment.ChildName,
                    RegistrationNumber = enrollment.RegistrationNumber,
                    Consent = enrollment.Consent,
                    IsActive = enrollment.IsActive,
                    WillUseDayCare = enrollment.WillUseDayCare,
                    DayCareDays = enrollment.DayCareDays,
                    EnrollmentStatus = enrollment.EnrollmentStatus,
                    PickupContacts = pickupContactsByFamily.TryGetValue(enrollment.FamilyId, out var contacts)
                        ? contacts.ToList()
                        : new List<AttendancePickupContactResponse>()
                }).ToList()
            };
        }

        private async Task ValidateCourseAssignments(
            Guid instituteId,
            IReadOnlyCollection<CourseStaffAssignmentItemRequest> assignments,
            IReadOnlyCollection<(Guid UserId, DateTime StartDate, DateTime? EndDate, bool IsActive, Guid AssignmentId, UserRoleType AssignmentRoles)> existingAssignments)
        {
            foreach (var assignment in assignments)
            {
                if (assignment.UserId == Guid.Empty)
                {
                    throw new InvalidOperationException("UserId is required for each staff assignment.");
                }

                if (!StaffAssignmentRoleHelper.IsValidCourseAssignmentRoleMask(assignment.AssignmentRoles))
                {
                    throw new InvalidOperationException("Only Assistant and SchoolTeacher are allowed for course and group staff assignments.");
                }

                assignment.StartDate = StaffAssignmentRoleHelper.NormalizeUtc(assignment.StartDate);
                assignment.EndDate = assignment.EndDate.HasValue
                    ? StaffAssignmentRoleHelper.NormalizeUtc(assignment.EndDate.Value)
                    : null;

                if (assignment.EndDate.HasValue && assignment.EndDate.Value < assignment.StartDate)
                {
                    throw new InvalidOperationException("EndDate cannot be earlier than StartDate.");
                }

                var hasInstituteStaffCoverage = await _instituteStaffAssignmentService
                    .HasEligibleInstituteStaffAssignment(instituteId, assignment.UserId, assignment.AssignmentRoles, assignment.StartDate)
                    .ConfigureAwait(false);

                if (!hasInstituteStaffCoverage)
                {
                    throw new InvalidOperationException("Each teacher or assistant must have an active institute staff assignment covering the requested role and start date.");
                }
            }

            EnsureNoOverlappingAssignments(assignments);

        }

        private static void EnsureNoOverlappingAssignments(IEnumerable<CourseStaffAssignmentItemRequest> assignments)
        {
            foreach (var group in assignments.Where(item => item.IsActive).GroupBy(item => item.UserId))
            {
                var items = group.OrderBy(item => item.StartDate).ToList();
                for (var index = 0; index < items.Count; index++)
                {
                    for (var compareIndex = index + 1; compareIndex < items.Count; compareIndex++)
                    {
                        if (StaffAssignmentRoleHelper.RangesOverlap(
                            items[index].StartDate,
                            items[index].EndDate,
                            items[compareIndex].StartDate,
                            items[compareIndex].EndDate))
                        {
                            throw new InvalidOperationException("Overlapping active staff assignments are not allowed for the same user.");
                        }
                    }
                }
            }
        }

        private static void EnsureKnownAssignmentIds(IEnumerable<CourseStaffAssignmentItemRequest> assignments, IEnumerable<Guid> knownAssignmentIds)
        {
            var knownIds = knownAssignmentIds.ToHashSet();
            var unknownId = assignments
                .Where(item => item.AssignmentId.HasValue)
                .Select(item => item.AssignmentId!.Value)
                .FirstOrDefault(id => !knownIds.Contains(id));

            if (unknownId != Guid.Empty)
            {
                throw new InvalidOperationException("One or more assignment ids do not belong to the selected course or enrollment group.");
            }
        }

        private static CourseGroupStaffAssignmentResponse MapInheritedCourseAssignment(CourseStaffAssignmentResponse assignment, Guid courseEnrollmentGroupId)
        {
            return new CourseGroupStaffAssignmentResponse
            {
                CourseGroupStaffAssignmentId = assignment.CourseStaffAssignmentId,
                CourseEnrollmentGroupId = courseEnrollmentGroupId,
                CourseId = assignment.CourseId,
                InstituteId = assignment.InstituteId,
                IsInheritedFromCourse = true,
                AssignmentRoles = assignment.AssignmentRoles,
                StartDate = assignment.StartDate,
                EndDate = assignment.EndDate,
                IsActive = assignment.IsActive,
                CreatedAt = assignment.CreatedAt,
                UpdatedOn = assignment.UpdatedOn,
                StaffUser = assignment.StaffUser
            };
        }

        private static SetCourseStaffAssignmentsRequest Normalize(SetCourseStaffAssignmentsRequest request)
        {
            foreach (var assignment in request.StaffAssignments)
            {
                assignment.StartDate = StaffAssignmentRoleHelper.NormalizeUtc(assignment.StartDate);
                assignment.EndDate = assignment.EndDate.HasValue
                    ? StaffAssignmentRoleHelper.NormalizeUtc(assignment.EndDate.Value)
                    : null;
            }

            return request;
        }

        private static SetCourseGroupStaffAssignmentsRequest Normalize(SetCourseGroupStaffAssignmentsRequest request)
        {
            foreach (var assignment in request.StaffAssignments)
            {
                assignment.StartDate = StaffAssignmentRoleHelper.NormalizeUtc(assignment.StartDate);
                assignment.EndDate = assignment.EndDate.HasValue
                    ? StaffAssignmentRoleHelper.NormalizeUtc(assignment.EndDate.Value)
                    : null;
            }

            return request;
        }

        private async Task<IReadOnlyList<AttendancePickupContactResponse>> BuildPickupContacts(
            Guid familyId,
            IReadOnlyCollection<StudentCourseEnrollmentResponse> enrollments)
        {
            var familyMembers = enrollments
                .Where(enrollment => enrollment.FamilyId == familyId)
                .SelectMany(enrollment => enrollment.FamilyMembers)
                .Where(member =>
                    member.Relationship == Relationship.Mother ||
                    member.Relationship == Relationship.Father ||
                    member.Relationship == Relationship.Guardian ||
                    member.Relationship == Relationship.Self)
                .GroupBy(member => member.UserId)
                .Select(group => group.First())
                .Select(member => new AttendancePickupContactResponse
                {
                    PickupContactType = member.Relationship switch
                    {
                        Relationship.Mother => PickupContactType.Mother,
                        Relationship.Father => PickupContactType.Father,
                        Relationship.Guardian => PickupContactType.Guardian,
                        _ => PickupContactType.Unknown
                    },
                    PickupUserId = member.UserId,
                    DisplayName = member.UserName,
                    Phone = member.Phone,
                    Relationship = member.Relationship
                })
                .ToList();

            var otherContacts = await _otherContactsService
                .GetFamilyOtherContacts(familyId, new[] { ContactType.Pickup })
                .ConfigureAwait(false);

            familyMembers.AddRange(otherContacts.Select(contact => new AttendancePickupContactResponse
            {
                PickupContactType = PickupContactType.OtherContact,
                PickupOtherContactId = contact.ContactId,
                DisplayName = $"{contact.FirstName} {contact.LastName}".Trim(),
                Phone = contact.Phone,
                Relationship = contact.Relationship
            }));

            return familyMembers;
        }
    }
}
