using Application.Users.Contracts;
using Cumulus.Data;
using Data;
using MaktabDataContracts.Enums;
using MaktabDataContracts.Requests.Children;
using Users.Repository;

namespace Application.Users.Repository.Implementation
{
    public class UserChildsRepository : DbRepository, IUserChildsRepository
    {
        public UserChildsRepository(IDatabase database) : base(database)
        {
        }

        public async Task<Child> AddChild(Child child)
        {
            using (var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false))
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"insert into child_information (`ChildId`, `FamilyId`, `FirstName`, `LastName`, `OtherHealthConditions`, `Allergies`, `WillUseDayCareServices`, `DateOfBirth`, `Gender`, " +
                        "`RAMQExpiry`, `RAMQNumber`, `RAMQSequenceNumber`, `IsActive`,`CreatedAt`, `UpdatedOn`)"
                        + " Values(@childId, @familyId, @firstName, @lastName, @otherHealthConditions, @allergies, @willUseDayCareServices, @dateOfBirth, @gender, @rAMQExpiry, @rAMQNumber, @rAMQSequenceNumber, @isActive, @createdAt," +
                        " @updatedOn )";

                    cmd.AddParameter("@childId", child.ChildId.ToByteArray());
                    cmd.AddParameter("@familyId", child.FamilyId.ToByteArray());
                    cmd.AddParameter("@firstName", child.FirstName);
                    cmd.AddParameter("@lastName", child.LastName);;
                    cmd.AddParameter("@OtherHealthConditions", child.OtherHealthConditions);
                    cmd.AddParameter("@allergies", child.Allergies);
                    cmd.AddParameter("@willUseDayCareServices", child.WillUseDayCareServices);
                    cmd.AddParameter("@dateOfBirth", child.DateOfBirth);
                    cmd.AddParameter("@gender", child.Gender);
                    cmd.AddParameter("@rAMQExpiry", child.RAMQExpiry);
                    cmd.AddParameter("@rAMQNumber", child.RAMQNumber);
                    cmd.AddParameter("@rAMQSequenceNumber", child.RAMQSequenceNumber);
                    cmd.AddParameter("@isActive", child.IsActive);
                    cmd.AddParameter("@createdAt", child.CreatedAt);
                    cmd.AddParameter("@updatedOn", child.UpdatedOn);

                    if (await cmd.ExecuteNonQueryAsync().ConfigureAwait(false) > 0)
                    {
                        return child;
                    }
                    else
                    {
                        return null;
                    }
                }
            }
        }

        public async Task<bool> CheckIfChildExisit(UserChildToVerify child)
        {
            using (var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false))
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"Select ChildId from child_information where familyId = @familyId AND IsActive = true AND UPPER(RAMQNumber) like UPPER(@rAMQNumber)";

                    cmd.AddParameter("@familyId", child.familyId.ToByteArray());
                    cmd.AddParameter("@rAMQNumber", child.RAMQNumber);
 
                    var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
                    return reader.HasRows;
                }
            }
        }

        public async Task<bool> DeleteChild(Guid childId, bool ifHardDelete = false)
        {
            using (var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false))
            {
                using (var cmd = conn.CreateCommand())
                {
                    if (ifHardDelete)
                    {
                        cmd.CommandText = @"Delete from child_information where ChildId = @childId";
                    }
                    else
                    {
                        cmd.CommandText = @"Update child_information SET IsActive = false where ChildId = @childId";
                    }

                    cmd.AddParameter("@childId", childId.ToByteArray());
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }

        public async Task<bool> DeleteFamilyChildren(Guid familyId, bool ifHardDelete = false)
        {
            using (var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false))
            {
                using (var cmd = conn.CreateCommand())
                {
                    if (ifHardDelete)
                    {
                        cmd.CommandText = @"Delete from child_information where FamilyId = @familyId";
                    }
                    else
                    {
                        cmd.CommandText = @"Update child_information SET IsActive = false where FamilyId = @familyId";
                    }

                    cmd.AddParameter("@familyId", familyId.ToByteArray());
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }

        public async Task<Child> GetChild(Guid childId)
        {
            using (var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false))
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"Select `ChildId`, `FamilyId`, `FirstName`, `LastName`, `OtherHealthConditions`, `Allergies`, `WillUseDayCareServices`, `DateOfBirth`, `Gender`, " +
                        "`RAMQExpiry`, `RAMQNumber`, `RAMQSequenceNumber`, `IsActive`,`CreatedAt`, `UpdatedOn` from child_information " +
                        " where ChildId = @childId and IsActive = True";

                    cmd.AddParameter("@childId", childId.ToByteArray());
                    using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);

                    if (!await reader.ReadAsync().ConfigureAwait(false))
                    {
                        return null;
                    }

                    var id = reader.GetGuidFromByteArray(0);
                    var familyId = reader.GetGuidFromByteArray(1);
                    var firstName = reader.GetString(2);
                    var lastName = reader.GetString(3);
                    var otherHealthConditions = reader.GetString(4);
                    var allergies = reader.GetString(5);
                    var willUseDayCareServices = reader.GetBoolean(6);
                    var dateOfBirth = reader.GetDateTime(7);
                    var gender = (Gender)reader.GetInt32(8);
                    var rAMQExpiry = reader.GetDateTime(9);
                    var rAMQNumber = reader.GetString(10);
                    var rAMQSequenceNumber = reader.GetInt32(11);
                    var isActive = reader.GetBoolean(12);
                    var createdAt = reader.GetDateTime(13);
                    var updatedOn = reader.GetDateTime(14);

                    return new Child
                    {
                        ChildId = id,
                        FamilyId = familyId,
                        FirstName = firstName,
                        LastName = lastName,
                        OtherHealthConditions = otherHealthConditions,
                        Allergies = allergies,
                        WillUseDayCareServices = willUseDayCareServices,
                        DateOfBirth = dateOfBirth,
                        Gender = gender,
                        RAMQExpiry = rAMQExpiry,
                        RAMQNumber = rAMQNumber,
                        RAMQSequenceNumber = rAMQSequenceNumber,
                        IsActive = isActive,
                        CreatedAt = createdAt,
                        UpdatedOn = updatedOn,
                    };
                }
            }
        }

        public async Task<IEnumerable<Child>> GetFamilyChildren(Guid familyId)
        {
            var results = new List<Child>();

            using (var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false))
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"Select `ChildId`, `FamilyId`, `FirstName`, `LastName`, `OtherHealthConditions`, `Allergies`, `WillUseDayCareServices`, `DateOfBirth`, `Gender`, " +
                        "`RAMQExpiry`, `RAMQNumber`, `RAMQSequenceNumber`, `IsActive`,`CreatedAt`, `UpdatedOn` from child_information " +
                            " where FamilyId = @familyId and IsActive = True";

                    cmd.AddParameter("@familyId", familyId.ToByteArray());

                    using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);

                    while (await reader.ReadAsync().ConfigureAwait(false))
                    {

                        //var iban = "";
                        //if (!await reader.IsDBNullAsync(6).ConfigureAwait(false))
                        //{
                        //    iban = reader.GetString(6);
                        //}

                        var id = reader.GetGuidFromByteArray(0);
                        var familyIdLinked = reader.GetGuidFromByteArray(1);
                        var firstName = reader.GetString(2);
                        var lastName = reader.GetString(3);
                        var otherHealthConditions = reader.GetString(4);
                        var allergies = reader.GetString(5);
                        var willUseDayCareServices = reader.GetBoolean(6);
                        var dateOfBirth = reader.GetDateTime(7);
                        var gender = (Gender)reader.GetInt32(8);
                        var rAMQExpiry = reader.GetDateTime(9);
                        var rAMQNumber = reader.GetString(10);
                        var rAMQSequenceNumber = reader.GetInt32(11);
                        var isActive = reader.GetBoolean(12);
                        var createdAt = reader.GetDateTime(13);
                        var updatedOn = reader.GetDateTime(14);

                        results.Add(new Child
                        {
                            ChildId = id,
                            FamilyId = familyIdLinked,
                            FirstName = firstName,
                            LastName = lastName,
                            OtherHealthConditions = otherHealthConditions,
                            Allergies = allergies,
                            WillUseDayCareServices = willUseDayCareServices,
                            DateOfBirth = dateOfBirth,
                            Gender = gender,
                            RAMQExpiry = rAMQExpiry,
                            RAMQNumber = rAMQNumber,
                            RAMQSequenceNumber = rAMQSequenceNumber,
                            IsActive = isActive,
                            CreatedAt = createdAt,
                            UpdatedOn = updatedOn,
                        });
                    }
                }
            }
            return results;
        }

        public Task<Child> UpdateChild(UpdateChildRequest child)
        {
            throw new NotImplementedException();
        }
    }
}
