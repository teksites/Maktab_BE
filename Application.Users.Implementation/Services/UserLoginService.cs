using Application.Users.Contracts;
using Email;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using MaktabDataContracts.Requests.Authentication;
using MaktabDataContracts.Requests.Users;
using MaktabDataContracts.Responses.Authentication;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Users.Contracts;
using Users.Repository;
using Users.Services;
using AddSession = InternalContracts.AddSession;
using AddSessionTwoFactorCode = InternalContracts.AddSessionTwoFactorCode;

namespace Users.Implementation.Services
{
    public class UserLoginService : IUserLoginService
    {
        private const string TwoFactorEmailSubject = "ICC Maktab login verification code - code de verification de connexion ICC Maktab";
        private readonly IConfiguration _configuration;
        private readonly IUserLoginRepository _repository;
        private readonly IUserService _userService;
        private readonly ISendEmailService _sendEmailService;

        public UserLoginService(
            IConfiguration configuration,
            IUserLoginRepository repository,
            IUserService userService,
            ISendEmailService sendEmailService)
        {
            _configuration = configuration;
            _repository = repository;
            _userService = userService;
            _sendEmailService = sendEmailService;
        }

        public Task<bool> ValidateUser(string username, string password)
        {
            return Task.FromResult(false);
        }

        public async Task<AuthenticationResponse> Authenticate(string userName, string password, string ipAddress)
        {
            var userInfo = await _userService.GetUserInformation(userName, password, false).ConfigureAwait(false);
            if (userInfo == null)
            {
                return null;
            }

            var key = _configuration["JwtConfig:Key"].ToString();
            var keyBytes = Encoding.ASCII.GetBytes(key);
            var tokenExpiryMinutesSetting = _configuration["JwtConfig:ExpiryMinutes"];
            var tokenExpiryMinutes = int.TryParse(tokenExpiryMinutesSetting, out var configuredTokenExpiryMinutes)
                ? configuredTokenExpiryMinutes
                : 240;

            var tokenHandler = new JwtSecurityTokenHandler();
            var tokenExpiry = DateTime.UtcNow.AddMinutes(tokenExpiryMinutes);

            var tokenDescriptor = new SecurityTokenDescriptor()
            {
                Subject = new ClaimsIdentity(new Claim[]
                {
                    new Claim(ClaimTypes.NameIdentifier, userName)
                }),
                Expires = tokenExpiry,
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(keyBytes), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            var tokenString = tokenHandler.WriteToken(token);
            if (string.IsNullOrEmpty(tokenString))
            {
                return null;
            }

            var loginTime = DateTime.UtcNow;
            var requiresTwoFactorVerification = !userInfo.IfTempUser && userInfo.IsMultiFactorLoginEnabled;
            var sessionInfo = new AddSession
            {
                SessionId = Guid.NewGuid(),
                UserId = userInfo.UserId,
                FamilyId = userInfo.FamilyId,
                IpAddress = ipAddress,
                IsActive = true,
                Token = tokenString,
                TokenExpiry = tokenExpiry,
                RequiresTwoFactorVerification = requiresTwoFactorVerification,
                IsTwoFactorVerified = !requiresTwoFactorVerification,
                TwoFactorVerifiedOn = requiresTwoFactorVerification ? null : loginTime,
                LogInTime = loginTime
            };

            var existingActiveSessionId = await GetSessionByUserId(userInfo.UserId).ConfigureAwait(false);
            if (!await LoginSession(sessionInfo).ConfigureAwait(false))
            {
                return null;
            }

            if (existingActiveSessionId != Guid.Empty)
            {
                await _repository.LogOutSession(existingActiveSessionId).ConfigureAwait(false);
            }

            DateTime? twoFactorCodeExpiresOn = null;
            if (requiresTwoFactorVerification)
            {
                twoFactorCodeExpiresOn = await CreateAndSendTwoFactorCodeAsync(
                    sessionInfo.SessionId,
                    userInfo.UserId,
                    userInfo.Email,
                    userInfo.FirstName,
                    userInfo.LastName).ConfigureAwait(false);

                if (!twoFactorCodeExpiresOn.HasValue)
                {
                    await _repository.LogOutSession(sessionInfo.SessionId).ConfigureAwait(false);
                    return null;
                }
            }

            return new AuthenticationResponse
            {
                AccessToken = tokenString,
                SessionId = sessionInfo.SessionId,
                UserId = userInfo.UserId,
                FamilyId = userInfo.FamilyId,
                RefreshToken = "",
                RequiresTwoFactorVerification = requiresTwoFactorVerification,
                IsTwoFactorVerified = !requiresTwoFactorVerification,
                TwoFactorCodeExpiresOn = twoFactorCodeExpiresOn,
                LoginTime = loginTime,
                ExpiresIn = tokenExpiry
            };
        }

        public async Task<SessionAuthenticationState> GetSessionAuthenticationState(Guid sessionId)
        {
            return await _repository.GetSessionAuthenticationState(sessionId).ConfigureAwait(false);
        }

        public async Task<TwoFactorLoginVerificationResponse> VerifyTwoFactorLogin(
            Guid sessionId,
            string userName,
            VerifyTwoFactorLoginRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.VerificationCode))
            {
                return CreateTwoFactorFailure(sessionId, "Verification code is required.");
            }

            var (sessionState, userInfo, failureResponse) = await ValidateOwnedSessionAsync(sessionId, userName).ConfigureAwait(false);
            if (failureResponse != null)
            {
                return failureResponse;
            }

            if (!sessionState.RequiresTwoFactorVerification)
            {
                return new TwoFactorLoginVerificationResponse
                {
                    Success = true,
                    Message = "Two-factor verification is not required for this session.",
                    SessionId = sessionId,
                    RequiresTwoFactorVerification = false,
                    IsTwoFactorVerified = true
                };
            }

            if (sessionState.IsTwoFactorVerified)
            {
                return new TwoFactorLoginVerificationResponse
                {
                    Success = true,
                    Message = "Two-factor verification is already completed for this session.",
                    SessionId = sessionId,
                    RequiresTwoFactorVerification = true,
                    IsTwoFactorVerified = true,
                    TwoFactorVerifiedOn = sessionState.TwoFactorVerifiedOn
                };
            }

            var activeCode = await _repository.GetActiveSessionTwoFactorCode(sessionId).ConfigureAwait(false);
            if (activeCode == null)
            {
                return CreateTwoFactorFailure(sessionId, "No active verification code was found. Please request a new code.");
            }

            if (activeCode.ExpiresOn <= DateTime.UtcNow)
            {
                await _repository.DeactivateSessionTwoFactorCodes(sessionId).ConfigureAwait(false);
                return CreateTwoFactorFailure(sessionId, "The verification code has expired. Please request a new code.", activeCode.ExpiresOn);
            }

            var maxAttempts = GetTwoFactorMaxAttempts();
            if (activeCode.AttemptCount >= maxAttempts)
            {
                await _repository.DeactivateSessionTwoFactorCodes(sessionId).ConfigureAwait(false);
                return CreateTwoFactorFailure(sessionId, "Maximum verification attempts exceeded. Please request a new code.", activeCode.ExpiresOn);
            }

            var providedHash = HashVerificationCode(request.VerificationCode.Trim());
            if (!string.Equals(activeCode.VerificationCodeHash, providedHash, StringComparison.Ordinal))
            {
                await _repository.IncrementSessionTwoFactorAttemptCount(activeCode.SessionTwoFactorCodeId).ConfigureAwait(false);
                if (activeCode.AttemptCount + 1 >= maxAttempts)
                {
                    await _repository.DeactivateSessionTwoFactorCodes(sessionId).ConfigureAwait(false);
                    return CreateTwoFactorFailure(sessionId, "Maximum verification attempts exceeded. Please request a new code.", activeCode.ExpiresOn);
                }

                return CreateTwoFactorFailure(sessionId, "Invalid verification code.", activeCode.ExpiresOn);
            }

            var verifiedOn = DateTime.UtcNow;
            await _repository.MarkSessionTwoFactorCodeVerified(activeCode.SessionTwoFactorCodeId, verifiedOn).ConfigureAwait(false);
            await _repository.MarkSessionTwoFactorVerified(sessionId, verifiedOn).ConfigureAwait(false);

            return new TwoFactorLoginVerificationResponse
            {
                Success = true,
                Message = "Two-factor verification completed successfully.",
                SessionId = sessionId,
                RequiresTwoFactorVerification = true,
                IsTwoFactorVerified = true,
                TwoFactorVerifiedOn = verifiedOn
            };
        }

        public async Task<TwoFactorLoginVerificationResponse> ResendTwoFactorLogin(Guid sessionId, string userName)
        {
            var (sessionState, userInfo, failureResponse) = await ValidateOwnedSessionAsync(sessionId, userName).ConfigureAwait(false);
            if (failureResponse != null)
            {
                return failureResponse;
            }

            if (!sessionState.RequiresTwoFactorVerification)
            {
                return new TwoFactorLoginVerificationResponse
                {
                    Success = true,
                    Message = "Two-factor verification is not required for this session.",
                    SessionId = sessionId,
                    RequiresTwoFactorVerification = false,
                    IsTwoFactorVerified = true
                };
            }

            if (sessionState.IsTwoFactorVerified)
            {
                return new TwoFactorLoginVerificationResponse
                {
                    Success = true,
                    Message = "Two-factor verification is already completed for this session.",
                    SessionId = sessionId,
                    RequiresTwoFactorVerification = true,
                    IsTwoFactorVerified = true,
                    TwoFactorVerifiedOn = sessionState.TwoFactorVerifiedOn
                };
            }

            await _repository.DeactivateSessionTwoFactorCodes(sessionId).ConfigureAwait(false);
            var expiresOn = await CreateAndSendTwoFactorCodeAsync(
                sessionId,
                userInfo.UserId,
                userInfo.Email,
                userInfo.FirstName,
                userInfo.LastName).ConfigureAwait(false);

            if (!expiresOn.HasValue)
            {
                return CreateTwoFactorFailure(sessionId, "Failed to send a new verification code.");
            }

            return new TwoFactorLoginVerificationResponse
            {
                Success = true,
                Message = "A new verification code has been sent.",
                SessionId = sessionId,
                RequiresTwoFactorVerification = true,
                IsTwoFactorVerified = false,
                TwoFactorCodeExpiresOn = expiresOn
            };
        }

        public async Task<Guid> GetUserBySessionId(Guid sessionId)
        {
            return await _repository.GetUserBySessionId(sessionId).ConfigureAwait(false);
        }

        public async Task<bool> LogOutSession(Guid sessionId)
        {
            if (await CheckIfSessionExistOrActive(sessionId).ConfigureAwait(false))
            {
                return await _repository.LogOutSession(sessionId).ConfigureAwait(false);
            }

            return false;
        }

        public async Task<bool> CheckIfSessionExistOrActive(Guid sessionId)
        {
            return await _repository.CheckIfSessionExistOrActive(sessionId).ConfigureAwait(false);
        }

        public async Task<Guid> GetSessionByUserId(Guid userId)
        {
            return await _repository.GetSessionByUserId(userId).ConfigureAwait(false);
        }

        public async Task<bool> ResetUserPassword(UpdateUserPassword updateUserPassword)
        {
            return await _userService.ResetUserPassword(updateUserPassword).ConfigureAwait(false);
        }

        public async Task<bool> ForgotPassword(string email)
        {
            return await _userService.ForgotPassword(email, null).ConfigureAwait(false);
        }

        private async Task<bool> LoginSession(AddSession addSession)
        {
            return await _repository.LogInSession(addSession).ConfigureAwait(false);
        }

        private async Task<DateTime?> CreateAndSendTwoFactorCodeAsync(
            Guid sessionId,
            Guid userId,
            string email,
            string firstName,
            string lastName)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return null;
            }

            var code = GenerateRandomVerificationCode();
            var now = DateTime.UtcNow;
            var expiresOn = now.AddMinutes(GetTwoFactorCodeExpiryMinutes());

            await _repository.DeactivateSessionTwoFactorCodes(sessionId).ConfigureAwait(false);
            await _repository.AddSessionTwoFactorCode(new AddSessionTwoFactorCode
            {
                SessionTwoFactorCodeId = Guid.NewGuid(),
                SessionId = sessionId,
                UserId = userId,
                Email = email.Trim(),
                VerificationCodeHash = HashVerificationCode(code),
                ExpiresOn = expiresOn,
                AttemptCount = 0,
                LastSentOn = now,
                IsVerified = false,
                IsActive = true,
                CreatedAt = now,
                UpdatedOn = now
            }).ConfigureAwait(false);

            var emailSent = await _sendEmailService.SendEmail(new EmailData
            {
                To = email.Trim(),
                Subject = TwoFactorEmailSubject,
                Body = BuildTwoFactorEmailBody(firstName, lastName, code)
            }).ConfigureAwait(false);

            return emailSent ? expiresOn : null;
        }

        private async Task<(SessionAuthenticationState SessionState, MaktabDataContracts.Responses.Users.UserInformationResponse UserInfo, TwoFactorLoginVerificationResponse FailureResponse)> ValidateOwnedSessionAsync(
            Guid sessionId,
            string userName)
        {
            if (sessionId == Guid.Empty)
            {
                return (null, null, CreateTwoFactorFailure(sessionId, "Session id is required."));
            }

            var sessionState = await _repository.GetSessionAuthenticationState(sessionId).ConfigureAwait(false);
            if (sessionState == null || !sessionState.IsActive)
            {
                return (null, null, CreateTwoFactorFailure(sessionId, "No active session found."));
            }

            var userInfo = await _userService.GetUserInformation(userName, null, true).ConfigureAwait(false);
            if (userInfo == null || userInfo.IfTempUser || userInfo.UserId != sessionState.UserId)
            {
                return (null, null, CreateTwoFactorFailure(sessionId, "Access denied for the requested session."));
            }

            return (sessionState, userInfo, null);
        }

        private int GetTwoFactorCodeExpiryMinutes()
        {
            var setting = _configuration["Authentication:TwoFactorCodeExpiryMinutes"];
            return int.TryParse(setting, out var configuredMinutes) && configuredMinutes > 0
                ? configuredMinutes
                : 10;
        }

        private int GetTwoFactorMaxAttempts()
        {
            var setting = _configuration["Authentication:TwoFactorMaxAttempts"];
            return int.TryParse(setting, out var configuredAttempts) && configuredAttempts > 0
                ? configuredAttempts
                : 5;
        }

        private static string GenerateRandomVerificationCode()
        {
            Guid g = Guid.NewGuid();
            string guidString = Convert.ToBase64String(g.ToByteArray());
            guidString = guidString.Replace("=", "");
            guidString = guidString.Replace("+", "");
            return guidString.Substring(0, 6);
        }

        private static string HashVerificationCode(string verificationCode)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(verificationCode));
            return Convert.ToHexString(bytes);
        }

        private static string BuildTwoFactorEmailBody(string firstName, string lastName, string verificationCode)
        {
            return $"<p><strong>Greetings {firstName} {lastName}</strong>,</p>" +
                   "<p>Verification code for your ICC Brossard Schools and Activities portal account (Maktab) login.</p>" +
                   $"<div>Your verification code is : <strong>{verificationCode}</strong>.</div>" +
                   "<div></div>" +
                   "<div>Please enter this code to complete your login to the ICC Brossard Schools and Activities portal.</div>" +
                   "<div>&nbsp;</div>" +
                   "<div><strong>ICC Brossard Schools and Activities Registration Portal (Maktab)</strong></div>" +
                   "<div>&nbsp;</div>" +
                   "<hr />" +
                   $"<p><strong>Bonjour {firstName} {lastName},</strong></p>" +
                   "<p>Code de verification pour la connexion a votre compte sur le portail des ecoles et activites ICC Brossard (Maktab).</p>" +
                   $"<div>Votre code de verification est : <strong>{verificationCode}</strong>.</div>" +
                   "<div></div>" +
                   "<div>Veuillez saisir ce code pour completer votre connexion au portail des ecoles et activites ICC Brossard.</div>" +
                   "<div>&nbsp;</div>" +
                   "<div><strong>Portail d'inscription des ecoles et activites ICC Brossard (Maktab)</strong></div>";
        }

        private static TwoFactorLoginVerificationResponse CreateTwoFactorFailure(
            Guid sessionId,
            string message,
            DateTime? expiresOn = null)
        {
            return new TwoFactorLoginVerificationResponse
            {
                Success = false,
                Message = message,
                SessionId = sessionId,
                RequiresTwoFactorVerification = true,
                IsTwoFactorVerified = false,
                TwoFactorCodeExpiresOn = expiresOn
            };
        }
    }
}
