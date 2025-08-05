using Application.Users.Contracts;
using Cumulus.Data;
using Data;
using MaktabDataContracts.Enums;
using MaktabDataContracts.Models;
using MaktabDataContracts.Requests.Cards;
using MaktabDataContracts.Requests.Users;
using MaktabDataContracts.Responses.Users;
using Users.Repository;

namespace Application.Users.Repository.Implementation
{
    public class ExtendedUserInformationRepository : DbRepository, IExtendedUserInformationRepository
    {
        public ExtendedUserInformationRepository(IDatabase database) : base(database)
        {
        }

        public async Task<ExtendedUserInformationDetail> AddExtendedUserInformation(ExtendedUserInformationDetail userInformation)
        {
            using (var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false))
            {

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"insert into extended_user_info (`UserId`, `IdNumber`, `Occupation`,  `BusinesName`, `DateOfBirth`, "
                        + "`IsActive`, `CreatedAt`, `UpdatedOn`)"
                        + " Values(@userId, @idNumber, @occupation, @businesName,@dateOfBirth,  @isActive, @createdAt, @updatedOn )";

                    cmd.AddParameter("@userId", userInformation.UserId.ToByteArray());
                    cmd.AddParameter("@idNumber", userInformation.IdNumber);
                    cmd.AddParameter("@occupation", userInformation.Occupation);
                    cmd.AddParameter("@dateOfBirth", userInformation.DateOfBirth);
                    cmd.AddParameter("@businesName", userInformation.BusinesName);
                    cmd.AddParameter("@isActive", userInformation.IsActive);
                    cmd.AddParameter("@createdAt", userInformation.CreatedAt);
                    cmd.AddParameter("@updatedOn", userInformation.UpdatedOn);

                    if (await cmd.ExecuteNonQueryAsync().ConfigureAwait(false) > 0)
                    {
                        return userInformation;   
                    }
                    else
                    {
                        return null;
                    }
                }
            }
        }

        public async Task<bool> CheckIfExtendedUserInformationExisit(Guid userId)
        {
            using (var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false))
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"Select userId from extended_user_info where userId = @userId and IsActive = true";
                    cmd.AddParameter("@userId", userId.ToByteArray());
                    var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
                    return reader.HasRows;
                }
            }
        }

        public async Task<bool> DeleteExtendedUserInformation(Guid userId, bool ifHardDelete = false)
        {
            using (var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false))
            {
                using (var cmd = conn.CreateCommand())
                {
                    if (ifHardDelete)
                    {
                        cmd.CommandText = @"Delete from extended_user_info where UserId = @userId";
                    }
                    else
                    {
                        cmd.CommandText = @"Update extended_user_info SET IsActive = false where UserId = @userId";
                    }

                    cmd.AddParameter("@userId", userId.ToByteArray());
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }

        public async Task<ExtendedUserInformationDetail> GetExtendedUserInformation(Guid userId)
        {
            using (var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false))
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"Select `UserId`, `IdNumber`, `DateOfBirth`, `Occupation`, `BusinesName`,  " +
                        " `IsActive`, `CreatedAt`, `UpdatedOn`  from extended_user_info " +
                        " where UserId = @userId and IsActive = True";

                    cmd.AddParameter("@userId", userId.ToByteArray());
                    using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);

                    if (!await reader.ReadAsync().ConfigureAwait(false))
                    {
                        return null;
                    }

                    var id = reader.GetGuidFromByteArray(0);
                    var idNumber = reader.GetString(1);
                    var dateOfBirth = reader.GetDateTime(2);
                    var occupation = reader.GetString(3);
                    var businesName = reader.GetString(4);
                    var isActive = reader.GetBoolean(5);
                    var createdAt = reader.GetDateTime(6);
                    var updatedOn = reader.GetDateTime(7);
                    
                    return new ExtendedUserInformationDetail
                    {
                        UserId = id,
                        IdNumber = idNumber,
                        Occupation = occupation,
                        BusinesName = businesName,
                        DateOfBirth = dateOfBirth,
                        IsActive = isActive,
                        CreatedAt = createdAt,
                        UpdatedOn = updatedOn,
                    };
                }
            }
        }

        public async Task<ExtendedUserInformationDetail> UpdateExtendedUserInformation(ExtendedUserInformationDetail userInformation)
        {
            using (var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false))
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"Update extended_user_info SET IdNumber = @idNumber, DateOfBirth = @dateOfBirth, Occupation = @occupation, BusinesName = @businesName, UpdatedOn = @updatedOn  where UserId = @userId";

                    cmd.AddParameter("@userId", userInformation.UserId.ToByteArray());
                    cmd.AddParameter("@idNumber", userInformation.IdNumber);
                    cmd.AddParameter("@businesName", userInformation.BusinesName);
                    cmd.AddParameter("@dateOfBirth", userInformation.DateOfBirth);
                    cmd.AddParameter("@occupation", userInformation.Occupation);
                    cmd.AddParameter("@updatedOn", DateTime.Now);
                    
                    if (await cmd.ExecuteNonQueryAsync().ConfigureAwait(false) > 0)
                    {
                        return await GetExtendedUserInformation(userInformation.UserId).ConfigureAwait(false);
                    }

                    return null;
                }
            }
        }
    }
}
