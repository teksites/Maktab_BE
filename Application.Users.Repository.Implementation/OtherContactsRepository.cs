using Application.Users.Contracts;
using Cumulus.Data;
using Data;
using MaktabDataContracts.Enums;
using System.Text;
using Users.Repository;

namespace Application.Users.Repository.Implementation
{
    public class OtherContactsRepository : DbRepository, IOtherContactsRepository
    {
        public OtherContactsRepository(IDatabase database) : base(database)
        {
        }

        public async Task<OtherContactInformation> AddOtherContact(OtherContactInformation otherContactInformation)
        {
            using (var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false))
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"insert into other_contacts_information (`ContactId`, `FamilyId`, `FirstName`, `LastName`,  `Phone`, `ContactType`, `Relationship`, "
                        + "`IsActive`, `CreatedAt`, `UpdatedOn`)"
                        + " Values(@contactId, @familyId, @firstName, @lastName, @phone, @contactType, @relationship, @isActive, @createdAt, @updatedOn)";

                    cmd.AddParameter("@contactId", otherContactInformation.ContactId.ToByteArray());
                    cmd.AddParameter("@familyId", otherContactInformation.FamilyId.ToByteArray());
                    cmd.AddParameter("@firstName", otherContactInformation.FirstName);
                    cmd.AddParameter("@lastName", otherContactInformation.LastName);
                    cmd.AddParameter("@phone", otherContactInformation.Phone);
                    cmd.AddParameter("@contactType", otherContactInformation.ContactType);
                    cmd.AddParameter("@relationship", otherContactInformation.Relationship);
                    cmd.AddParameter("@isActive", otherContactInformation.IsActive);
                    cmd.AddParameter("@createdAt", otherContactInformation.CreatedAt);
                    cmd.AddParameter("@updatedOn", otherContactInformation.UpdatedOn);

                    if (await cmd.ExecuteNonQueryAsync().ConfigureAwait(false) > 0)
                    {
                        return otherContactInformation;   
                    }
                    else
                    {
                        return null;
                    }
                }
            }
        }

        public async Task<bool> CheckIfOtherContactExisit(Guid familyId, string phone)
        {
            using (var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false))
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"Select ContactId from other_contacts_information where FamilyId = @familyId and UPPER(Phone) like UPPER(@phone) and IsActive = true";
                    cmd.AddParameter("@familyId", familyId);
                    cmd.AddParameter("@phone", phone);
                    var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
                    return reader.HasRows;
                }
            }
        }

        public async Task<bool> DeleteOtherContact(Guid otherContactId, bool ifHardDelete = false)
        {
            using (var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false))
            {
                using (var cmd = conn.CreateCommand())
                {
                    if (ifHardDelete)
                    {
                        cmd.CommandText = @"Delete from other_contacts_information where ContactId = @otherContactId";
                    }
                    else
                    {
                        cmd.CommandText = @"Update other_contacts_information SET IsActive = false where ContactId = @otherContactId";
                    }

                    cmd.AddParameter("@otherContactId", otherContactId.ToByteArray());
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }

        public async Task<bool> DeleteFamilyOtherContact(Guid familyId, bool ifHardDelete = false)
        {
            using (var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false))
            {
                using (var cmd = conn.CreateCommand())
                {
                    if (ifHardDelete)
                    {
                        cmd.CommandText = @"Delete from other_contacts_information where FamilyId = @familyId";
                    }
                    else
                    {
                        cmd.CommandText = @"Update other_contacts_information SET IsActive = false where FamilyId = @familyId";
                    }

                    cmd.AddParameter("@familyId", familyId.ToByteArray());
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }

        public async Task<OtherContactInformation> GetOtherContact(Guid otherContactId)
        {
            using (var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false))
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"Select `ContactId`, `FamilyId`, `FirstName`, `LastName`,  `Phone`, `ContactType`, `Relationship`, "
                        + "`IsActive`, `CreatedAt`, `UpdatedOn` from other_contacts_information " +
                        " where ContactId = @otherContactId";

                    cmd.AddParameter("@otherContactId", otherContactId.ToByteArray());
                    using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);

                    if (!await reader.ReadAsync().ConfigureAwait(false))
                    {
                        return null;
                    }

                    var id = reader.GetGuidFromByteArray(0);
                    var famailyId = reader.GetGuidFromByteArray(1);
                    var firstName = reader.GetString(2);
                    var lastName = reader.GetString(3);
                    var phone = reader.GetString(4);
                    var contactType = (ContactType)reader.GetInt32(5);
                    var relationship = (Relationship)reader.GetInt32(6);
                    var isActive = reader.GetBoolean(7);
                    var createdAt = reader.GetDateTime(8);
                    var updatedOn = reader.GetDateTime(9);

                    return new OtherContactInformation
                    {
                        ContactId = id,
                        FamilyId = famailyId,
                        FirstName = firstName,
                        LastName = lastName,
                        Phone = phone,
                        ContactType = contactType,
                        Relationship = relationship,
                        IsActive = isActive,
                        CreatedAt = createdAt,
                        UpdatedOn = updatedOn,
                    };
                }
            }
        }

        public async Task<OtherContactInformation> GetOtherContactByPhoneNumber(string phoneNumber)
        {
            using (var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false))
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"Select `ContactId`, `FamilyId`, `FirstName`, `LastName`,  `Phone`, `ContactType`, `Relationship`, "
                        + "`IsActive`, `CreatedAt`, `UpdatedOn` from other_contacts_information " +
                        " where  UPPER(Phone) like UPPER(@phoneNumber) and IsActive = true";

                    cmd.AddParameter("@phoneNumber", phoneNumber);
                    using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);

                    if (!await reader.ReadAsync().ConfigureAwait(false))
                    {
                        return null;
                    }

                    var id = reader.GetGuidFromByteArray(0);
                    var famailyId = reader.GetGuidFromByteArray(1);
                    var firstName = reader.GetString(2);
                    var lastName = reader.GetString(3);
                    var phone = reader.GetString(4);
                    var contactType = (ContactType)reader.GetInt32(5);
                    var relationship = (Relationship)reader.GetInt32(6);
                    var isActive = reader.GetBoolean(7);
                    var createdAt = reader.GetDateTime(8);
                    var updatedOn = reader.GetDateTime(9);

                    return new OtherContactInformation
                    {
                        ContactId = id,
                        FamilyId = famailyId,
                        FirstName = firstName,
                        LastName = lastName,
                        Phone = phone,
                        ContactType = contactType,
                        Relationship = relationship,
                        IsActive = isActive,
                        CreatedAt = createdAt,
                        UpdatedOn = updatedOn,
                    };
                }
            }
        }

        public async Task<IEnumerable<OtherContactInformation>> GetFamilyOtherContacts(Guid familyId, IEnumerable<ContactType> contactTypes)
        {
            var results = new List<OtherContactInformation>();

            using (var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false))
            {
                using (var cmd = conn.CreateCommand())
                {

                    var sb = new StringBuilder();
                    sb.AppendLine("Select `ContactId`, `FamilyId`, `FirstName`, `LastName`,  `Phone`, `ContactType`, `Relationship`, ");
                    sb.AppendLine("`IsActive`, `CreatedAt`, `UpdatedOn` from other_contacts_information ");
                    sb.AppendLine("SubmittedDate, TransferDate, IsActive, CreatedAt, UpdatedOn, ExchangeRateId, RecepientName, CurrencySymbol, TransactionName");
                    sb.AppendLine(" where FamilyId = @familyId and IsActive = True AND ContactTypeIN (");

                    // Dynamically build parameters for the IN clause
                    var contactTypesParams = contactTypes.Select((contactType, index) => {
                        string paramName = $"@state{index}";
                        cmd.AddParameter(paramName, (int)contactType);
                        return paramName;
                    }).ToList();
                    sb.AppendLine(string.Join(", ", contactTypesParams));
                    sb.AppendLine(") ");


                    cmd.AddParameter("@familyId", familyId.ToByteArray());
                    cmd.CommandText = sb.ToString();

                    using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);

                    while (await reader.ReadAsync().ConfigureAwait(false))
                    {
                        var id = reader.GetGuidFromByteArray(0);
                        var famailyId = reader.GetGuidFromByteArray(1);
                        var firstName = reader.GetString(2);
                        var lastName = reader.GetString(3);
                        var phone = reader.GetString(4);
                        var contactTyp = (ContactType)reader.GetInt32(5);
                        var relationship = (Relationship)reader.GetInt32(6);
                        var isActive = reader.GetBoolean(7);
                        var createdAt = reader.GetDateTime(8);
                        var updatedOn = reader.GetDateTime(9);

                        results.Add(new OtherContactInformation
                        {
                            ContactId = id,
                            FamilyId = famailyId,
                            FirstName = firstName,
                            LastName = lastName,
                            Phone = phone,
                            ContactType = contactTyp,
                            Relationship = relationship,
                            IsActive = isActive,
                            CreatedAt = createdAt,
                            UpdatedOn = updatedOn,
                        });
                    }
                }
            }

            return results;
        }

        public Task<OtherContactInformation> UpdateOtherContact(OtherContactInformation otherContactInformation)
        {
            throw new NotImplementedException("You cannot update the contact. Please delete and add again.");
        }
    }
}
