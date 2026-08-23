using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Maktab.Attributes;
using MaktabDataContracts.Requests.Users;
using MaktabDataContracts.Responses.Users;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Web;
using Users.Contracts;
using Users.Services;
using Application.Users.Contracts;

namespace Maktab.Controllers
{

    [Route("api/users")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly IExtendedUserInformationService _extendedUserInformationService;
        private readonly IDataAccessVerificationService _dataAccessVerificationService;
        //If its success, we will update the DB with success


        private readonly ILogger<UsersController> _logger;

        public UsersController(
            IUserService userService,
            IExtendedUserInformationService extendedUserInformationService,
            IDataAccessVerificationService dataAccessVerificationService,
            ILogger<UsersController> logger)
        {
            _userService = userService;
            _extendedUserInformationService = extendedUserInformationService;
            _dataAccessVerificationService = dataAccessVerificationService;
            _logger = logger;
        }

        [Authorize]
        [ApiAuthorize()]
        [HttpGet("")]
        [EnableCors("corspolicy")]
        public async Task<IEnumerable<UserInformationResponse>> GetAllUsers(bool ifOnlyActive = true)
        {
            return await _userService.GetAllUsersInformation(ifOnlyActive).ConfigureAwait(false);
        }

        [Authorize]
        [ApiAuthorize()]
        [HttpGet("family/{familyId:guid}")]
        [EnableCors("corspolicy")]
        public async Task<IEnumerable<UserInformationResponse>> GetAllFamilyUsers(Guid familyId, bool ifOnlyActive = true)
        {
            return await _userService.GetAllFamilyUsersInformation(familyId, ifOnlyActive).ConfigureAwait(false);
        }

        [Authorize]
        [ApiAuthorize()]
        [HttpGet("family/{familyId:guid}/information")]
        [EnableCors("corspolicy")]
        public async Task<FamilyInformationDetailsResponse> GetFamilyInformation(Guid familyId)
        {
            return await _userService.GetFamilyInformation(familyId).ConfigureAwait(false);
        }

        [Authorize]
        [ApiAuthorize(true)]
        [HttpGet("{userId:guid}")]
        [EnableCors("corspolicy")]
        public async Task<UserInformationResponse> GetUserInformation(Guid userId)
        {
            return await _userService.GetUserInformation(userId).ConfigureAwait(false);
        }

        [Authorize]
        [ApiAuthorize(false, false, MaktabDataContracts.Enums.UserRoleType.Normal)]
        [HttpGet("{email}")]
        [EnableCors("corspolicy")]
        public async Task<ActionResult<UserInformationResponse>> GetUserInformationByEmail(string email)
        {
            var decodedEmail = HttpUtility.UrlDecode(email)?.Trim();
            if (string.IsNullOrWhiteSpace(decodedEmail))
            {
                return BadRequest("Email is required.");
            }

            var userInformation = await _userService.GetUserInformationByEmail(decodedEmail).ConfigureAwait(false);
            if (userInformation == null)
            {
                return NotFound();
            }

            var session = await GetRequiredSessionContext().ConfigureAwait(false);
            if (!_dataAccessVerificationService.HasElevatedAccess(session.UserRoles)
                && userInformation.FamilyId != session.FamilyId)
            {
                return Forbid();
            }

            return Ok(userInformation);
        }

        [HttpGet("registered")]
        [EnableCors("corspolicy")]
        public async Task<bool> CheckIfUserRegistered(string email, string phone)
        {
            var decodedEmail = HttpUtility.UrlDecode(email);
            return await _userService.CheckIfUserAlreadyRegistered(decodedEmail, phone).ConfigureAwait(false);
        }

        [ApiAuthorize(true, true)]
        [HttpGet("checkuser")]
        [EnableCors("corspolicy")]
        public async Task<bool> CheckIfUserNameExisit(string userName)
        {
            return await _userService.CheckIfUserNameExisit(userName).ConfigureAwait(false);
        }

        //[ApiAuthorize(true, true)]
        [HttpPost("familyinfo")]
        [EnableCors("corspolicy")]
        public async Task<Guid> GetFamilyIhfo(UserFamilyInformationRequest userInformation)
        {
            return await _userService.GetUserFamilyInformation(userInformation).ConfigureAwait(false);
        }

        //[Authorize]
        [HttpPost("add")]
        [EnableCors("corspolicy")]
        public async Task<UserInformationResponse> Add(AddUserInformation userInformation)
        {
            return await _userService.AddTemporaryUser(userInformation).ConfigureAwait(false);
        }

        [Authorize]
        [HttpPost("{userId:guid}/verify")]
        [EnableCors("corspolicy")]
        public async Task<bool> VerifyUserCode(Guid userId, UserVerificationRequest userInformation)
        {
            return await _userService.VerifyUserVerificationCodes(new UserVerification
            {
                UserId = userId,
                EmailVerificationCode = userInformation.EmailVerificationCode,
                PhoneVerificationCode = userInformation.PhoneVerificationCode
            }).ConfigureAwait(false);
        }

        [Authorize]
        [HttpPut("{userId:guid}/link/{familyId:guid}")]
        [EnableCors("corspolicy")]
        public async Task<UserInformationResponse> LinkFamily(Guid userId, Guid familyId)
        {
            return await _userService.LinkUserToAFamily(userId, familyId).ConfigureAwait(false);
        }

        [Authorize]
        [ApiAuthorize(false, false, MaktabDataContracts.Enums.UserRoleType.Admin | MaktabDataContracts.Enums.UserRoleType.SuperUser | MaktabDataContracts.Enums.UserRoleType.SchoolAdmin)]
        [HttpPut("{userId:guid}/admin")]
        [EnableCors("corspolicy")]
        public async Task<ActionResult<UserInformationResponse>> AdminUpdateUser(Guid userId, AdminUpdateUserRequest userInformation)
        {
            var updatedUser = await _userService.AdminUpdateUser(userId, userInformation).ConfigureAwait(false);
            if (updatedUser == null)
            {
                return NotFound();
            }

            return Ok(updatedUser);
        }

        [Authorize]
        [ApiAuthorize(false, false, MaktabDataContracts.Enums.UserRoleType.Normal)]
        [HttpPut("{userId:guid}")]
        [EnableCors("corspolicy")]
        public async Task<ActionResult<UserInformationResponse>> UpdateUserProfile(Guid userId, UpdateUserProfileRequest userInformation)
        {
            var updatedUser = await _userService.UpdateUserProfile(userId, userInformation).ConfigureAwait(false);
            if (updatedUser == null)
            {
                return NotFound();
            }

            return Ok(updatedUser);
        }

        [HttpPost("{userId:guid}/resetpassword")]
        [EnableCors("corspolicy")]
        public async Task<bool> ResetPassword(Guid userId, UpdateUserPasswordRequest userInformation)
        {
            return await _userService.ResetUserPassword(new UpdateUserPassword
            {
                NewPassword = userInformation.NewPassword,
                OldPassword = userInformation.OldPassword,
                UserId = userId
            }).ConfigureAwait(false);
        }

        [ApiAuthorize(true, true)]
        [HttpGet("forgotpassword")]
        [EnableCors("corspolicy")]
        public async Task<bool> ForgotPassword(string userName)
        {
            return await _userService.ForgotPassword(userName, null).ConfigureAwait(false);
        }

        [Authorize]
        [ApiAuthorize()]
        [HttpPost("{userId:guid}/delete")]
        [EnableCors("corspolicy")]
        public async Task<bool> DeleteUser(Guid userId, bool ifHardDelete = false)
        {
            return await _userService.DeleteUser(userId, ifHardDelete).ConfigureAwait(false);
        }

        [Authorize]
        [HttpPut("{userId:guid}/activationcode")]
        [EnableCors("corspolicy")]
        public async Task<bool> SendActivationCode(Guid userId)
        {
            return await _userService.SendActivationCode(userId).ConfigureAwait(false);
        }

        [Authorize]
        [HttpPost("{userId:guid}/extendedinfo")]
        [EnableCors("corspolicy")]
        public async Task<ExtendedUserInformationResponse> AddExtendedUserInformation(Guid userId, AddExtendedUserInformationRequest userInformation)
        {
            return await _extendedUserInformationService.AddExtendedUserInformation(new AddExtendedUserInformationInternal 
            {
                UserId = userId,
                FamilyId = userInformation.FamilyId,
                SIN = userInformation.SIN,
                AddressId = userInformation.AddressId,
              IsActiveTaxCreditRecipient = userInformation.IsActiveTaxCreditRecipient,
            }).ConfigureAwait(false);
        }

        [Authorize]
        [HttpPut("{userId:guid}/extendedinfo")]
        [EnableCors("corspolicy")]
        public async Task<ExtendedUserInformationResponse> UpdateExtendedUserInformation(Guid userId, UpdateExtendedUserInformationRequest userInformation)
        {

            return await _extendedUserInformationService.UpdateExtendedUserInformation(new AddExtendedUserInformationInternal
            {
                UserId = userId,
                //SIN = userInformation.SIN,
                AddressId = userInformation.AddressId,
            }).ConfigureAwait(false);
        }

        [Authorize]
        [HttpDelete("{userId:guid}/extendedinfo")]
        [EnableCors("corspolicy")]
        public async Task<bool> DeleteExtendedUserInformation(Guid userId, bool ifHardDelete)
        {
            return await _extendedUserInformationService.DeletExtendedUserInformation(userId, ifHardDelete).ConfigureAwait(false);
        }

        [Authorize]
        [HttpDelete("/families/{familyId:guid}/extendedinfo")]
        [EnableCors("corspolicy")]
        public async Task<bool> DeleteFamilyExtendedUserInformation(Guid familyId, bool ifHardDelete)
        {
            return await _extendedUserInformationService.DeletExtendedUserInformation(familyId, ifHardDelete).ConfigureAwait(false);
        }

        [Authorize]
        [HttpGet("{userId:guid}/extendedinfo")]
        [EnableCors("corspolicy")]
        public async Task<ExtendedUserInformationResponse> GetExtendedUserInformation(Guid userId)
        {
            return await _extendedUserInformationService.GetExtendedUserInformation(userId).ConfigureAwait(false);
        }

        [Authorize]
        [HttpGet("families/{familyId:guid}/extendedinfo")]
        [EnableCors("corspolicy")]
        public async Task<ExtendedUserInformationResponse> GetFamilyExtendedUserInformation(Guid familyId)
        {
            return await _extendedUserInformationService.GetFamilyExtendedUserInformation(familyId).ConfigureAwait(false);
        }

        [Authorize]
        [HttpGet("{userId:guid}/extendedinfo/check")]
        [EnableCors("corspolicy")]
        public async Task<bool> CheckExtendedUserInformation(Guid userId)
        {
            return await _extendedUserInformationService.CheckIfExtendedUserInformationExisit(userId).ConfigureAwait(false);
        }

        [Authorize]
        [HttpGet("families/{familyId:guid}/familyextendedinfo/check")]
        [EnableCors("corspolicy")]
        public async Task<bool> CheckFamilyExtendedUserInformation(Guid familyId)
        {
            return await _extendedUserInformationService.CheckIfExtendedFamilyInformationExisit(familyId).ConfigureAwait(false);
        }

        [Authorize]
        [HttpGet("families/{familyId:guid}/extendedinfo/sin/check")]
        [EnableCors("corspolicy")]
        public async Task<bool> CheckIfFamilySinExists(Guid familyId, string sin)
        {
            return await _extendedUserInformationService.CheckIfFamilySinExists(familyId, sin).ConfigureAwait(false);
        }

        private async Task<SessionAccessContext> GetRequiredSessionContext()
        {
            if (!Request.Headers.TryGetValue("Session_Info", out var sessionHeader)
                || !Guid.TryParse(sessionHeader, out var sessionId)
                || sessionId == Guid.Empty)
            {
                throw new UnauthorizedAccessException("Session header not found or invalid.");
            }

            var sessionContext = await _dataAccessVerificationService.GetSessionAccessContext(sessionId).ConfigureAwait(false);
            if (sessionContext == null || sessionContext.UserId == Guid.Empty)
            {
                throw new UnauthorizedAccessException("No active session found.");
            }

            return sessionContext;
        }
    }
}
