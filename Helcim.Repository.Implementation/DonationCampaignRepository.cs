using System.Data.Common;
using Cumulus.Data;
using Data;
using Helcim.Repository;
using MaktabDataContracts.Enums.Helcim;
using MaktabDataContracts.Requests.DonationCampaign;
using MaktabDataContracts.Responses.DonationCampaign;

namespace Helcim.Repository.Implementation;

public sealed class DonationCampaignRepository : DbRepository, IDonationCampaignRepository
{
    public DonationCampaignRepository(IDatabase database) : base(database) { }

    public async Task<IReadOnlyList<DonationCampaignTypeResponse>> GetActiveTypes()
    {
        using var connection = await Database.CreateAndOpenConnectionAsync();
        using var command = connection.CreateCommand();
        command.CommandText = @"SELECT CampaignTypeId, Code, Name, NameFr, NameAr
            FROM donation_campaign_type WHERE IsActive=1 ORDER BY Name";
        using var reader = await command.ExecuteReaderAsync();
        var result = new List<DonationCampaignTypeResponse>();
        while (await reader.ReadAsync()) result.Add(MapType(reader));
        return result;
    }

    public Task<IReadOnlyList<DonationCampaignResponse>> GetVisible(Guid? mosqueId, DateTime utcToday)
        => GetCampaigns(mosqueId, visibleOnly: true, utcToday);

    public Task<IReadOnlyList<DonationCampaignResponse>> GetAll(Guid? mosqueId = null)
        => GetCampaigns(mosqueId, visibleOnly: false, null);

    public async Task<DonationCampaignResponse?> Get(Guid campaignId)
    {
        var campaigns = await GetCampaigns(null, false, null, campaignId);
        return campaigns.SingleOrDefault();
    }

    public async Task<DonationCampaignResponse> Save(Guid campaignId, UpsertDonationCampaignRequest request)
    {
        using var connection = await Database.CreateAndOpenConnectionAsync();
        using var transaction = await connection.BeginTransactionAsync();
        try
        {
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = @"INSERT INTO donation_campaign
                    (CampaignId, MosqueId, CampaignTypeId, Name, NameFr, NameAr, ShortDescription, ShortDescriptionFr,
                     ShortDescriptionAr, StartDate, EndDate, AllowCustomAmount, Currency, IsActive)
                    VALUES (@CampaignId, @MosqueId, @CampaignTypeId, @Name, @NameFr, @NameAr, @ShortDescription,
                            @ShortDescriptionFr, @ShortDescriptionAr, @StartDate, @EndDate, @AllowCustomAmount, @Currency, @IsActive)
                    ON DUPLICATE KEY UPDATE MosqueId=VALUES(MosqueId), CampaignTypeId=VALUES(CampaignTypeId), Name=VALUES(Name),
                        NameFr=VALUES(NameFr), NameAr=VALUES(NameAr), ShortDescription=VALUES(ShortDescription),
                        ShortDescriptionFr=VALUES(ShortDescriptionFr), ShortDescriptionAr=VALUES(ShortDescriptionAr),
                        StartDate=VALUES(StartDate), EndDate=VALUES(EndDate), AllowCustomAmount=VALUES(AllowCustomAmount),
                        Currency=VALUES(Currency), IsActive=VALUES(IsActive), UpdatedOn=UTC_TIMESTAMP()";
                AddCampaignParameters(command, campaignId, request);
                await command.ExecuteNonQueryAsync();
            }

            // Presets are configuration, not payment records. Retire current values before inserting the replacement set.
            using (var deactivate = connection.CreateCommand())
            {
                deactivate.Transaction = transaction;
                deactivate.CommandText = "UPDATE donation_campaign_preset_amount SET IsActive=0, UpdatedOn=UTC_TIMESTAMP() WHERE CampaignId=@CampaignId AND IsActive=1";
                deactivate.AddParameter("@CampaignId", campaignId.ToByteArray());
                await deactivate.ExecuteNonQueryAsync();
            }

            for (var index = 0; index < request.PresetAmounts.Count; index++)
            {
                using var preset = connection.CreateCommand();
                preset.Transaction = transaction;
                preset.CommandText = @"INSERT INTO donation_campaign_preset_amount
                    (CampaignPresetAmountId, CampaignId, Amount, DisplayOrder, IsActive)
                    VALUES (@Id, @CampaignId, @Amount, @DisplayOrder, 1)";
                preset.AddParameter("@Id", Guid.NewGuid().ToByteArray());
                preset.AddParameter("@CampaignId", campaignId.ToByteArray());
                preset.AddParameter("@Amount", request.PresetAmounts[index]);
                preset.AddParameter("@DisplayOrder", index + 1);
                await preset.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

        return await Get(campaignId) ?? throw new InvalidOperationException("Campaign was not saved.");
    }

    public async Task<bool> Deactivate(Guid campaignId)
    {
        using var connection = await Database.CreateAndOpenConnectionAsync();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE donation_campaign SET IsActive=0, UpdatedOn=UTC_TIMESTAMP() WHERE CampaignId=@CampaignId AND IsActive=1";
        command.AddParameter("@CampaignId", campaignId.ToByteArray());
        return await command.ExecuteNonQueryAsync() > 0;
    }

    public async Task<IReadOnlyList<DonationPaymentResponse>> GetPayments(DonationPaymentQuery query)
    {
        using var connection = await Database.CreateAndOpenConnectionAsync();
        using var command = connection.CreateCommand();
        var filters = new List<string> { "dp.IsActive=1" };
        AddFilter(command, filters, "dp.CampaignId", "@CampaignId", query.CampaignId?.ToByteArray());
        AddFilter(command, filters, "dc.CampaignTypeId", "@CampaignTypeId", query.CampaignTypeId);
        AddFilter(command, filters, "dc.MosqueId", "@MosqueId", query.MosqueId?.ToByteArray());
        AddFilter(command, filters, "dp.UserId", "@UserId", query.UserId?.ToByteArray());
        AddFilter(command, filters, "dp.PaidAt >=", "@FromUtc", query.FromUtc);
        AddFilter(command, filters, "dp.PaidAt <", "@ToUtc", query.ToUtc);
        command.CommandText = $@"SELECT dp.DonationPaymentId, dp.CampaignId, dp.UserId, dp.Amount, dp.NetAmount, dp.Currency,
                dp.InvoiceStatus, dp.TransactionStatus, dp.TransactionType, dp.CardCompany, dp.CardFundingType, dp.LastFourDigits,
                dp.CardType, dp.PaidAt, dp.CreatedAt, dc.CampaignTypeId, dct.Code AS CampaignTypeCode, dc.Name AS CampaignName
            FROM donation_payment dp
            INNER JOIN donation_campaign dc ON dc.CampaignId=dp.CampaignId
            INNER JOIN donation_campaign_type dct ON dct.CampaignTypeId=dc.CampaignTypeId
            WHERE {string.Join(" AND ", filters)}
            ORDER BY COALESCE(dp.PaidAt, dp.CreatedAt) DESC";
        using var reader = await command.ExecuteReaderAsync();
        var result = new List<DonationPaymentResponse>();
        while (await reader.ReadAsync()) result.Add(MapPayment(reader));
        return result;
    }

    private async Task<IReadOnlyList<DonationCampaignResponse>> GetCampaigns(Guid? mosqueId, bool visibleOnly, DateTime? utcToday, Guid? campaignId = null)
    {
        using var connection = await Database.CreateAndOpenConnectionAsync();
        using var command = connection.CreateCommand();
        var filters = new List<string>();
        AddFilter(command, filters, "dc.MosqueId", "@MosqueId", mosqueId?.ToByteArray());
        AddFilter(command, filters, "dc.CampaignId", "@CampaignId", campaignId?.ToByteArray());
        if (visibleOnly)
        {
            filters.Add("dc.IsActive=1");
            filters.Add("dct.IsActive=1");
            filters.Add("(dc.StartDate IS NULL OR dc.StartDate <= @Today)");
            filters.Add("(dc.EndDate IS NULL OR dc.EndDate >= @Today)");
            command.AddParameter("@Today", utcToday!.Value.Date);
        }
        command.CommandText = $@"SELECT dc.CampaignId, dc.MosqueId, dc.CampaignTypeId, dc.Name, dc.NameFr, dc.NameAr,
                dc.ShortDescription, dc.ShortDescriptionFr, dc.ShortDescriptionAr, dc.StartDate, dc.EndDate,
                dc.AllowCustomAmount, dc.Currency, dc.IsActive, dc.CreatedAt, dc.UpdatedOn,
                dct.Code AS CampaignTypeCode, dct.Name AS CampaignTypeName, dct.NameFr AS CampaignTypeNameFr, dct.NameAr AS CampaignTypeNameAr
            FROM donation_campaign dc
            INNER JOIN donation_campaign_type dct ON dct.CampaignTypeId=dc.CampaignTypeId
            {(filters.Count == 0 ? string.Empty : "WHERE " + string.Join(" AND ", filters))}
            ORDER BY dc.IsActive DESC, dc.StartDate DESC, dc.Name";
        var result = new List<DonationCampaignResponse>();
        using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync()) result.Add(MapCampaign(reader));
        }

        foreach (var campaign in result)
            campaign.PresetAmounts = await GetActivePresets(connection, campaign.CampaignId);
        return result;
    }

    private static async Task<IReadOnlyList<DonationCampaignPresetAmountResponse>> GetActivePresets(System.Data.Common.DbConnection connection, Guid campaignId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = @"SELECT CampaignPresetAmountId, Amount, DisplayOrder FROM donation_campaign_preset_amount
            WHERE CampaignId=@CampaignId AND IsActive=1 ORDER BY DisplayOrder";
        command.AddParameter("@CampaignId", campaignId.ToByteArray());
        using var reader = await command.ExecuteReaderAsync();
        var result = new List<DonationCampaignPresetAmountResponse>();
        while (await reader.ReadAsync())
        {
            result.Add(new DonationCampaignPresetAmountResponse
            {
                CampaignPresetAmountId = ReadDbFieldGuid(reader, "CampaignPresetAmountId"),
                Amount = reader.GetDecimal(reader.GetOrdinal("Amount")),
                DisplayOrder = reader.GetByte(reader.GetOrdinal("DisplayOrder"))
            });
        }
        return result;
    }

    private static void AddCampaignParameters(DbCommand command, Guid campaignId, UpsertDonationCampaignRequest request)
    {
        command.AddParameter("@CampaignId", campaignId.ToByteArray());
        command.AddParameter("@MosqueId", request.MosqueId.ToByteArray());
        command.AddParameter("@CampaignTypeId", request.CampaignTypeId);
        command.AddParameter("@Name", request.Name.Trim());
        command.AddParameter("@NameFr", request.NameFr.Trim());
        command.AddParameter("@NameAr", request.NameAr.Trim());
        command.AddParameter("@ShortDescription", request.ShortDescription.Trim());
        command.AddParameter("@ShortDescriptionFr", request.ShortDescriptionFr.Trim());
        command.AddParameter("@ShortDescriptionAr", request.ShortDescriptionAr.Trim());
        command.AddParameter("@StartDate", request.StartDate?.Date);
        command.AddParameter("@EndDate", request.EndDate?.Date);
        command.AddParameter("@AllowCustomAmount", request.AllowCustomAmount);
        command.AddParameter("@Currency", request.Currency.Trim().ToUpperInvariant());
        command.AddParameter("@IsActive", request.IsActive);
    }

    private static void AddFilter(DbCommand command, ICollection<string> filters, string column, string parameter, object? value)
    {
        if (value == null) return;
        filters.Add($"{column} {parameter}");
        command.AddParameter(parameter, value);
    }

    private static DonationCampaignTypeResponse MapType(DbDataReader reader) => new()
    {
        CampaignTypeId = reader.GetByte(reader.GetOrdinal("CampaignTypeId")), Code = ReadDbFieldString(reader, "Code"),
        Name = ReadDbFieldString(reader, "Name"), NameFr = ReadDbFieldString(reader, "NameFr"), NameAr = ReadDbFieldString(reader, "NameAr")
    };

    private static DonationCampaignResponse MapCampaign(DbDataReader reader) => new()
    {
        CampaignId = ReadDbFieldGuid(reader, "CampaignId"), MosqueId = ReadDbFieldGuid(reader, "MosqueId"),
        CampaignTypeId = reader.GetByte(reader.GetOrdinal("CampaignTypeId")), CampaignTypeCode = ReadDbFieldString(reader, "CampaignTypeCode"),
        CampaignTypeName = ReadDbFieldString(reader, "CampaignTypeName"), CampaignTypeNameFr = ReadDbFieldString(reader, "CampaignTypeNameFr"),
        CampaignTypeNameAr = ReadDbFieldString(reader, "CampaignTypeNameAr"), Name = ReadDbFieldString(reader, "Name"),
        NameFr = ReadDbFieldString(reader, "NameFr"), NameAr = ReadDbFieldString(reader, "NameAr"),
        ShortDescription = ReadDbFieldString(reader, "ShortDescription"), ShortDescriptionFr = ReadDbFieldString(reader, "ShortDescriptionFr"),
        ShortDescriptionAr = ReadDbFieldString(reader, "ShortDescriptionAr"), StartDate = ReadNullableDate(reader, "StartDate"),
        EndDate = ReadNullableDate(reader, "EndDate"), AllowCustomAmount = ReadDbFieldBool(reader, "AllowCustomAmount"),
        Currency = ReadDbFieldString(reader, "Currency"), IsActive = ReadDbFieldBool(reader, "IsActive"),
        CreatedAt = ReadDbFieldDateTimeUtc(reader, "CreatedAt"), UpdatedOn = ReadDbFieldDateTimeUtc(reader, "UpdatedOn")
    };

    private static DonationPaymentResponse MapPayment(DbDataReader reader) => new()
    {
        DonationPaymentId = ReadDbFieldGuid(reader, "DonationPaymentId"), CampaignId = ReadDbFieldGuid(reader, "CampaignId"),
        UserId = ReadDbFieldNullableGuid(reader, "UserId"), CampaignTypeId = reader.GetByte(reader.GetOrdinal("CampaignTypeId")),
        CampaignTypeCode = ReadDbFieldString(reader, "CampaignTypeCode"), CampaignName = ReadDbFieldString(reader, "CampaignName"),
        Amount = reader.GetDecimal(reader.GetOrdinal("Amount")), NetAmount = reader.GetDecimal(reader.GetOrdinal("NetAmount")),
        Currency = (HelcimCurrency)reader.GetInt32(reader.GetOrdinal("Currency")),
        InvoiceStatus = (HelcimInvoiceStatus)reader.GetInt32(reader.GetOrdinal("InvoiceStatus")),
        TransactionStatus = (HelcimCardTransactionStatus)reader.GetInt32(reader.GetOrdinal("TransactionStatus")),
        TransactionType = (HelcimCardTransactionType)reader.GetInt32(reader.GetOrdinal("TransactionType")),
        CardCompany = ReadDbFieldString(reader, "CardCompany"), CardFundingType = ReadDbFieldString(reader, "CardFundingType"),
        LastFourDigits = ReadDbFieldString(reader, "LastFourDigits"), CardType = ReadDbFieldString(reader, "CardType"),
        PaidAt = ReadNullableDate(reader, "PaidAt"), CreatedAt = ReadDbFieldDateTimeUtc(reader, "CreatedAt")
    };

    private static DateTime? ReadNullableDate(DbDataReader reader, string field)
    {
        var ordinal = reader.GetOrdinal(field);
        return reader.IsDBNull(ordinal) ? null : DateTime.SpecifyKind(reader.GetDateTime(ordinal), DateTimeKind.Utc);
    }
}
