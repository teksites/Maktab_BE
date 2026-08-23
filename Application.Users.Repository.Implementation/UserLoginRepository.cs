using Application.Users.Contracts;
using Cumulus.Data;
using Data;
using System.Data.Common;
using Users.Repository;
using AddSession = InternalContracts.AddSession;
using AddSessionTwoFactorCode = InternalContracts.AddSessionTwoFactorCode;
using SessionTwoFactorCode = InternalContracts.SessionTwoFactorCode;

namespace Application.Users.Repository.Implementation
{
    public class UserLoginRepository : DbRepository, IUserLoginRepository
    {
        public UserLoginRepository(IDatabase database) : base(database)
        {
        }

        public Task<string> Authenticate(string userName, string password)
        {
            throw new NotImplementedException();
        }

        public async Task<bool> ValidateUser(string userName, string password)
        {
            using (var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"Select userId from user_info where Upper(UserName) = Upper(@userName) and Password=@password";
                cmd.AddParameter("@userName", userName);
                cmd.AddParameter("@password", password);
                using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
                return reader.HasRows;
            }
        }

        public async Task<UserInformation> GetUserInformation(string userName, string password)
        {
            using (var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"Select UserId, FirstName, LastName, Email, Phone, UserName, FamilyId, IsMultiFactorLoginEnabled from user_info" +
                    " where UserName = @userName and Password = @password and IsActive = true";

                cmd.AddParameter("@userName", userName);
                cmd.AddParameter("@password", password);
                using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);

                if (!await reader.ReadAsync().ConfigureAwait(false))
                {
                    return null;
                }

                return new UserInformation
                {
                    UserId = reader.GetGuidFromByteArray(0),
                    FirstName = reader.GetString(1),
                    LastName = reader.GetString(2),
                    Email = reader.GetString(3),
                    Phone = reader.GetString(4),
                    UserName = userName,
                    FamilyId = reader.GetGuidFromByteArray(6),
                    IsMultiFactorLoginEnabled = reader.GetBoolean(7)
                };
            }
        }

        public async Task<bool> LogInSession(AddSession addSession)
        {
            using (var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"insert into session_info (SessionId, UserId, FamilyId, Token, TokenExpiry, IpAddress, IsActive, RequiresTwoFactorVerification, IsTwoFactorVerified, TwoFactorVerifiedOn, LogInTime)"
                    + " Values(@sessionId, @userId, @familyId, @token, @tokenExpiry, @ipAddress, @isActive, @requiresTwoFactorVerification, @isTwoFactorVerified, @twoFactorVerifiedOn, @logInTime)";

                cmd.AddParameter("@sessionId", addSession.SessionId.ToByteArray());
                cmd.AddParameter("@userId", addSession.UserId.ToByteArray());
                cmd.AddParameter("@familyId", addSession.FamilyId.ToByteArray());
                cmd.AddParameter("@token", addSession.Token);
                cmd.AddParameter("@tokenExpiry", addSession.TokenExpiry);
                cmd.AddParameter("@ipAddress", addSession.IpAddress);
                cmd.AddParameter("@isActive", addSession.IsActive);
                cmd.AddParameter("@requiresTwoFactorVerification", addSession.RequiresTwoFactorVerification);
                cmd.AddParameter("@isTwoFactorVerified", addSession.IsTwoFactorVerified);
                cmd.AddParameter("@twoFactorVerifiedOn", addSession.TwoFactorVerifiedOn);
                cmd.AddParameter("@logInTime", addSession.LogInTime);

                return await cmd.ExecuteNonQueryAsync().ConfigureAwait(false) > 0;
            }
        }

        public async Task<bool> LogOutSession(Guid sessionId)
        {
            using (var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false))
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"Update session_info SET IsActive = false, LogOutTime = @logOutTime where SessionId = @sessionId";

                    cmd.AddParameter("@sessionId", sessionId.ToByteArray());
                    cmd.AddParameter("@logOutTime", DateTime.UtcNow);

                    var updated = await cmd.ExecuteNonQueryAsync().ConfigureAwait(false) > 0;
                    await DeactivateSessionTwoFactorCodes(conn, sessionId).ConfigureAwait(false);
                    return updated;
                }
            }
        }

        public async Task<bool> CheckIfSessionExistOrActive(Guid sessionId)
        {
            using (var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"Select userId from session_info where IsActive = true and SessionId = @sessionId";
                cmd.AddParameter("@sessionId", sessionId.ToByteArray());
                using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
                return reader.HasRows;
            }
        }

        public async Task<Guid> GetSessionByUserId(Guid userId)
        {
            using (var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"Select SessionId from session_info where IsActive = true and UserId = @userId";
                cmd.AddParameter("@userId", userId.ToByteArray());

                using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);

                if (!await reader.ReadAsync().ConfigureAwait(false))
                {
                    return Guid.Empty;
                }

                return reader.GetGuidFromByteArray(0);
            }
        }

        public async Task<Guid> GetUserBySessionId(Guid sessionId)
        {
            using (var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"Select UserId from session_info where IsActive = true and SessionId = @sessionId";
                cmd.AddParameter("@sessionId", sessionId.ToByteArray());

                using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);

                if (!await reader.ReadAsync().ConfigureAwait(false))
                {
                    return Guid.Empty;
                }

                return reader.GetGuidFromByteArray(0);
            }
        }

        public async Task<SessionAuthenticationState> GetSessionAuthenticationState(Guid sessionId)
        {
            using (var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"Select SessionId, UserId, FamilyId, IsActive, RequiresTwoFactorVerification, IsTwoFactorVerified, TwoFactorVerifiedOn
                    from session_info where SessionId = @sessionId";
                cmd.AddParameter("@sessionId", sessionId.ToByteArray());

                using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
                if (!await reader.ReadAsync().ConfigureAwait(false))
                {
                    return null;
                }

                return new SessionAuthenticationState
                {
                    SessionId = reader.GetGuidFromByteArray(0),
                    UserId = reader.GetGuidFromByteArray(1),
                    FamilyId = reader.GetGuidFromByteArray(2),
                    IsActive = reader.GetBoolean(3),
                    RequiresTwoFactorVerification = reader.GetBoolean(4),
                    IsTwoFactorVerified = reader.GetBoolean(5),
                    TwoFactorVerifiedOn = reader.IsDBNull(6) ? null : reader.GetDateTime(6)
                };
            }
        }

        public async Task<bool> AddSessionTwoFactorCode(AddSessionTwoFactorCode addSessionTwoFactorCode)
        {
            using (var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"insert into session_two_factor_codes
                    (SessionTwoFactorCodeId, SessionId, UserId, Email, VerificationCodeHash, ExpiresOn, AttemptCount, LastSentOn, IsVerified, IsActive, CreatedAt, UpdatedOn)
                    values
                    (@sessionTwoFactorCodeId, @sessionId, @userId, @email, @verificationCodeHash, @expiresOn, @attemptCount, @lastSentOn, @isVerified, @isActive, @createdAt, @updatedOn)";

                cmd.AddParameter("@sessionTwoFactorCodeId", addSessionTwoFactorCode.SessionTwoFactorCodeId.ToByteArray());
                cmd.AddParameter("@sessionId", addSessionTwoFactorCode.SessionId.ToByteArray());
                cmd.AddParameter("@userId", addSessionTwoFactorCode.UserId.ToByteArray());
                cmd.AddParameter("@email", addSessionTwoFactorCode.Email);
                cmd.AddParameter("@verificationCodeHash", addSessionTwoFactorCode.VerificationCodeHash);
                cmd.AddParameter("@expiresOn", addSessionTwoFactorCode.ExpiresOn);
                cmd.AddParameter("@attemptCount", addSessionTwoFactorCode.AttemptCount);
                cmd.AddParameter("@lastSentOn", addSessionTwoFactorCode.LastSentOn);
                cmd.AddParameter("@isVerified", addSessionTwoFactorCode.IsVerified);
                cmd.AddParameter("@isActive", addSessionTwoFactorCode.IsActive);
                cmd.AddParameter("@createdAt", addSessionTwoFactorCode.CreatedAt);
                cmd.AddParameter("@updatedOn", addSessionTwoFactorCode.UpdatedOn);

                return await cmd.ExecuteNonQueryAsync().ConfigureAwait(false) > 0;
            }
        }

        public async Task<SessionTwoFactorCode> GetActiveSessionTwoFactorCode(Guid sessionId)
        {
            using (var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"Select SessionTwoFactorCodeId, SessionId, UserId, Email, VerificationCodeHash, ExpiresOn, AttemptCount, LastSentOn, IsVerified, VerifiedOn, IsActive, CreatedAt, UpdatedOn
                    from session_two_factor_codes
                    where SessionId = @sessionId and IsActive = true and IsVerified = false
                    order by CreatedAt desc
                    limit 1";
                cmd.AddParameter("@sessionId", sessionId.ToByteArray());

                using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
                if (!await reader.ReadAsync().ConfigureAwait(false))
                {
                    return null;
                }

                return new SessionTwoFactorCode
                {
                    SessionTwoFactorCodeId = reader.GetGuidFromByteArray(0),
                    SessionId = reader.GetGuidFromByteArray(1),
                    UserId = reader.GetGuidFromByteArray(2),
                    Email = reader.GetString(3),
                    VerificationCodeHash = reader.GetString(4),
                    ExpiresOn = reader.GetDateTime(5),
                    AttemptCount = reader.GetInt32(6),
                    LastSentOn = reader.IsDBNull(7) ? null : reader.GetDateTime(7),
                    IsVerified = reader.GetBoolean(8),
                    VerifiedOn = reader.IsDBNull(9) ? null : reader.GetDateTime(9),
                    IsActive = reader.GetBoolean(10),
                    CreatedAt = reader.GetDateTime(11),
                    UpdatedOn = reader.GetDateTime(12)
                };
            }
        }

        public async Task<bool> DeactivateSessionTwoFactorCodes(Guid sessionId)
        {
            using (var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false))
            {
                return await DeactivateSessionTwoFactorCodes(conn, sessionId).ConfigureAwait(false);
            }
        }

        public async Task<bool> IncrementSessionTwoFactorAttemptCount(Guid sessionTwoFactorCodeId)
        {
            using (var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"Update session_two_factor_codes
                    set AttemptCount = AttemptCount + 1,
                        UpdatedOn = @updatedOn
                    where SessionTwoFactorCodeId = @sessionTwoFactorCodeId";
                cmd.AddParameter("@sessionTwoFactorCodeId", sessionTwoFactorCodeId.ToByteArray());
                cmd.AddParameter("@updatedOn", DateTime.UtcNow);
                return await cmd.ExecuteNonQueryAsync().ConfigureAwait(false) > 0;
            }
        }

        public async Task<bool> MarkSessionTwoFactorCodeVerified(Guid sessionTwoFactorCodeId, DateTime verifiedOn)
        {
            using (var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"Update session_two_factor_codes
                    set IsVerified = true,
                        IsActive = false,
                        VerifiedOn = @verifiedOn,
                        UpdatedOn = @updatedOn
                    where SessionTwoFactorCodeId = @sessionTwoFactorCodeId";
                cmd.AddParameter("@sessionTwoFactorCodeId", sessionTwoFactorCodeId.ToByteArray());
                cmd.AddParameter("@verifiedOn", verifiedOn);
                cmd.AddParameter("@updatedOn", verifiedOn);
                return await cmd.ExecuteNonQueryAsync().ConfigureAwait(false) > 0;
            }
        }

        public async Task<bool> MarkSessionTwoFactorVerified(Guid sessionId, DateTime verifiedOn)
        {
            using (var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"Update session_info
                    set IsTwoFactorVerified = true,
                        TwoFactorVerifiedOn = @verifiedOn
                    where SessionId = @sessionId";
                cmd.AddParameter("@sessionId", sessionId.ToByteArray());
                cmd.AddParameter("@verifiedOn", verifiedOn);
                return await cmd.ExecuteNonQueryAsync().ConfigureAwait(false) > 0;
            }
        }

        public async Task<bool> DeleteInActiveSessions()
        {
            using (var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"Delete from session_info where IsActive = false";
                return await cmd.ExecuteNonQueryAsync().ConfigureAwait(false) > 0;
            }
        }

        private static async Task<bool> DeactivateSessionTwoFactorCodes(DbConnection conn, Guid sessionId)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"Update session_two_factor_codes
                set IsActive = false,
                    UpdatedOn = @updatedOn
                where SessionId = @sessionId and IsActive = true and IsVerified = false";
            cmd.AddParameter("@sessionId", sessionId.ToByteArray());
            cmd.AddParameter("@updatedOn", DateTime.UtcNow);
            return await cmd.ExecuteNonQueryAsync().ConfigureAwait(false) > 0;
        }
    }
}
