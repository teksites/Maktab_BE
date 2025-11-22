using Application.Users.Contracts;
using Cumulus.Data;
using Data;
using MaktabDataContracts.Enums;
using Users.Repository;
using Users.Utils.Implementation;

namespace Application.Users.Repository.Implementation
{
    public class TempUserRepository : DbRepository, ITempUserRepository
    {
        public TempUserRepository(IDatabase database) : base(database)
        {
        }

        public async Task<UserInformation> AddTemporaryUser(UserRegistrationInformation userInformation)
        {
            using (var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false))
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"insert into temp_user_info (UserId, FamilyId, FirstName, LastName, Email, Phone, UserName, Password, EmailVerificationCode, PhoneVerificationCode, IsActive, CreatedAt, UpdatedOn, Relationship,UserRole)"
                    + "Values(@userId, @familyId, @firstName, @lastName, @email, @phone, @userName, @password, @emailVerificationCode, @phoneVerficationCode, @isActive, @createdAt, @updatedOn, @relationship, @userRole)";

                    var password = PasswordHelper.HashPassword(userInformation.Password);
                    cmd.AddParameter("@userId", userInformation.UserId.ToByteArray());
                    cmd.AddParameter("@familyId", userInformation.FamilyId.ToByteArray());
                    cmd.AddParameter("@firstName", userInformation.FirstName);
                    cmd.AddParameter("@lastName", userInformation.LastName);
                    cmd.AddParameter("@email", userInformation.Email);
                    cmd.AddParameter("@phone", userInformation.Phone);
                    cmd.AddParameter("@userName", userInformation.UserName);
                    cmd.AddParameter("@password", password);
                    cmd.AddParameter("@emailVerificationCode", userInformation.EmailVerificationCode);
                    cmd.AddParameter("@phoneVerficationCode", userInformation.PhoneVerificationCode);
                    cmd.AddParameter("@isActive", userInformation.IsActive);
                    cmd.AddParameter("@createdAt", userInformation.CreatedAt);
                    cmd.AddParameter("@updatedOn", userInformation.CreatedAt);
                    cmd.AddParameter("@relationship", (int)userInformation.Relationship);
                    cmd.AddParameter("@userRole", (int)userInformation.UserRole);
                    
                    if (await cmd.ExecuteNonQueryAsync().ConfigureAwait(false) > 0)
                    {
                        return new UserInformation
                        {
                            UserId = userInformation.UserId,
                            FamilyId = userInformation.FamilyId,
                            FirstName = userInformation.FirstName,
                            LastName = userInformation.LastName,
                            UserName = userInformation.UserName,
                            Phone = userInformation.Phone,
                            Email =   userInformation.Email,
                            Password = password,
                            IsActive   = userInformation.IsActive,
                            CreatedAt = userInformation.CreatedAt,
                            UpdatedOn = userInformation.CreatedAt,
                            Relationship = userInformation.Relationship,
                            UserRole = userInformation.UserRole
                        };
                    }
                    else
                    {
                        return null;
                    }
                }
            }
        }

        public async Task<bool> VerifyTempUserVerificationCodes(UserVerification userVerification)
        {
            using (var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false))
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"Select userId from temp_user_info where UPPER(EmailVerificationCode) like UPPER(@emailVerificationCode) ";

                    if (string.IsNullOrEmpty(userVerification.PhoneVerificationCode))
                    {
                        cmd.CommandText += " or PhoneVerificationCode = @phoneVerificationCode  ";
                    }
                    cmd.CommandText += " and UserId =@userId";

                    cmd.AddParameter("@userId", userVerification.UserId.ToByteArray());
                    cmd.AddParameter("@emailVerificationCode", userVerification.EmailVerificationCode);
                    cmd.AddParameter("@phoneVerificationCode", userVerification.PhoneVerificationCode);
                    var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
                    return reader.HasRows;
                }
            }
        }

        public async Task<bool> CheckIfTempUserAlreadyRegistered(string email, string phone)
        {
            using (var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false))
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"Select userId from temp_user_info where UPPER(Email) like UPPER(@email) or Phone = @phone";
                    cmd.AddParameter("@email", email);
                    cmd.AddParameter("@phone", phone);
                    var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
                    return reader.HasRows;
                }
            }
        }

        public async Task<bool> CheckIfTempUserNameExisit(string userName)
        {
            using (var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false))
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"Select userId from temp_user_info where Upper(UserName) like Upper(@userName)  Or Upper(Email) like Upper(@userName) ";
                    cmd.AddParameter("@userName", userName);
                    var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
                    return reader.HasRows;
                }
            }
        }

        public async Task<bool> DeleteTempUser(Guid userId)
        {
            using (var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false))
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"Delete from temp_user_info where UserId = @userId";
                    cmd.AddParameter("@userId", userId.ToByteArray());
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }

        public async Task<UserInformation> GetTempUserInformation(Guid userId)
        {
            using (var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false))
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"Select UserId, FirstName, LastName, Email, Phone, UserName, Password, IsActive, CreatedAt, UpdatedOn, IsTempPassword, Relationship, FamilyId from temp_user_info" +
                        " where UserId = @userId";

                    cmd.AddParameter("@userId", userId.ToByteArray());
                    using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
                 
                    if(!await reader.ReadAsync().ConfigureAwait(false))
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
                    var isTempPassword = reader.GetBoolean(10);
                    var relationship = (Relationship)reader.GetInt32(11);
                    var familyId = reader.GetGuidFromByteArray(12);
                    var userRole = (UserRoleType)reader.GetInt32(13);

                    return new UserInformation
                    {
                        UserId = userId,
                        FamilyId = familyId,
                        FirstName = fristName,
                        LastName = lastName,
                        Email = email,
                        Password = password,
                        Phone = phone,
                        UserName = userName,
                        IsActive = isActive,
                        CreatedAt = CreatedAt,
                        UpdatedOn = UpdatedOn,
                        IsTempPassword = isTempPassword,
                        IsAdmin = false,
                        Relationship = relationship,
                        UserRole = userRole
                    };
                }
            }
        }

        public async Task<UserInformation> UpdateRegistrationActivationCodes(UpdateUserRegistrationInformation userInformation)
        {
            using (var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false))
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"Update temp_user_info SET EmailVerificationCode = @emailVerificationCode, PhoneVerificationCode= @phoneVerificationCode where UserId = @userId";

                    cmd.AddParameter("@userId", userInformation.UserId.ToByteArray());
                    cmd.AddParameter("@emailVerificationCode", userInformation.EmailVerificationCode);
                    cmd.AddParameter("@phoneVerificationCode", userInformation.PhoneVerificationCode);
                    cmd.AddParameter("@updatedOn", DateTime.Now);
                 
                    if (await cmd.ExecuteNonQueryAsync().ConfigureAwait(false) > 0)
                    {
                        return await GetTempUserInformation(userInformation.UserId).ConfigureAwait(false);
                    }

                    return null;
                }
            }
        }
        public async Task<IEnumerable<UserInformation>> GetAllTempUsersInformation(bool ifOnlyActive = true)
        {
            var results = new List<UserInformation>();

            using (var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false))
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"Select UserId, FirstName, LastName, Email, Phone, UserName, Password, IsActive, CreatedAt, UpdatedOn, isTempPassword, Relationship, UserRole, FamilyId from temp_user_info";

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
                        var isTempPassword = reader.GetBoolean(10);
                        var relationship = (Relationship)reader.GetInt32(11);
                        var userRole = (UserRoleType)reader.GetInt32(12);
                        var familyId = reader.GetGuidFromByteArray(13);

                        results.Add(new UserInformation
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
                            IsAdmin = false,
                            IsTempPassword = isTempPassword,
                            Relationship = relationship,
                            UserRole = userRole
                        });
                    }
                }
            }
            return results;
        }
        public async Task<UserInformation> GetTempUserInformation(string userName, string password)
        {
            using (var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false))
            {
                using (var cmd = conn.CreateCommand())
                {
                    // 1. Remove password from WHERE clause
                    cmd.CommandText = @"
                SELECT UserId, FirstName, LastName, Email, Phone, UserName, Password, IsTempPassword, Relationship, FamilyId, UserRole   
                FROM temp_user_info 
                WHERE (UPPER(UserName) = UPPER(@userName) OR UPPER(Email) = UPPER(@userName)) AND IsActive = true";

                    cmd.AddParameter("@userName", userName);

                    using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);

                    if (!await reader.ReadAsync().ConfigureAwait(false))
                        return null;

                    // 2. Extract stored hashed password
                    var storedHashedPassword = reader.GetString(6);

                    // 3. Verify password using helper
                    if (!PasswordHelper.VerifyPassword(password, storedHashedPassword))
                    {
                        return null;
                    }

                    // 4. If password matches, build and return user info
                    var id = reader.GetGuidFromByteArray(0);
                    var fristName = reader.GetString(1);
                    var lastName = reader.GetString(2);
                    var email = reader.GetString(3);
                    var phone = reader.GetString(4);
                    var userNam = reader.GetString(5);
                    var isTempPassword = reader.GetBoolean(7);
                    var relationship = (Relationship)reader.GetInt32(8);
                    var familyId = reader.GetGuidFromByteArray(9);
                    var userRole = (UserRoleType)reader.GetInt32(10);

                    return new UserInformation
                    {
                        UserId = id,
                        FamilyId = familyId,
                        FirstName = fristName,
                        LastName = lastName,
                        Email = email,
                        Phone = phone,
                        UserName = userNam,
                        IsTempPassword = isTempPassword,
                        Relationship = relationship,
                        UserRole = userRole
                    };
                }
            }
        }
    }
}
