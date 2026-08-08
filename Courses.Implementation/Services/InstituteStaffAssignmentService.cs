using Courses.Repository;
using Courses.Services;
using MaktabDataContracts.Enums;
using MaktabDataContracts.Requests.InstituteStaff;
using MaktabDataContracts.Responses.InstituteStaff;

namespace Courses.Implementation.Services
{
    public class InstituteStaffAssignmentService : IInstituteStaffAssignmentService
    {
        private readonly IInstituteStaffAssignmentRepository _repository;
        private readonly IInstituteService _instituteService;

        public InstituteStaffAssignmentService(
            IInstituteStaffAssignmentRepository repository,
            IInstituteService instituteService)
        {
            _repository = repository;
            _instituteService = instituteService;
        }

        public async Task<InstituteStaffAssignmentResponse> AddAssignment(AddInstituteStaffAssignmentRequest request)
        {
            await ValidateAssignmentRequest(request.InstituteId, request.UserId, request.StaffRoles, request.StartDate, request.EndDate).ConfigureAwait(false);

            var existingAssignments = await _repository
                .GetAssignments(request.InstituteId, request.UserId, onlyActive: false)
                .ConfigureAwait(false);

            EnsureNoOverlappingAssignments(existingAssignments, request.StartDate, request.EndDate, request.IsActive, null);

            return await _repository.AddAssignment(Normalize(request)).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Unable to create institute staff assignment.");
        }

        public async Task<InstituteStaffAssignmentResponse> UpdateAssignment(Guid assignmentId, UpdateInstituteStaffAssignmentRequest request)
        {
            request.InstituteStaffAssignmentId = assignmentId;

            var existingAssignment = await _repository.GetAssignment(assignmentId).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Institute staff assignment was not found.");

            if (existingAssignment.InstituteId != request.InstituteId)
            {
                throw new InvalidOperationException("Institute cannot be changed for an existing staff assignment.");
            }

            if (existingAssignment.StaffUser.UserId != request.UserId)
            {
                throw new InvalidOperationException("Assigned user cannot be changed for an existing staff assignment.");
            }

            await ValidateAssignmentRequest(request.InstituteId, request.UserId, request.StaffRoles, request.StartDate, request.EndDate).ConfigureAwait(false);

            var existingAssignments = await _repository
                .GetAssignments(request.InstituteId, request.UserId, onlyActive: false)
                .ConfigureAwait(false);

            EnsureNoOverlappingAssignments(existingAssignments, request.StartDate, request.EndDate, request.IsActive, assignmentId);

            return await _repository.UpdateAssignment(Normalize(request)).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Unable to update institute staff assignment.");
        }

        public Task<bool> DeleteAssignment(Guid assignmentId, bool hardDelete = false)
            => _repository.DeleteAssignment(assignmentId, hardDelete);

        public async Task<IEnumerable<InstituteStaffAssignmentResponse>> GetAssignments(GetInstituteStaffRequest request)
        {
            await EnsureInstituteExists(request.InstituteId).ConfigureAwait(false);

            var normalized = new GetInstituteStaffRequest
            {
                InstituteId = request.InstituteId,
                OnlyActive = request.OnlyActive,
                EffectiveOn = request.EffectiveOn.HasValue
                    ? StaffAssignmentRoleHelper.NormalizeUtc(request.EffectiveOn.Value)
                    : null,
                RoleFilter = request.RoleFilter,
                SearchText = request.SearchText?.Trim() ?? string.Empty
            };

            return await _repository.GetAssignments(normalized).ConfigureAwait(false);
        }

        public async Task<IEnumerable<InstituteStaffCandidateResponse>> GetCandidates(GetInstituteStaffCandidatesRequest request)
        {
            await EnsureInstituteExists(request.InstituteId).ConfigureAwait(false);

            var candidateUsers = await _repository.GetCandidateUsers(new GetInstituteStaffCandidatesRequest
            {
                InstituteId = request.InstituteId,
                OnlyActive = request.OnlyActive,
                RoleFilter = UserRoleType.None,
                SearchText = request.SearchText?.Trim() ?? string.Empty
            }).ConfigureAwait(false);

            var filteredCandidates = request.RoleFilter == UserRoleType.None
                ? candidateUsers
                : candidateUsers.Where(candidate => StaffAssignmentRoleHelper.CanCoverRequestedRoles(candidate.GlobalUserRoles, request.RoleFilter)).ToList();

            var activeAssignments = await _repository.GetAssignments(new GetInstituteStaffRequest
            {
                InstituteId = request.InstituteId,
                OnlyActive = true,
                EffectiveOn = DateTime.UtcNow
            }).ConfigureAwait(false);

            var currentRolesByUser = activeAssignments
                .GroupBy(assignment => assignment.StaffUser.UserId)
                .ToDictionary(
                    group => group.Key,
                    group => StaffAssignmentRoleHelper.CombineRoles(group.Select(item => item.StaffRoles)));

            return filteredCandidates
                .Select(candidate => new InstituteStaffCandidateResponse
                {
                    StaffUser = candidate,
                    IsAlreadyAssigned = currentRolesByUser.TryGetValue(candidate.UserId, out var roles) && roles != UserRoleType.None,
                    CurrentStaffRoles = currentRolesByUser.TryGetValue(candidate.UserId, out roles) ? roles : UserRoleType.None
                })
                .OrderBy(candidate => candidate.StaffUser.FirstName)
                .ThenBy(candidate => candidate.StaffUser.LastName)
                .ToList();
        }

        public async Task<bool> HasEligibleInstituteStaffAssignment(Guid instituteId, Guid userId, UserRoleType requiredRoles, DateTime effectiveOn)
        {
            if (requiredRoles == UserRoleType.None)
            {
                return false;
            }

            var assignments = await _repository.GetAssignments(new GetInstituteStaffRequest
            {
                InstituteId = instituteId,
                OnlyActive = true,
                EffectiveOn = StaffAssignmentRoleHelper.NormalizeUtc(effectiveOn)
            }).ConfigureAwait(false);

            return assignments.Any(assignment =>
                assignment.StaffUser.UserId == userId &&
                StaffAssignmentRoleHelper.CanCoverRequestedRoles(assignment.StaffRoles, requiredRoles));
        }

        private async Task ValidateAssignmentRequest(Guid instituteId, Guid userId, UserRoleType staffRoles, DateTime startDate, DateTime? endDate)
        {
            if (instituteId == Guid.Empty)
            {
                throw new InvalidOperationException("InstituteId is required.");
            }

            if (userId == Guid.Empty)
            {
                throw new InvalidOperationException("UserId is required.");
            }

            if (!StaffAssignmentRoleHelper.IsValidInstituteStaffRoleMask(staffRoles))
            {
                throw new InvalidOperationException("Only Assistant, SchoolTeacher, SchoolSupervisor, SchoolAdmin, and Manager are allowed institute staff roles.");
            }

            var normalizedStartDate = StaffAssignmentRoleHelper.NormalizeUtc(startDate);
            DateTime? normalizedEndDate = endDate.HasValue
                ? StaffAssignmentRoleHelper.NormalizeUtc(endDate.Value)
                : null;

            if (normalizedEndDate.HasValue && normalizedEndDate.Value < normalizedStartDate)
            {
                throw new InvalidOperationException("EndDate cannot be earlier than StartDate.");
            }

            await EnsureInstituteExists(instituteId).ConfigureAwait(false);

            var userSummary = await _repository.GetUserSummary(userId).ConfigureAwait(false)
                ?? throw new InvalidOperationException("The selected user does not exist.");

            if (!StaffAssignmentRoleHelper.IsEligibleCandidate(userSummary.GlobalUserRoles))
            {
                throw new InvalidOperationException("The selected user is not eligible for school staff assignment.");
            }

            if (!StaffAssignmentRoleHelper.CanCoverRequestedRoles(userSummary.GlobalUserRoles, staffRoles))
            {
                throw new InvalidOperationException("The selected user does not have sufficient global role access for the requested school staff role.");
            }
        }

        private async Task EnsureInstituteExists(Guid instituteId)
        {
            var institute = await _instituteService.GetInstitute(instituteId).ConfigureAwait(false);
            if (institute == null)
            {
                throw new InvalidOperationException("Institute was not found.");
            }
        }

        private static void EnsureNoOverlappingAssignments(
            IEnumerable<InstituteStaffAssignmentResponse> assignments,
            DateTime startDate,
            DateTime? endDate,
            bool isActive,
            Guid? assignmentIdToIgnore)
        {
            if (!isActive)
            {
                return;
            }

            var normalizedStartDate = StaffAssignmentRoleHelper.NormalizeUtc(startDate);
            DateTime? normalizedEndDate = endDate.HasValue
                ? StaffAssignmentRoleHelper.NormalizeUtc(endDate.Value)
                : null;

            var hasOverlap = assignments.Any(assignment =>
                assignment.IsActive &&
                (!assignmentIdToIgnore.HasValue || assignment.InstituteStaffAssignmentId != assignmentIdToIgnore.Value) &&
                StaffAssignmentRoleHelper.RangesOverlap(
                    assignment.StartDate,
                    assignment.EndDate,
                    normalizedStartDate,
                    normalizedEndDate));

            if (hasOverlap)
            {
                throw new InvalidOperationException("An overlapping active institute staff assignment already exists for this user and school.");
            }
        }

        private static AddInstituteStaffAssignmentRequest Normalize(AddInstituteStaffAssignmentRequest request)
        {
            request.StartDate = StaffAssignmentRoleHelper.NormalizeUtc(request.StartDate);
            request.EndDate = request.EndDate.HasValue
                ? StaffAssignmentRoleHelper.NormalizeUtc(request.EndDate.Value)
                : null;

            return request;
        }

        private static UpdateInstituteStaffAssignmentRequest Normalize(UpdateInstituteStaffAssignmentRequest request)
        {
            request.StartDate = StaffAssignmentRoleHelper.NormalizeUtc(request.StartDate);
            request.EndDate = request.EndDate.HasValue
                ? StaffAssignmentRoleHelper.NormalizeUtc(request.EndDate.Value)
                : null;

            return request;
        }
    }
}
