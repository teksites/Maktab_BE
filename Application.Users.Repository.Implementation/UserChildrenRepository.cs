using Application.Users.Contracts;
using Cumulus.Data;
using Data;
using MaktabDataContracts.Enums;
using MaktabDataContracts.Requests.Children;
using System.Data.Common;
using Users.Repository;

namespace Application.Users.Repository.Implementation
{
    public class UserChildrenRepository : DbRepository, IUserChildrenRepository
    {
        public UserChildrenRepository(IDatabase database) : base(database) { }

        public async Task<Child> AddChild(Child child)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false);
            using var tx = await conn.BeginTransactionAsync().ConfigureAwait(false);
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"INSERT INTO child_information 
                (ChildId, FamilyId, FirstName, LastName, OtherHealthConditions, HasAllergy, Allergies, AcedemicGroupType, DateOfBirth, Gender, RAMQExpiry, RAMQNumber, RAMQSequenceNumber, IsActive, CreatedAt, UpdatedOn, RegistrationNumber)
                VALUES 
                (@ChildId, @FamilyId, @FirstName, @LastName, @OtherHealthConditions, @HasAllergy, @Allergies, @AcedemicGroupType, @DateOfBirth, @Gender, @RAMQExpiry, @RAMQNumber, @RAMQSequenceNumber, @IsActive, @CreatedAt, @UpdatedOn, @RegistrationNumber)";
            cmd.Transaction = tx;

            child.RegistrationNumber = await GetNextRegistrationNumber(conn, tx).ConfigureAwait(false);

            cmd.AddParameter("@ChildId", child.ChildId.ToByteArray());
            cmd.AddParameter("@FamilyId", child.FamilyId.ToByteArray());
            cmd.AddParameter("@FirstName", child.FirstName);
            cmd.AddParameter("@LastName", child.LastName);
            cmd.AddParameter("@OtherHealthConditions", child.OtherHealthConditions);
            cmd.AddParameter("@HasAllergy", child.HasAllergy);
            cmd.AddParameter("@Allergies", child.Allergies);
            cmd.AddParameter("@AcedemicGroupType", (int)child.AcedemicGroup);
            cmd.AddParameter("@DateOfBirth", child.DateOfBirth);
            cmd.AddParameter("@Gender", (int)child.Gender);
            cmd.AddParameter("@RAMQExpiry", child.RAMQExpiry);
            cmd.AddParameter("@RAMQNumber", child.RAMQNumber);
            cmd.AddParameter("@RAMQSequenceNumber", child.RAMQSequenceNumber);
            cmd.AddParameter("@IsActive", child.IsActive);
            cmd.AddParameter("@CreatedAt", child.CreatedAt);
            cmd.AddParameter("@UpdatedOn", child.UpdatedOn);
            cmd.AddParameter("@RegistrationNumber", child.RegistrationNumber);

            var rows = await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            if (rows > 0)
            {
                await tx.CommitAsync().ConfigureAwait(false);
                return child;
            }

            await tx.RollbackAsync().ConfigureAwait(false);
            return null;
        }

        public async Task<Child> UpdateChild(UpdateChildRequest child)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false);
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"UPDATE child_information SET
                FirstName = @FirstName,
                LastName = @LastName,
                OtherHealthConditions = @OtherHealthConditions,
                Allergies = @Allergies,
                AcedemicGroupType = @AcedemicGroupType,
                DateOfBirth = @DateOfBirth,
                Gender = @Gender,
                RAMQExpiry = @RAMQExpiry,
                RAMQNumber = @RAMQNumber,
                RAMQSequenceNumber = @RAMQSequenceNumber,
                HasAllergy = @HasAllergy,
                UpdatedOn = @UpdatedOn
                WHERE ChildId = @ChildId";

            cmd.AddParameter("@ChildId", child.ChildId.ToByteArray());
            cmd.AddParameter("@FirstName", child.FirstName);
            cmd.AddParameter("@LastName", child.LastName);
            cmd.AddParameter("@OtherHealthConditions", child.OtherHealthConditions);
            cmd.AddParameter("@Allergies", child.Allergies);
            cmd.AddParameter("@AcedemicGroupType", (int)child.AcedemicGroup);
            cmd.AddParameter("@DateOfBirth", child.DateOfBirth);
            cmd.AddParameter("@Gender", (int)child.Gender);
            cmd.AddParameter("@RAMQExpiry", child.RAMQExpiry);
            cmd.AddParameter("@RAMQNumber", child.RAMQNumber);
            cmd.AddParameter("@RAMQSequenceNumber", child.RAMQSequenceNumber);
            cmd.AddParameter("@HasAllergy", child.HasAllergy);
            cmd.AddParameter("@UpdatedOn", DateTime.UtcNow);

            var rows = await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            return rows > 0 ? await GetChild(child.ChildId).ConfigureAwait(false) : null;
        }

        public async Task<bool> DeleteChild(Guid childId, bool ifHardDelete = false)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false);
            using var cmd = conn.CreateCommand();

            if (ifHardDelete)
                cmd.CommandText = @"DELETE FROM child_information WHERE ChildId = @ChildId";
            else
                cmd.CommandText = @"UPDATE child_information SET IsActive = false WHERE ChildId = @ChildId";

            cmd.AddParameter("@ChildId", childId.ToByteArray());
            return await cmd.ExecuteNonQueryAsync().ConfigureAwait(false) > 0;
        }

        public async Task<bool> DeleteFamilyChildren(Guid familyId, bool ifHardDelete = false)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false);
            using var cmd = conn.CreateCommand();

            if (ifHardDelete)
                cmd.CommandText = @"DELETE FROM child_information WHERE FamilyId = @FamilyId";
            else
                cmd.CommandText = @"UPDATE child_information SET IsActive = false WHERE FamilyId = @FamilyId";

            cmd.AddParameter("@FamilyId", familyId.ToByteArray());
            return await cmd.ExecuteNonQueryAsync().ConfigureAwait(false) > 0;
        }

        public async Task<Child> GetChild(Guid childId)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false);
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"SELECT * FROM child_information WHERE ChildId = @ChildId AND IsActive = true";
            cmd.AddParameter("@ChildId", childId.ToByteArray());

            using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
            if (!await reader.ReadAsync().ConfigureAwait(false)) return null;

            return MapToChild(reader);
        }

        public async Task<IEnumerable<Child>> GetFamilyChildren(Guid familyId)
        {
            var results = new List<Child>();
            using var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false);
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"SELECT * FROM child_information WHERE FamilyId = @FamilyId AND IsActive = true";
            cmd.AddParameter("@FamilyId", familyId.ToByteArray());

            using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                results.Add(MapToChild(reader));
            }
            return results;
        }

        public async Task<bool> CheckIfChildExist(UserChildToVerify child)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false);
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"SELECT ChildId FROM child_information 
                                WHERE FamilyId = @FamilyId 
                                AND IsActive = true 
                                AND UPPER(RAMQNumber) = UPPER(@RAMQNumber)";

            cmd.AddParameter("@FamilyId", child.FamilyId.ToByteArray());
            cmd.AddParameter("@RAMQNumber", child.RAMQNumber);

            using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
            return reader.HasRows;
        }

        private Child MapToChild(DbDataReader reader)
        {
            return new Child
            {
                ChildId = reader.GetGuidFromByteArray("ChildId"),
                FamilyId = reader.GetGuidFromByteArray("FamilyId"),
                FirstName = reader.GetString("FirstName"),
                LastName = reader.GetString("LastName"),
                OtherHealthConditions = reader.GetString("OtherHealthConditions"),
                Allergies = reader.GetString("Allergies"),
                //AcedemicGroup = (AcedemicGroupType)reader.GetInt32("AcedemicGroupType"),
                AcedemicGroup = (AcedemicGroupType)(reader.GetNullableInt("AcedemicGroupType") ?? 0),
                DateOfBirth = reader.GetDateTime("DateOfBirth"),
                Gender = (Gender)reader.GetInt32("Gender"),
                RAMQExpiry = reader.GetDateTime("RAMQExpiry"),
                RAMQNumber = reader.GetString("RAMQNumber"),
                RAMQSequenceNumber = reader.GetInt32("RAMQSequenceNumber"),
                IsActive = reader.GetBoolean("IsActive"),
                CreatedAt = reader.GetDateTime("CreatedAt"),
                UpdatedOn = reader.GetDateTime("UpdatedOn"),
                RegistrationNumber = GetRegistrationNumber(reader),
                HasAllergy = reader.GetBooleanOrDefault("HasAllergy"),
            };
        }

        private static string GetRegistrationNumber(DbDataReader reader)
        {
            var ordinal = reader.GetOrdinal("RegistrationNumber");
            return reader.IsDBNull(ordinal) ? string.Empty : reader.GetString(ordinal);
        }

        private async Task<string> GetNextRegistrationNumber(DbConnection conn, DbTransaction tx)
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = @"
                SELECT RegistrationNumber
                FROM child_information
                WHERE RegistrationNumber IS NOT NULL
                  AND RegistrationNumber <> ''
                  AND RegistrationNumber REGEXP '^[0-9]+(\\.[0-9]+)?$'
                ORDER BY CAST(SUBSTRING_INDEX(RegistrationNumber, '.', 1) AS UNSIGNED) DESC
                LIMIT 1
                FOR UPDATE;";

            var result = await cmd.ExecuteScalarAsync().ConfigureAwait(false);
            if (result == null || result == DBNull.Value)
            {
                return "1";
            }

            var current = Convert.ToString(result);
            var integerPart = (current ?? string.Empty).Split('.')[0];
            return long.TryParse(integerPart, out var currentValue)
                ? (currentValue + 1L).ToString()
                : "1";
        }
    }
}
