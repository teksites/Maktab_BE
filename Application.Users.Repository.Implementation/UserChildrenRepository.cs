using Application.Users.Contracts;
using Cumulus.Data;
using Data;
using MaktabDataContracts.Enums;
using MaktabDataContracts.Helpers;
using MaktabDataContracts.Requests.Children;
using MaktabDataContracts.Responses.Children;
using System.Data.Common;
using System.Text.Json;
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
                (ChildId, FamilyId, FirstName, LastName, ArabicName, OtherHealthConditions, HasAllergy, Allergies, AcedemicGroupType, DateOfBirth, Gender, RAMQExpiry, RAMQNumber, RAMQSequenceNumber, IsActive, CreatedAt, UpdatedOn, RegistrationNumber, Consent, UserType)
                VALUES 
                (@ChildId, @FamilyId, @FirstName, @LastName, @ArabicName, @OtherHealthConditions, @HasAllergy, @Allergies, @AcedemicGroupType, @DateOfBirth, @Gender, @RAMQExpiry, @RAMQNumber, @RAMQSequenceNumber, @IsActive, @CreatedAt, @UpdatedOn, @RegistrationNumber, @Consent, @UserType)";
            cmd.Transaction = tx;

            child.RegistrationNumber = await GetNextRegistrationNumber(conn, tx).ConfigureAwait(false);

            cmd.AddParameter("@ChildId", child.ChildId.ToByteArray());
            cmd.AddParameter("@FamilyId", child.FamilyId.ToByteArray());
            cmd.AddParameter("@FirstName", child.FirstName);
            cmd.AddParameter("@LastName", child.LastName);
            cmd.AddParameter("@ArabicName", GetArabicName(child.ArabicName));
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
            cmd.AddParameter("@Consent", (object?)child.Consent ?? DBNull.Value);
            cmd.AddParameter("@UserType", (int)child.UserType);

            var rows = await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            if (rows > 0)
            {
                await tx.CommitAsync().ConfigureAwait(false);
                return child;
            }

            await tx.RollbackAsync().ConfigureAwait(false);
            return null;
        }

        public async Task<bool> UpsertLinkedUserChild(Guid childId, Guid familyId, string firstName, string lastName, Gender gender, UserType userType, bool isActive)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false);
            using var tx = await conn.BeginTransactionAsync().ConfigureAwait(false);
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;

            var now = DateTime.UtcNow;
            var placeholderDate = new DateTime(1900, 1, 1);
            var registrationNumber = await GetNextRegistrationNumber(conn, tx).ConfigureAwait(false);

            cmd.CommandText = @"
                INSERT INTO child_information
                (
                    ChildId,
                    FamilyId,
                    FirstName,
                    LastName,
                    ArabicName,
                    OtherHealthConditions,
                    HasAllergy,
                    Allergies,
                    AcedemicGroupType,
                    DateOfBirth,
                    Gender,
                    RAMQExpiry,
                    RAMQNumber,
                    RAMQSequenceNumber,
                    IsActive,
                    CreatedAt,
                    UpdatedOn,
                    RegistrationNumber,
                    Consent,
                    UserType
                )
                VALUES
                (
                    @ChildId,
                    @FamilyId,
                    @FirstName,
                    @LastName,
                    @ArabicName,
                    @OtherHealthConditions,
                    @HasAllergy,
                    @Allergies,
                    @AcedemicGroupType,
                    @DateOfBirth,
                    @Gender,
                    @RAMQExpiry,
                    @RAMQNumber,
                    @RAMQSequenceNumber,
                    @IsActive,
                    @CreatedAt,
                    @UpdatedOn,
                    @RegistrationNumber,
                    @Consent,
                    @UserType
                )
                ON DUPLICATE KEY UPDATE
                    FamilyId = VALUES(FamilyId),
                    FirstName = VALUES(FirstName),
                    LastName = VALUES(LastName),
                    AcedemicGroupType = VALUES(AcedemicGroupType),
                    DateOfBirth = VALUES(DateOfBirth),
                    Gender = VALUES(Gender),
                    RAMQExpiry = VALUES(RAMQExpiry),
                    IsActive = VALUES(IsActive),
                    UpdatedOn = VALUES(UpdatedOn),
                    Consent = VALUES(Consent),
                    UserType = VALUES(UserType),
                    RegistrationNumber = IFNULL(RegistrationNumber, VALUES(RegistrationNumber))";

            cmd.AddParameter("@ChildId", childId.ToByteArray());
            cmd.AddParameter("@FamilyId", familyId.ToByteArray());
            cmd.AddParameter("@FirstName", firstName);
            cmd.AddParameter("@LastName", lastName);
            cmd.AddParameter("@ArabicName", string.Empty);
            cmd.AddParameter("@OtherHealthConditions", string.Empty);
            cmd.AddParameter("@HasAllergy", false);
            cmd.AddParameter("@Allergies", string.Empty);
            cmd.AddParameter("@AcedemicGroupType", (int)AcedemicGroupType.Adults);
            cmd.AddParameter("@DateOfBirth", placeholderDate);
            cmd.AddParameter("@Gender", (int)gender);
            cmd.AddParameter("@RAMQExpiry", placeholderDate);
            cmd.AddParameter("@RAMQNumber", string.Empty);
            cmd.AddParameter("@RAMQSequenceNumber", 0);
            cmd.AddParameter("@IsActive", isActive);
            cmd.AddParameter("@CreatedAt", now);
            cmd.AddParameter("@UpdatedOn", now);
            cmd.AddParameter("@RegistrationNumber", registrationNumber);
            cmd.AddParameter("@Consent", string.Empty);
            cmd.AddParameter("@UserType", (int)userType);

            var rows = await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            if (rows > 0)
            {
                await tx.CommitAsync().ConfigureAwait(false);
                return true;
            }

            await tx.RollbackAsync().ConfigureAwait(false);
            return false;
        }

        public async Task<Child> UpdateChild(Child child)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false);
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"UPDATE child_information SET
                FirstName = @FirstName,
                LastName = @LastName,
                OtherHealthConditions = @OtherHealthConditions,
                Allergies = @Allergies,
                AcedemicGroupType = @AcedemicGroupType,
                ArabicName = @ArabicName,
                DateOfBirth = @DateOfBirth,
                Gender = @Gender,
                RAMQExpiry = @RAMQExpiry,
                RAMQNumber = @RAMQNumber,
                RAMQSequenceNumber = @RAMQSequenceNumber,
                HasAllergy = @HasAllergy,
                Consent = @Consent,
                UserType = @UserType,
                UpdatedOn = @UpdatedOn
                WHERE ChildId = @ChildId";

            cmd.AddParameter("@ChildId", child.ChildId.ToByteArray());
            cmd.AddParameter("@FirstName", child.FirstName);
            cmd.AddParameter("@LastName", child.LastName);
            cmd.AddParameter("@OtherHealthConditions", child.OtherHealthConditions);
            cmd.AddParameter("@Allergies", child.Allergies);
            cmd.AddParameter("@AcedemicGroupType", (int)child.AcedemicGroup);
            cmd.AddParameter("@ArabicName", GetArabicName(child.ArabicName));
            cmd.AddParameter("@DateOfBirth", child.DateOfBirth);
            cmd.AddParameter("@Gender", (int)child.Gender);
            cmd.AddParameter("@RAMQExpiry", child.RAMQExpiry);
            cmd.AddParameter("@RAMQNumber", child.RAMQNumber);
            cmd.AddParameter("@RAMQSequenceNumber", child.RAMQSequenceNumber);
            cmd.AddParameter("@HasAllergy", child.HasAllergy);
            cmd.AddParameter("@Consent", (object?)child.Consent ?? DBNull.Value);
            cmd.AddParameter("@UserType", (int)child.UserType);
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

        public async Task<ChildEducationalProfileResponse?> GetChildEducationalProfile(Guid childId)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false);
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                SELECT ChildEducationalProfileId, ChildId, FamilyId, CompletedSurahsJson, IsActive, CreatedAt, UpdatedOn
                FROM child_educational_profile
                WHERE ChildId = @ChildId
                LIMIT 1";

            cmd.AddParameter("@ChildId", childId.ToByteArray());

            using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
            if (!await reader.ReadAsync().ConfigureAwait(false))
            {
                return null;
            }

            return MapToChildEducationalProfile(reader);
        }

        public async Task<ChildEducationalProfileResponse> UpsertChildEducationalProfile(Guid childId, Guid familyId, IReadOnlyCollection<QuranSurah> completedSurahs)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false);
            using var tx = await conn.BeginTransactionAsync().ConfigureAwait(false);
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;

            var now = DateTime.UtcNow;
            var profileId = Guid.NewGuid();

            cmd.CommandText = @"
                INSERT INTO child_educational_profile
                (ChildEducationalProfileId, ChildId, FamilyId, CompletedSurahsJson, IsActive, CreatedAt, UpdatedOn)
                VALUES
                (@ChildEducationalProfileId, @ChildId, @FamilyId, @CompletedSurahsJson, @IsActive, @CreatedAt, @UpdatedOn)
                ON DUPLICATE KEY UPDATE
                    FamilyId = VALUES(FamilyId),
                    CompletedSurahsJson = VALUES(CompletedSurahsJson),
                    IsActive = VALUES(IsActive),
                    UpdatedOn = VALUES(UpdatedOn)";

            cmd.AddParameter("@ChildEducationalProfileId", profileId.ToByteArray());
            cmd.AddParameter("@ChildId", childId.ToByteArray());
            cmd.AddParameter("@FamilyId", familyId.ToByteArray());
            cmd.AddParameter("@CompletedSurahsJson", SerializeCompletedSurahs(completedSurahs));
            cmd.AddParameter("@IsActive", true);
            cmd.AddParameter("@CreatedAt", now);
            cmd.AddParameter("@UpdatedOn", now);

            await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);

            cmd.Parameters.Clear();
            cmd.CommandText = @"
                UPDATE child_information
                SET HasSurahCatalogBeenProvided = @HasSurahCatalogBeenProvided,
                    UpdatedOn = @UpdatedOn
                WHERE ChildId = @ChildId";

            cmd.AddParameter("@HasSurahCatalogBeenProvided", true);
            cmd.AddParameter("@UpdatedOn", now);
            cmd.AddParameter("@ChildId", childId.ToByteArray());

            await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            await tx.CommitAsync().ConfigureAwait(false);

            return await GetChildEducationalProfile(childId).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Failed to load child educational profile after upsert.");
        }

        private Child MapToChild(DbDataReader reader)
        {
            return new Child
            {
                ChildId = reader.GetGuidFromByteArray("ChildId"),
                FamilyId = reader.GetGuidFromByteArray("FamilyId"),
                FirstName = reader.GetString("FirstName"),
                LastName = reader.GetString("LastName"),
                ArabicName = GetArabicName(reader),
                HasSurahCatalogBeenProvided = GetHasSurahCatalogBeenProvided(reader),
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
                Consent = GetConsent(reader),
                UserType = (UserType)(reader.GetNullableInt("UserType") ?? 0),
            };
        }

        private static ChildEducationalProfileResponse MapToChildEducationalProfile(DbDataReader reader)
        {
            return new ChildEducationalProfileResponse
            {
                ChildEducationalProfileId = reader.GetGuidFromByteArray("ChildEducationalProfileId"),
                ChildId = reader.GetGuidFromByteArray("ChildId"),
                FamilyId = reader.GetGuidFromByteArray("FamilyId"),
                CompletedSurahs = DeserializeCompletedSurahs(GetCompletedSurahsJson(reader)),
                IsActive = reader.GetBoolean("IsActive"),
                CreatedAt = reader.GetDateTime("CreatedAt"),
                UpdatedOn = reader.GetDateTime("UpdatedOn")
            };
        }

        private static string GetRegistrationNumber(DbDataReader reader)
        {
            var ordinal = reader.GetOrdinal("RegistrationNumber");
            return reader.IsDBNull(ordinal) ? string.Empty : reader.GetString(ordinal);
        }

        private static string GetConsent(DbDataReader reader)
        {
            var ordinal = reader.GetOrdinal("Consent");
            return reader.IsDBNull(ordinal) ? string.Empty : reader.GetString(ordinal);
        }

        private static string GetArabicName(string? arabicName)
        {
            return string.IsNullOrWhiteSpace(arabicName) ? string.Empty : arabicName.Trim();
        }

        private static string GetArabicName(DbDataReader reader)
        {
            var ordinal = reader.GetOrdinal("ArabicName");
            return reader.IsDBNull(ordinal) ? string.Empty : reader.GetString(ordinal);
        }

        private static string GetCompletedSurahsJson(DbDataReader reader)
        {
            var ordinal = reader.GetOrdinal("CompletedSurahsJson");
            return reader.IsDBNull(ordinal) ? "[]" : reader.GetString(ordinal);
        }

        private static bool GetHasSurahCatalogBeenProvided(DbDataReader reader)
        {
            var ordinal = reader.GetOrdinal("HasSurahCatalogBeenProvided");
            return !reader.IsDBNull(ordinal) && reader.GetBoolean(ordinal);
        }

        private static string SerializeCompletedSurahs(IEnumerable<QuranSurah> completedSurahs)
        {
            var normalizedSurahs = (completedSurahs ?? Enumerable.Empty<QuranSurah>())
                .Where(surah => Enum.IsDefined(typeof(QuranSurah), surah))
                .Distinct()
                .OrderBy(surah => (int)surah)
                .Select(surah => surah.ToString())
                .ToList();

            return JsonSerializer.Serialize(normalizedSurahs);
        }

        private static List<QuranSurahOptionResponse> DeserializeCompletedSurahs(string completedSurahsJson)
        {
            if (string.IsNullOrWhiteSpace(completedSurahsJson))
            {
                return new List<QuranSurahOptionResponse>();
            }

            try
            {
                var names = JsonSerializer.Deserialize<List<string>>(completedSurahsJson) ?? new List<string>();
                return names
                    .Where(name => Enum.TryParse<QuranSurah>(name, out _))
                    .Select(name => Enum.Parse<QuranSurah>(name))
                    .Distinct()
                    .OrderBy(surah => (int)surah)
                    .Select(QuranSurahCatalog.GetOption)
                    .ToList();
            }
            catch (JsonException)
            {
                return new List<QuranSurahOptionResponse>();
            }
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
