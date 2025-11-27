using System.Data.Common;
using Cumulus.Data;
using Data;
using MaktabDataContracts.Requests.Zeffy;
using MaktabDataContracts.Responses.Zeffy;
using Newtonsoft.Json;

namespace Zeffy.Repository.Implementation
{
    public class ZeffyTransactionRepository : DbRepository, IZeffyTransactionRepository
    {
        public ZeffyTransactionRepository(IDatabase database) : base(database) { }

        public async Task Add(AddZeffyRequest zeffy)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            var now = DateTime.UtcNow;
            var zeffyId = zeffy.ZeffyId != Guid.Empty ? zeffy.ZeffyId : Guid.NewGuid();

            cmd.CommandText = @"
                INSERT INTO zeffy_transaction 
                (
                    ZeffyId,
                    FamilyId,
                    StudentCourseTransactionId,
                    PaymentCode,
                    Id,
                    Address,
                    Amount,
                    Birthdate,
                    CampaignId,
                    City,
                    Company,
                    Country,
                    CreationDate,
                    CustomFields,
                    DonationFormId,
                    DonationId,
                    Email,
                    Firstname,
                    Form_Name,
                    InHonourName,
                    Language,
                    Lastname,
                    OrganizationId,
                    PaymentMethod,
                    Pdf,
                    Postal,
                    Province,
                    ReceiptNumber,
                    Recurrent,
                    TeamId,
                    IsActive,
                    CreatedAt,
                    UpdatedOn
                )
                VALUES
                (
                    @ZeffyId,
                    @FamilyId,
                    @StudentCourseTransactionId,
                    @PaymentCode,
                    @Id,
                    @Address,
                    @Amount,
                    @Birthdate,
                    @CampaignId,
                    @City,
                    @Company,
                    @Country,
                    @CreationDate,
                    @CustomFields,
                    @DonationFormId,
                    @DonationId,
                    @Email,
                    @Firstname,
                    @Form_Name,
                    @InHonourName,
                    @Language,
                    @Lastname,
                    @OrganizationId,
                    @PaymentMethod,
                    @Pdf,
                    @Postal,
                    @Province,
                    @ReceiptNumber,
                    @Recurrent,
                    @TeamId,
                    @IsActive,
                    @CreatedAt,
                    @UpdatedOn
                );";

            cmd.AddParameter("@ZeffyId", zeffyId.ToByteArray());
            cmd.AddParameter("@FamilyId", zeffy.FamilyId.ToByteArray());
            cmd.AddParameter("@StudentCourseTransactionId", zeffy.StudentCourseTransactionId.ToByteArray());
            cmd.AddParameter("@PaymentCode", zeffy.PaymentCode);

            cmd.AddParameter("@Id", zeffy.Id);
            cmd.AddParameter("@Address", (object?)zeffy.Address ?? DBNull.Value);
            cmd.AddParameter("@Amount", zeffy.Amount);
            cmd.AddParameter("@Birthdate", (object?)zeffy.Birthdate ?? DBNull.Value);
            cmd.AddParameter("@CampaignId", (object?)zeffy.CampaignId ?? DBNull.Value);
            cmd.AddParameter("@City", (object?)zeffy.City ?? DBNull.Value);
            cmd.AddParameter("@Company", (object?)zeffy.Company ?? DBNull.Value);
            cmd.AddParameter("@Country", (object?)zeffy.Country ?? DBNull.Value);
            cmd.AddParameter("@CreationDate", zeffy.CreationDate);

            var customFieldsJson = (zeffy.CustomFields != null && zeffy.CustomFields.Any())
                ? JsonConvert.SerializeObject(zeffy.CustomFields)
                : null;
            cmd.AddParameter("@CustomFields", (object?)customFieldsJson ?? DBNull.Value);

            cmd.AddParameter("@DonationFormId", (object?)zeffy.DonationFormId ?? DBNull.Value);
            cmd.AddParameter("@DonationId", (object?)zeffy.DonationId ?? DBNull.Value);
            cmd.AddParameter("@Email", (object?)zeffy.Email ?? DBNull.Value);
            cmd.AddParameter("@Firstname", (object?)zeffy.Firstname ?? DBNull.Value);
            cmd.AddParameter("@Form_Name", (object?)zeffy.Form_Name ?? DBNull.Value);
            cmd.AddParameter("@InHonourName", (object?)zeffy.InHonourName ?? DBNull.Value);
            cmd.AddParameter("@Language", (object?)zeffy.Language ?? DBNull.Value);
            cmd.AddParameter("@Lastname", (object?)zeffy.Lastname ?? DBNull.Value);
            cmd.AddParameter("@OrganizationId", (object?)zeffy.OrganizationId ?? DBNull.Value);
            cmd.AddParameter("@PaymentMethod", (object?)zeffy.PaymentMethod ?? DBNull.Value);
            cmd.AddParameter("@Pdf", (object?)zeffy.Pdf ?? DBNull.Value);
            cmd.AddParameter("@Postal", (object?)zeffy.Postal ?? DBNull.Value);
            cmd.AddParameter("@Province", (object?)zeffy.Province ?? DBNull.Value);
            cmd.AddParameter("@ReceiptNumber", (object?)zeffy.ReceiptNumber ?? DBNull.Value);
            cmd.AddParameter("@Recurrent", (object?)zeffy.Recurrent ?? DBNull.Value);
            cmd.AddParameter("@TeamId", (object?)zeffy.TeamId ?? DBNull.Value);

            cmd.AddParameter("@IsActive", zeffy.IsActive);
            cmd.AddParameter("@CreatedAt", now);
            cmd.AddParameter("@UpdatedOn", now);

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<bool> Update(ZeffyResponse zeffy)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                UPDATE zeffy_transaction
                SET
                    FamilyId                   = @FamilyId,
                    StudentCourseTransactionId = @StudentCourseTransactionId,
                    PaymentCode                = @PaymentCode,
                    Id                         = @Id,
                    Address                    = @Address,
                    Amount                     = @Amount,
                    Birthdate                  = @Birthdate,
                    CampaignId                 = @CampaignId,
                    City                       = @City,
                    Company                    = @Company,
                    Country                    = @Country,
                    CreationDate               = @CreationDate,
                    CustomFields               = @CustomFields,
                    DonationFormId             = @DonationFormId,
                    DonationId                 = @DonationId,
                    Email                      = @Email,
                    Firstname                  = @Firstname,
                    Form_Name                  = @Form_Name,
                    InHonourName               = @InHonourName,
                    Language                   = @Language,
                    Lastname                   = @Lastname,
                    OrganizationId             = @OrganizationId,
                    PaymentMethod              = @PaymentMethod,
                    Pdf                        = @Pdf,
                    Postal                     = @Postal,
                    Province                   = @Province,
                    ReceiptNumber              = @ReceiptNumber,
                    Recurrent                  = @Recurrent,
                    TeamId                     = @TeamId,
                    IsActive                   = @IsActive,
                    UpdatedOn                  = @UpdatedOn
                WHERE ZeffyId = @ZeffyId";

            cmd.AddParameter("@ZeffyId", zeffy.ZeffyId.ToByteArray());
            cmd.AddParameter("@FamilyId", zeffy.FamilyId.ToByteArray());
            cmd.AddParameter("@StudentCourseTransactionId", zeffy.StudentCourseTransactionId.ToByteArray());
            cmd.AddParameter("@PaymentCode", zeffy.PaymentCode);

            cmd.AddParameter("@Id", zeffy.Id);
            cmd.AddParameter("@Address", (object?)zeffy.Address ?? DBNull.Value);
            cmd.AddParameter("@Amount", zeffy.Amount);
            cmd.AddParameter("@Birthdate", (object?)zeffy.Birthdate ?? DBNull.Value);
            cmd.AddParameter("@CampaignId", (object?)zeffy.CampaignId ?? DBNull.Value);
            cmd.AddParameter("@City", (object?)zeffy.City ?? DBNull.Value);
            cmd.AddParameter("@Company", (object?)zeffy.Company ?? DBNull.Value);
            cmd.AddParameter("@Country", (object?)zeffy.Country ?? DBNull.Value);
            cmd.AddParameter("@CreationDate", zeffy.CreationDate);

            var customFieldsJson = (zeffy.CustomFields != null && zeffy.CustomFields.Any())
                ? JsonConvert.SerializeObject(zeffy.CustomFields)
                : null;
            cmd.AddParameter("@CustomFields", (object?)customFieldsJson ?? DBNull.Value);

            cmd.AddParameter("@DonationFormId", (object?)zeffy.DonationFormId ?? DBNull.Value);
            cmd.AddParameter("@DonationId", (object?)zeffy.DonationId ?? DBNull.Value);
            cmd.AddParameter("@Email", (object?)zeffy.Email ?? DBNull.Value);
            cmd.AddParameter("@Firstname", (object?)zeffy.Firstname ?? DBNull.Value);
            cmd.AddParameter("@Form_Name", (object?)zeffy.Form_Name ?? DBNull.Value);
            cmd.AddParameter("@InHonourName", (object?)zeffy.InHonourName ?? DBNull.Value);
            cmd.AddParameter("@Language", (object?)zeffy.Language ?? DBNull.Value);
            cmd.AddParameter("@Lastname", (object?)zeffy.Lastname ?? DBNull.Value);
            cmd.AddParameter("@OrganizationId", (object?)zeffy.OrganizationId ?? DBNull.Value);
            cmd.AddParameter("@PaymentMethod", (object?)zeffy.PaymentMethod ?? DBNull.Value);
            cmd.AddParameter("@Pdf", (object?)zeffy.Pdf ?? DBNull.Value);
            cmd.AddParameter("@Postal", (object?)zeffy.Postal ?? DBNull.Value);
            cmd.AddParameter("@Province", (object?)zeffy.Province ?? DBNull.Value);
            cmd.AddParameter("@ReceiptNumber", (object?)zeffy.ReceiptNumber ?? DBNull.Value);
            cmd.AddParameter("@Recurrent", (object?)zeffy.Recurrent ?? DBNull.Value);
            cmd.AddParameter("@TeamId", (object?)zeffy.TeamId ?? DBNull.Value);

            cmd.AddParameter("@IsActive", zeffy.IsActive);
            cmd.AddParameter("@UpdatedOn", DateTime.UtcNow);

            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        public async Task<bool> Delete(Guid zeffyId, bool hardDelete = false)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            if (hardDelete)
            {
                cmd.CommandText = @"DELETE FROM zeffy_transaction WHERE ZeffyId = @ZeffyId";
            }
            else
            {
                cmd.CommandText = @"UPDATE zeffy_transaction SET IsActive = FALSE, UpdatedOn = @UpdatedOn WHERE ZeffyId = @ZeffyId";
                cmd.AddParameter("@UpdatedOn", DateTime.UtcNow);
            }

            cmd.AddParameter("@ZeffyId", zeffyId.ToByteArray());

            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        public async Task<ZeffyResponse?> GetByZeffyId(Guid zeffyId)
        {
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"SELECT * FROM zeffy_transaction WHERE ZeffyId = @ZeffyId";
            cmd.AddParameter("@ZeffyId", zeffyId.ToByteArray());

            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return null;

            return MapToZeffyResponse(reader);
        }

        public async Task<List<ZeffyResponse>> GetAllZeffyDonations()
        {
            var results = new List<ZeffyResponse>();
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"SELECT * FROM zeffy_transaction";

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(MapToZeffyResponse(reader));
            }
            return results;
        }

        public async Task<IEnumerable<ZeffyResponse>> GetByStudentCourseTransactionId(Guid studentCourseTransactionId)
        {
            var results = new List<ZeffyResponse>();
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"SELECT * FROM zeffy_transaction WHERE StudentCourseTransactionId = @StudentCourseTransactionId";
            cmd.AddParameter("@StudentCourseTransactionId", studentCourseTransactionId.ToByteArray());

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(MapToZeffyResponse(reader));
            }
            return results;
        }

        public async Task<IEnumerable<ZeffyResponse>> GetByFamilyId(Guid familyId)
        {
            var results = new List<ZeffyResponse>();
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"SELECT * FROM zeffy_transaction WHERE FamilyId = @FamilyId";
            cmd.AddParameter("@FamilyId", familyId.ToByteArray());

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(MapToZeffyResponse(reader));
            }
            return results;
        }

        public async Task<IEnumerable<ZeffyResponse>> GetByPaymentCode(string paymentCode)
        {
            var results = new List<ZeffyResponse>();
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"SELECT * FROM zeffy_transaction WHERE PaymentCode = @PaymentCode";
            cmd.AddParameter("@PaymentCode", paymentCode);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(MapToZeffyResponse(reader));
            }
            return results;
        }

        public async Task<IEnumerable<ZeffyResponse>> GetByFamilyAndPaymentCode(Guid familyId, string paymentCode)
        {
            var results = new List<ZeffyResponse>();
            using var conn = await Database.CreateAndOpenConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                SELECT * 
                FROM zeffy_transaction 
                WHERE FamilyId = @FamilyId AND PaymentCode = @PaymentCode";

            cmd.AddParameter("@FamilyId", familyId.ToByteArray());
            cmd.AddParameter("@PaymentCode", paymentCode);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(MapToZeffyResponse(reader));
            }
            return results;
        }

        private ZeffyResponse MapToZeffyResponse(DbDataReader reader)
        {
            var customFieldsJson = reader.GetNullableString("CustomFields");
            var customFields = string.IsNullOrWhiteSpace(customFieldsJson)
                ? new List<CustomField>()
                : JsonConvert.DeserializeObject<List<CustomField>>(customFieldsJson) ?? new List<CustomField>();

            return new ZeffyResponse
            {
                ZeffyId = reader.GetGuidFromByteArray("ZeffyId"),
                FamilyId = reader.GetGuidFromByteArray("FamilyId"),
                StudentCourseTransactionId = reader.GetGuidFromByteArray("StudentCourseTransactionId"),
                PaymentCode = reader.GetString("PaymentCode"),

                Id = reader.GetString("Id"),
                Address = reader.GetNullableString("Address"),
                Amount = reader.GetString("Amount"),
                Birthdate = reader.GetNullableString("Birthdate"),
                CampaignId = reader.GetNullableString("CampaignId"),
                City = reader.GetNullableString("City"),
                Company = reader.GetNullableString("Company"),
                Country = reader.GetNullableString("Country"),
                CreationDate = reader.GetDateTime("CreationDate"),

                CustomFields = customFields,

                DonationFormId = reader.GetNullableString("DonationFormId"),
                DonationId = reader.GetNullableString("DonationId"),
                Email = reader.GetNullableString("Email"),
                Firstname = reader.GetNullableString("Firstname"),
                Form_Name = reader.GetNullableString("Form_Name"),
                InHonourName = reader.GetNullableString("InHonourName"),
                Language = reader.GetNullableString("Language"),
                Lastname = reader.GetNullableString("Lastname"),
                OrganizationId = reader.GetNullableString("OrganizationId"),
                PaymentMethod = reader.GetNullableString("PaymentMethod"),
                Pdf = reader.GetNullableString("Pdf"),
                Postal = reader.GetNullableString("Postal"),
                Province = reader.GetNullableString("Province"),
                ReceiptNumber = reader.GetNullableString("ReceiptNumber"),
                Recurrent = reader.GetNullableString("Recurrent"),
                TeamId = reader.GetNullableString("TeamId"),

                IsActive = reader.GetBoolean("IsActive"),
                CreatedAt = reader.GetDateTime("CreatedAt"),
                UpdatedOn = reader.GetDateTime("UpdatedOn")
            };
        }
    }
}
