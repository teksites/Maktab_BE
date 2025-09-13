using Application.Users.Contracts;
using Cumulus.Data;
using Data;
using Users.Contracts;
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
                    cmd.CommandText = @"insert into extended_user_info (`UserId`, `FamilyId`, `SIN`,  `IsActiveTaxCreditRecipient`,  `AddressId`, "
                        + "`IsActive`, `CreatedAt`, `UpdatedOn`)"
                        + " Values(@userId, @familyId, @sin, @isActiveTaxCreditRecipient,  @addressId,  @isActive, @createdAt, @updatedOn )";

                    cmd.AddParameter("@userId", userInformation.UserId.ToByteArray());
                    cmd.AddParameter("@familyId", userInformation.FamilyId.ToByteArray());
                    cmd.AddParameter("@sin", userInformation.SIN);
                    cmd.AddParameter("@addressId", userInformation.AddressId.ToByteArray());
                    cmd.AddParameter("@isActive", userInformation.IsActive);
                    cmd.AddParameter("@createdAt", userInformation.CreatedAt);
                    cmd.AddParameter("@updatedOn", userInformation.UpdatedOn);
                    cmd.AddParameter("@isActiveTaxCreditRecipient", userInformation.IsActiveTaxCreditRecipient);

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

        public async Task<bool> CheckIfExtendedFamilyInformationExisit(Guid familyId)
        {
            using (var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false))
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"Select userId from extended_user_info where FamilyId = @familyId and IsActive = true";
                    cmd.AddParameter("@familyId", familyId.ToByteArray());
                    var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
                    return reader.HasRows;
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

        public async Task<bool> DeleteFamilyExtendedUserInformation(Guid familyId, bool ifHardDelete = false)
        {
            using (var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false))
            {
                using (var cmd = conn.CreateCommand())
                {
                    if (ifHardDelete)
                    {
                        cmd.CommandText = @"Delete from extended_user_info where FamilyId = @familyId";
                    }
                    else
                    {
                        cmd.CommandText = @"Update extended_user_info SET IsActive = false where FamilyId = @familyId";
                    }

                    cmd.AddParameter("@familyId", familyId.ToByteArray());
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
                    cmd.CommandText = @"Select `UserId`, `FamilyId`, `SIN`,  `AddressId`, "
                        + "`IsActive`, `CreatedAt`, `UpdatedOn`, `IsActiveTaxCreditRecipient`  from extended_user_info " +
                        " where UserId = @userId and IsActive = True";

                    cmd.AddParameter("@userId", userId.ToByteArray());
                    using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);

                    if (!await reader.ReadAsync().ConfigureAwait(false))
                    {
                        return null;
                    }

                    var id = reader.GetGuidFromByteArray(0);
                    var familyId = reader.GetGuidFromByteArray(1);
                    var sin = reader.GetString(2);
                    var addressId = reader.GetGuidFromByteArray(3);
                    var isActive = reader.GetBoolean(4);
                    var createdAt = reader.GetDateTime(5);
                    var updatedOn = reader.GetDateTime(6);
                    var isActiveTaxCreditRecipient = reader.GetBoolean(7);

                    return new ExtendedUserInformationDetail
                    {
                        UserId = id,
                        FamilyId = familyId,
                        SIN = sin,
                        AddressId = addressId,
                        IsActive = isActive,
                        CreatedAt = createdAt,
                        UpdatedOn = updatedOn,
                        IsActiveTaxCreditRecipient = isActiveTaxCreditRecipient
                    };
                }
            }
        }

        public async Task<ExtendedUserInformationDetail> GetFamilyExtendedUserInformation(Guid familyId)
        {
            using (var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false))
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"Select `UserId`, `FamilyId`, `SIN`,  `AddressId`, "
                        + "`IsActive`, `CreatedAt`, `UpdatedOn`,  `IsActiveTaxCreditRecipient`  from extended_user_info " +
                        " where FamilyId = @familyId and IsActive = True";

                    cmd.AddParameter("@familyId", familyId.ToByteArray());
                    using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);

                    if (!await reader.ReadAsync().ConfigureAwait(false))
                    {
                        return null;
                    }

                    var id = reader.GetGuidFromByteArray(0);
                    var family = reader.GetGuidFromByteArray(1);
                    var sin = reader.GetString(2);
                    var addressId = reader.GetGuidFromByteArray(3);
                    var isActive = reader.GetBoolean(4);
                    var createdAt = reader.GetDateTime(5);
                    var updatedOn = reader.GetDateTime(6);
                    var isActiveTaxCreditRecipient = reader.GetBoolean(7);

                    return new ExtendedUserInformationDetail
                    {
                        UserId = id,
                        FamilyId = family,
                        SIN = sin,
                        AddressId = addressId,
                        IsActive = isActive,
                        CreatedAt = createdAt,
                        UpdatedOn = updatedOn,
                        IsActiveTaxCreditRecipient = isActiveTaxCreditRecipient
                    };
                }
            }
        }

        public async Task<ExtendedUserInformationDetail> UpdateExtendedUserInformation(AddExtendedUserInformationInternal userInformation)
        {
            using (var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false))
            {
                using (var cmd = conn.CreateCommand())
                {
                    var query = @"Update extended_user_info SET  IsActiveTaxCreditRecipient = @isActiveTaxCreditRecipient, ";

                    cmd.AddParameter("@userId", userInformation.UserId.ToByteArray());
                    //cmd.AddParameter("@sin", userInformation.SIN);
                    if (userInformation.AddressId != null) 
                    {
                        cmd.AddParameter("@addressId", userInformation.AddressId.Value.ToByteArray());
                        query += " AddressId = @addressId, ";
                    }

                    cmd.AddParameter("@updatedOn", DateTime.Now);
                    cmd.AddParameter("@isActiveTaxCreditRecipient", userInformation.IsActiveTaxCreditRecipient);

                    cmd.CommandText = query+ "  UpdatedOn = @updatedOn where UserId = @userId";

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
