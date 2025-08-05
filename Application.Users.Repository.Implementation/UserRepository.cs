using Application.Users.Contracts;
using Cumulus.Data;
using Data;
using MaktabDataContracts.Enums;
using Users.Contracts;
using Users.Repository;
using Users.Utils.Implementation;

namespace Application.Users.Repository.Implementation
{
    public class UserRepository : DbRepository, IUserRepository
    {
        public UserRepository(IDatabase database) : base(database)
        {
        }

        public async Task<UserInformation> AddUser(UserInformation userInformation)
        {
            using (var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false))
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"insert into user_info (UserId, FamilyId, FirstName, LastName, Email, Phone, UserName, Password, IsActive, CreatedAt, UpdatedOn, IsAdmin, Relationship)"
                    + "Values(@userId, @familyId, @firstName, @lastName, @email, @phone, @userName, @password, @isActive, @createdAt, @updatedOn, @isAdmin, @relationship)";

                    //var password = PasswordHelper.HashPassword(userInformation.Password);
                    cmd.AddParameter("@userId", userInformation.UserId.ToByteArray());
                    cmd.AddParameter("@familyId", userInformation.FamilyId.ToByteArray());
                    cmd.AddParameter("@firstName", userInformation.FirstName);
                    cmd.AddParameter("@lastName", userInformation.LastName);
                    cmd.AddParameter("@email", userInformation.Email);
                    cmd.AddParameter("@phone", userInformation.Phone);
                    cmd.AddParameter("@userName", userInformation.UserName);
                    cmd.AddParameter("@password", userInformation.Password);
                    cmd.AddParameter("@isActive", userInformation.IsActive);
                    cmd.AddParameter("@createdAt", userInformation.CreatedAt);
                    cmd.AddParameter("@updatedOn", userInformation.UpdatedOn);
                    cmd.AddParameter("@isAdmin", userInformation.IsAdmin);
                    cmd.AddParameter("@@relationship", userInformation.Relationship);
                    
                    if (await cmd.ExecuteNonQueryAsync().ConfigureAwait(false) > 0)
                    {
                        return new UserInformation
                        {
                            UserId = userInformation.UserId,
                            FamilyId = userInformation.FamilyId,
                            FirstName = userInformation.FirstName,
                            LastName = userInformation.LastName,
                            UserName = userInformation.UserName,
                            Password = userInformation.Password,
                            Phone = userInformation.Phone,
                            Email = userInformation.Email,
                            IsActive = userInformation.IsActive,
                            CreatedAt = userInformation.CreatedAt,
                            UpdatedOn = userInformation.UpdatedOn,
                            IsAdmin = userInformation.IsAdmin,
                            Relationship = userInformation.Relationship

                        };
                    }
                    else
                    {
                        return null;
                    }
                }
            }
        }

        public async Task<bool> CheckIfUserAlreadyRegistered(string email, string phone)
        {
            using (var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false))
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"Select userId from user_info where UPPER(Email) like UPPER(@email) or Phone = @phone";
                    cmd.AddParameter("@email", email);
                    cmd.AddParameter("@phone", phone);
                    var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
                    return reader.HasRows;
                }
            }
        }

        public async Task<bool> CheckIfUserIsAdmin(Guid userId)
        {
            using (var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false))
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"Select UserId from user_info where UserId = @userId and IsAdmin = True and IsActive = True";
                    cmd.AddParameter("@userId", userId.ToByteArray());
                    var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
                    return reader.HasRows;
                }
            }
        }
        public async Task<bool> CheckIfTempUser(string userName)
        {
            using (var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false))
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"Select UserId from user_info where UserName = @userName and IsActive = True";
                    cmd.AddParameter("@userName", userName);
                    var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
                    return reader.HasRows;
                }
            }
        }

        public async Task<bool> CheckIfUserNameExisit(string userName)
        {
            using (var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false))
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"Select userId from user_info where Upper(UserName) like Upper(@userName) Or Upper(Email) like Upper(@userName) ";
                    cmd.AddParameter("@userName", userName);
                    var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
                    return reader.HasRows;
                }
            }
        }

        public async Task<bool> DeleteUser(Guid userId, bool ifHardDelete = false)
        {
            using (var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false))
            {
                using (var cmd = conn.CreateCommand())
                {
                    if (ifHardDelete)
                    {
                        cmd.CommandText = @"Delete from user_info where UserId = @userId";
                    }
                    else
                    {
                        cmd.CommandText = @"Update user_info SET IsActive = false where UserId = @userId";
                    }

                    cmd.AddParameter("@userId", userId.ToByteArray());
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }

        public async Task<IEnumerable<UserInformation>> GetAllUsersInformation(bool ifOnlyActive = true)
        {
            var results = new List<UserInformation>();

            using (var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false))
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"Select UserId, FirstName, LastName, Email, Phone, UserName, Password, IsActive, CreatedAt, UpdatedOn, IsAdmin, IsTempPassword, FamilyId, Relationship from user_info";

                    if (ifOnlyActive)
                    {
                        cmd.CommandText += " where IsActive = True";
                    }

                    using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);

                    while (await reader.ReadAsync().ConfigureAwait(false))
                    {
                        var userId = reader.GetGuidFromByteArray(0);
                        var fristName = reader.GetString(1);
                        var lastName = reader.GetString(2);
                        var email = reader.GetString(3);
                        var phone = reader.GetString(4);
                        var userName = reader.GetString(5);
                        var password = reader.GetString(6);
                        var isActive = reader.GetBoolean(7);
                        var CreatedAt = reader.GetDateTime(8);
                        var UpdatedOn = reader.GetDateTime(9);
                        var isAdmin = reader.GetBoolean(10);
                        var isTempPassword = reader.GetBoolean(11);
                        var familyId = reader.GetGuidFromByteArray(12);
                        var relationship = (Relationship)reader.GetInt32(13);


                        results.Add(new UserInformation
                        {
                            UserId = userId,
                            FamilyId = familyId,
                            FirstName = fristName,
                            LastName = lastName,
                            Email = email,
                            Phone = phone,
                            Password = string.Empty,
                            UserName = userName,
                            IsActive = isActive,
                            CreatedAt = CreatedAt,
                            UpdatedOn = UpdatedOn,
                            IsAdmin = isAdmin,
                            IsTempPassword = isTempPassword,
                            Relationship = relationship
                        });
                    }
                }
            }
            return results; 
        }

        public async Task<IEnumerable<UserInformation>> GetAllFamilyUsersInformation(Guid id, bool ifOnlyActive = true)
        {
            var results = new List<UserInformation>();

            using (var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false))
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"Select UserId, FirstName, LastName, Email, Phone, UserName, Password, IsActive, CreatedAt, UpdatedOn, IsAdmin, IsTempPassword, FamilyId, Relationship from user_info where FamilyId = @familyId";

                    cmd.AddParameter("@familyId", id.ToByteArray());

                    if (ifOnlyActive)
                    {
                        cmd.CommandText += " and IsActive = True";
                    }

                    using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);

                    while (await reader.ReadAsync().ConfigureAwait(false))
                    {
                        var userId = reader.GetGuidFromByteArray(0);
                        var fristName = reader.GetString(1);
                        var lastName = reader.GetString(2);
                        var email = reader.GetString(3);
                        var phone = reader.GetString(4);
                        var userName = reader.GetString(5);
                        var password = reader.GetString(6);
                        var isActive = reader.GetBoolean(7);
                        var CreatedAt = reader.GetDateTime(8);
                        var UpdatedOn = reader.GetDateTime(9);
                        var isAdmin = reader.GetBoolean(10);
                        var isTempPassword = reader.GetBoolean(11);
                        var familyId = reader.GetGuidFromByteArray(12);
                        var relationship = (Relationship)reader.GetInt32(13);

                        results.Add(new UserInformation
                        {
                            UserId = userId,
                            FamilyId = familyId,
                            FirstName = fristName,
                            LastName = lastName,
                            Email = email,
                            Phone = phone,
                            Password = string.Empty,
                            UserName = userName,
                            IsActive = isActive,
                            CreatedAt = CreatedAt,
                            UpdatedOn = UpdatedOn,
                            IsAdmin = isAdmin,
                            IsTempPassword = isTempPassword,
                            Relationship = relationship
                        });
                    }
                }
            }
            return results;
        }

        public async Task<UserInformation> GetUserInformation(Guid userId)
        {
            using (var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false))
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"Select UserId, FirstName, LastName, Email, Phone, UserName, Password, IsActive, CreatedAt, UpdatedOn, IsAdmin, IsTempPassword, FamilyId, Relationship from user_info" +
                        " where UserId = @userId";

                    cmd.AddParameter("@userId", userId.ToByteArray());
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
                    var userName = reader.GetString(5);
                    var password = reader.GetString(6);
                    var isActive = reader.GetBoolean(7);
                    var CreatedAt = reader.GetDateTime(8);
                    var UpdatedOn = reader.GetDateTime(9);
                    var isAdmin = reader.GetBoolean(10);
                    var isTempPassword = reader.GetBoolean(11);
                    var familyId = reader.GetGuidFromByteArray(12);
                    var relationship = (Relationship)reader.GetInt32(13);

                    return new UserInformation
                    {
                        UserId = userId,
                        FamilyId = familyId,
                        FirstName = fristName,
                        LastName = lastName,
                        Email = email,
                        Phone = phone,
                        Password = password,
                        UserName = userName,
                        IsActive = isActive,
                        CreatedAt = CreatedAt,
                        UpdatedOn = UpdatedOn,
                        IsAdmin = isAdmin,
                        IsTempPassword = isTempPassword,
                        Relationship = relationship
                    };
                }
            }
        }

        public async Task<UserInformation> UpdateUser(UpdateUserPassword userInformation, bool ifTempPassword = false)
        {
            using (var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false))
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"Update user_info SET Password = @password, UpdatedOn = @updatedOn , IsTempPassword = @isTempPassword where UserId = @userId";

                    cmd.AddParameter("@userId", userInformation.UserId.ToByteArray());
                    cmd.AddParameter("@password", PasswordHelper.HashPassword(userInformation.NewPassword));
                    cmd.AddParameter("@updatedOn", DateTime.Now);
                    cmd.AddParameter("@isTempPassword", ifTempPassword);

                    if (await cmd.ExecuteNonQueryAsync().ConfigureAwait(false) > 0)
                    {
                        return await GetUserInformation(userInformation.UserId).ConfigureAwait(false);
                    }

                    return null;
                }
            }
        }

        public async Task<UserInformation> LinkUserToAFamily(Guid userId, Guid familyId)
        {
            using (var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false))
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"Update user_info SET familyId = @familyId, UpdatedOn = @updatedOn where UserId = @userId";

                    cmd.AddParameter("@userId", userId.ToByteArray());
                    cmd.AddParameter("@familyId", familyId.ToByteArray());
                    cmd.AddParameter("@updatedOn", DateTime.Now);

                    if (await cmd.ExecuteNonQueryAsync().ConfigureAwait(false) > 0)
                    {
                        return await GetUserInformation(userId).ConfigureAwait(false);
                    }

                    return null;
                }
            }
        }

        public async Task<UserInformation> GetUserInformation(string userName, string? password, bool ifForgotPassword)
        {
            using (var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false))
            {
                using (var cmd = conn.CreateCommand())
                {
                    // 1. Fetch full user info including password hash
                    cmd.CommandText = @"
                    SELECT UserId, FirstName, LastName, Email, Phone, UserName, IsAdmin, IsTempPassword, Password, FamilyId, Relationship
                    FROM user_info
                    WHERE (UPPER(UserName) = UPPER(@userName) OR UPPER(Email) = UPPER(@userName)) 
                      AND IsActive = TRUE";

                    cmd.AddParameter("@userName", userName);

                    using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);

                    if (!await reader.ReadAsync().ConfigureAwait(false))
                    {
                        return null;
                    }

                    // 2. Read values from DB
                    var id = reader.GetGuidFromByteArray(0);
                    var firstName = reader.GetString(1);
                    var lastName = reader.GetString(2);
                    var email = reader.GetString(3);
                    var phone = reader.GetString(4);
                    var userNam = reader.GetString(5);
                    var isAdmin = reader.GetBoolean(6);
                    var isTempPassword = reader.GetBoolean(7);
                    var storedPasswordHash = reader.GetString(8); // index of Password
                    var familyId = reader.GetGuidFromByteArray(9);
                    var relationship = (Relationship)reader.GetInt32(10);

                    // 3. If password check is requested, verify hash
                    if (!ifForgotPassword && !PasswordHelper.VerifyPassword(password, storedPasswordHash))
                    {
                        return null;
                    }

                    return new UserInformation
                    {
                        UserId = id,
                        FamilyId = familyId,
                        FirstName = firstName,
                        LastName = lastName,
                        Email = email,
                        Phone = phone,
                        UserName = userNam,
                        IsAdmin = isAdmin,
                        IsTempPassword = isTempPassword,
                        IsActive = true,
                        Relationship = relationship
                    };
                }
            }
        }
    }
}
