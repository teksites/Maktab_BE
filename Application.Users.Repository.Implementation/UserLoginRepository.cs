
using Application.Users.Contracts;
using Cumulus.Data;
using Data;
using InternalContracts;
using Users.Repository;

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
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"Select userId from user_info where Upper(UserName) = Upper(@userName) and Password=@password";
                    cmd.AddParameter("@userName", userName);
                    cmd.AddParameter("@password", password);
                    var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
                    return reader.HasRows;
                }
            }
        }

        public async Task<UserInformation> GetUserInformation(string userName, string password)
        {
            using (var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false))
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"Select UserId, FirstName, LastName, Email, Phone, UserName, FamilyId from user_info" +
                        " where UserName = @userName and Password = @password and IsActive = true";

                    cmd.AddParameter("@userName", userName);
                    cmd.AddParameter("@password", password);
                    using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);

                    if (!await reader.ReadAsync().ConfigureAwait(false))
                    {
                        return null;
                    }

                    var id = reader.GetGuidFromByteArray(0);
                    var fristName = reader.GetString(1);
                    var lastName = reader.GetString(2);
                    var email = reader.GetString(3);
                    var phone = reader.GetString(4);
                    var userNam = reader.GetString(5);
                    var familyId = reader.GetGuidFromByteArray(6);

                    return new UserInformation
                    {
                        UserId = id,
                        FirstName = fristName,
                        LastName = lastName,
                        Email = email,
                        Phone = phone,
                        UserName = userName,
                        FamilyId = familyId
                    };
                }
            }
        }

        public async Task<bool> LogInSession(AddSession addSession)
        {
            using (var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false))
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"insert into session_info (SessionId, UserId, FamilyId, Token, TokenExpiry, IpAddress, IsActive, LogInTime)"
                    + "Values(@sessionId, @userId, @familyId, @token, @tokenExpiry, @ipAddress, @isActive, @logInTime)";

                    cmd.AddParameter("@sessionId", addSession.SessionId.ToByteArray());
                    cmd.AddParameter("@userId", addSession.UserId.ToByteArray());
                    cmd.AddParameter("@familyId", addSession.FamilyId.ToByteArray());
                    cmd.AddParameter("@token", addSession.Token);
                    cmd.AddParameter("@tokenExpiry", addSession.TokenExpiry);
                    cmd.AddParameter("@ipAddress", addSession.IpAddress);
                    cmd.AddParameter("@isActive", addSession.IsActive);
                    cmd.AddParameter("@logInTime", addSession.LogInTime);

                    if (await cmd.ExecuteNonQueryAsync().ConfigureAwait(false) > 0)
                    {
                        return true;
                    }
                    return false;
                }
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
                    cmd.AddParameter("@logOutTime", DateTime.Now);

                    if (await cmd.ExecuteNonQueryAsync().ConfigureAwait(false) > 0)
                    {
                        return true;
                    }
                    return false;
                }
            }
        }

        public async Task<bool> CheckIfSessionExistOrActive(Guid sessionId)
        {
            using (var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false))
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"Select userId from session_info where IsActive = true and SessionId = @sessionId";
                    cmd.AddParameter("@sessionId", sessionId.ToByteArray());
                    var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
                    return reader.HasRows;
                }
            }
        }

        public async Task<Guid> GetSessionByUserId(Guid userId)
        {
            using (var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false))
            {
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
        }

        public async Task<Guid> GetUserBySessionId(Guid sessionId)
        {
            using (var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false))
            {
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
        }
 
        public async Task<bool> DeleteInActiveSessions()
        {
            using (var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false))
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"Delete from session_info where  IsActive = false";

                    if (await cmd.ExecuteNonQueryAsync().ConfigureAwait(false) > 0)
                    {
                        return true;
                    }
                    return false;
                }
            }
        }
    }
}
