using System.Data.Common;
using Cumulus.Data;
using Data;
using Helcim.Repository;

namespace Helcim.Repository.Implementation;

public sealed class HelcimCardVaultRepository : DbRepository, IHelcimCardVaultRepository
{
    public HelcimCardVaultRepository(IDatabase database) : base(database) { }

    public async Task<IReadOnlyList<HelcimSavedCardRecord>> GetActiveCards(Guid userId)
    {
        using var connection = await Database.CreateAndOpenConnectionAsync();
        using var command = connection.CreateCommand();
        command.CommandText = @"SELECT CardId, UserId, FamilyId, CardTokenCiphertext, CardTokenNonce, CardTokenTag,
            CardTokenHash, CardCompany, CardFundingType, LastFourDigits, CardHolderName, ExpiryMonth, ExpiryYear,
            IsDefault, IsActive, CreatedAt FROM helcim_saved_card
            WHERE UserId = @UserId AND IsActive = 1 ORDER BY IsDefault DESC, CreatedAt DESC";
        command.AddParameter("@UserId", userId.ToByteArray());
        return await ReadCards(command);
    }

    public async Task<HelcimSavedCardRecord?> GetActiveCard(Guid cardId, Guid userId)
    {
        using var connection = await Database.CreateAndOpenConnectionAsync();
        using var command = connection.CreateCommand();
        command.CommandText = @"SELECT CardId, UserId, FamilyId, CardTokenCiphertext, CardTokenNonce, CardTokenTag,
            CardTokenHash, CardCompany, CardFundingType, LastFourDigits, CardHolderName, ExpiryMonth, ExpiryYear,
            IsDefault, IsActive, CreatedAt FROM helcim_saved_card
            WHERE CardId = @CardId AND UserId = @UserId AND IsActive = 1";
        command.AddParameter("@CardId", cardId.ToByteArray());
        command.AddParameter("@UserId", userId.ToByteArray());
        return (await ReadCards(command)).SingleOrDefault();
    }

    public async Task AddCard(HelcimSavedCardRecord card)
    {
        using var connection = await Database.CreateAndOpenConnectionAsync();
        using var command = connection.CreateCommand();
        command.CommandText = @"INSERT INTO helcim_saved_card (CardId, UserId, FamilyId, CardTokenCiphertext, CardTokenNonce,
            CardTokenTag, CardTokenHash, CardCompany, CardFundingType, LastFourDigits, CardHolderName, ExpiryMonth, ExpiryYear, IsDefault)
            VALUES (@CardId, @UserId, @FamilyId, @Ciphertext, @Nonce, @Tag, @Hash, @Company, @FundingType, @LastFour, @Holder, @Month, @Year, @Default)";
        command.AddParameter("@CardId", card.CardId.ToByteArray());
        command.AddParameter("@UserId", card.UserId.ToByteArray());
        command.AddParameter("@FamilyId", card.FamilyId?.ToByteArray());
        command.AddParameter("@Ciphertext", card.TokenCiphertext);
        command.AddParameter("@Nonce", card.TokenNonce);
        command.AddParameter("@Tag", card.TokenTag);
        command.AddParameter("@Hash", card.TokenHash);
        command.AddParameter("@Company", card.CardCompany);
        command.AddParameter("@FundingType", card.CardFundingType);
        command.AddParameter("@LastFour", card.LastFourDigits);
        command.AddParameter("@Holder", card.CardHolderName);
        command.AddParameter("@Month", card.ExpiryMonth);
        command.AddParameter("@Year", card.ExpiryYear);
        command.AddParameter("@Default", card.IsDefault);
        await command.ExecuteNonQueryAsync();
    }

    public async Task SetDefault(Guid cardId, Guid userId)
    {
        using var connection = await Database.CreateAndOpenConnectionAsync();
        using var transaction = await connection.BeginTransactionAsync();
        using var clear = connection.CreateCommand();
        clear.Transaction = transaction;
        clear.CommandText = "UPDATE helcim_saved_card SET IsDefault = 0 WHERE UserId = @UserId AND IsActive = 1";
        clear.AddParameter("@UserId", userId.ToByteArray());
        await clear.ExecuteNonQueryAsync();
        using var set = connection.CreateCommand();
        set.Transaction = transaction;
        set.CommandText = "UPDATE helcim_saved_card SET IsDefault = 1 WHERE CardId = @CardId AND UserId = @UserId AND IsActive = 1";
        set.AddParameter("@CardId", cardId.ToByteArray());
        set.AddParameter("@UserId", userId.ToByteArray());
        if (await set.ExecuteNonQueryAsync() != 1) throw new KeyNotFoundException("Saved card was not found.");
        await transaction.CommitAsync();
    }

    public async Task Deactivate(Guid cardId, Guid userId)
    {
        using var connection = await Database.CreateAndOpenConnectionAsync();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE helcim_saved_card SET IsActive = 0, IsDefault = 0 WHERE CardId = @CardId AND UserId = @UserId AND IsActive = 1";
        command.AddParameter("@CardId", cardId.ToByteArray());
        command.AddParameter("@UserId", userId.ToByteArray());
        if (await command.ExecuteNonQueryAsync() != 1) throw new KeyNotFoundException("Saved card was not found.");
    }

    private static async Task<IReadOnlyList<HelcimSavedCardRecord>> ReadCards(DbCommand command)
    {
        var cards = new List<HelcimSavedCardRecord>();
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            cards.Add(new HelcimSavedCardRecord
            {
                CardId = ReadDbFieldGuid(reader, "CardId"), UserId = ReadDbFieldGuid(reader, "UserId"),
                FamilyId = ReadDbFieldNullableGuid(reader, "FamilyId"), TokenCiphertext = (byte[])reader["CardTokenCiphertext"],
                TokenNonce = (byte[])reader["CardTokenNonce"], TokenTag = (byte[])reader["CardTokenTag"],
                TokenHash = ReadDbFieldString(reader, "CardTokenHash"), CardCompany = ReadDbFieldString(reader, "CardCompany"),
                CardFundingType = ReadDbFieldString(reader, "CardFundingType", "Unknown"), LastFourDigits = ReadDbFieldString(reader, "LastFourDigits"),
                CardHolderName = ReadDbFieldString(reader, "CardHolderName"), ExpiryMonth = ReadDbFieldNullInt(reader, "ExpiryMonth"),
                ExpiryYear = ReadDbFieldNullInt(reader, "ExpiryYear"), IsDefault = ReadDbFieldBool(reader, "IsDefault"),
                IsActive = ReadDbFieldBool(reader, "IsActive"), CreatedAt = ReadDbFieldDateTimeUtc(reader, "CreatedAt")
            });
        }
        return cards;
    }
}
