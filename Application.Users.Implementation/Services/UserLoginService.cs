using InternalContracts;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using MaktabDataContracts.Requests.Users;
using MaktabDataContracts.Responses.Authentication;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Users.Contracts;
using Users.Repository;
using Users.Services;

namespace Users.Implementation.Services
{
    public class UserLoginService : IUserLoginService
    {
        private readonly IConfiguration _configuration;
        private readonly IUserLoginRepository _repository;
        private readonly IUserService _userService;
        public UserLoginService(IConfiguration configuration, IUserLoginRepository repository, IUserService userService)
        {
            _configuration = configuration;
            _repository = repository;
            _userService = userService;

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

            var tokenHandler = new JwtSecurityTokenHandler();
            var tokenExpiry = DateTime.UtcNow.AddMinutes(60);

            var tokenDescriptor = new SecurityTokenDescriptor()
            {
                Subject = new ClaimsIdentity(new Claim[] {
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
            var sessionInfo = new AddSession
            {
                SessionId = Guid.NewGuid(),
                UserId = userInfo.UserId,
                IpAddress = ipAddress,
                IsActive = true,
                Token = tokenString,
                TokenExpiry = tokenExpiry,
                LogInTime = loginTime,
            };

            var existingActiveSessionId = await GetSessionByUserId(userInfo.UserId).ConfigureAwait(false);

            if ( await LoginSession(sessionInfo).ConfigureAwait(false))
            {
                //Log out old exisiting session
                if (existingActiveSessionId != Guid.Empty)
                {
                    await _repository.LogOutSession(existingActiveSessionId).ConfigureAwait(false);
                }

                var response = new AuthenticationResponse
                {
                    AccessToken = tokenString,
                    SessionId = sessionInfo.SessionId,
                    UserId = userInfo.UserId,
                    RefreshToken = "",
                    LoginTime = loginTime,
                    ExpiresIn = tokenExpiry
                };

                return response; 
            }
            return null;
        }

        private async Task<bool> LoginSession(AddSession addSession)
        {
            return await _repository.LogInSession(addSession).ConfigureAwait(false);
        }

        public async Task<Guid> GetUserBySessionId(Guid sessionId)
        {
            return await _repository.GetUserBySessionId(sessionId).ConfigureAwait(false);
        }

        public async Task<bool> LogOutSession(Guid sessionId)
        {
            if (await CheckIfSessionExistOrActive(sessionId))
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

    }
}
