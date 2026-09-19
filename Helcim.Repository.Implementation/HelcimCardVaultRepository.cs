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
        command.CommandText = @"SELECT CardId, UserId, FamilyId, CardTokenCiphertext, CardTokenNonce, CardTokenTag, CardTokenHash,
            CardCompany, CardFundingType, LastFourDigits, CardHolderName, ExpiryMonth, ExpiryYear, SourceHelcimTransactionId, IsDefault, CreatedAt
            CardFingerprint, CardCompany, CardFundingType, LastFourDigits, CardHolderName, ExpiryMonth, ExpiryYear, SourceHelcimTransactionId, IsDefault, CreatedAt
            FROM helcim_saved_card WHERE UserId = @UserId AND IsActive = 1 ORDER BY IsDefault DESC, CreatedAt DESC";
        command.AddParameter("@UserId", userId.ToByteArray());
        var cards = new List<HelcimSavedCardRecord>();
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) cards.Add(Map(reader));
        return cards;
    }

    public async Task<HelcimSavedCardRecord?> GetActiveCard(Guid cardId, Guid userId)
        => (await GetActiveCards(userId)).SingleOrDefault(card => card.CardId == cardId);

    public async Task SaveOrReplace(HelcimSavedCardRecord card)
    {
        using var connection = await Database.CreateAndOpenConnectionAsync();
        using var command = connection.CreateCommand();
        command.CommandText = @"INSERT INTO helcim_saved_card (CardId, UserId, FamilyId, CardTokenCiphertext, CardTokenNonce,
            CardTokenTag, CardTokenHash, CardFingerprint, CardCompany, CardFundingType, LastFourDigits, CardHolderName, ExpiryMonth, ExpiryYear, SourceHelcimTransactionId, IsDefault)
            SELECT @CardId, @UserId, @FamilyId, @Ciphertext, @Nonce, @Tag, @Hash, @Fingerprint, @Company, @Funding, @LastFour, @Holder, @ExpiryMonth, @ExpiryYear, @SourceTransactionId,
            NOT EXISTS(SELECT 1 FROM helcim_saved_card WHERE UserId=@UserId AND IsActive=1)
            ON DUPLICATE KEY UPDATE FamilyId = VALUES(FamilyId), CardTokenCiphertext = VALUES(CardTokenCiphertext),
                CardTokenNonce = VALUES(CardTokenNonce), CardTokenTag = VALUES(CardTokenTag), CardTokenHash = VALUES(CardTokenHash),
                CardCompany = VALUES(CardCompany), CardFundingType = VALUES(CardFundingType), LastFourDigits = VALUES(LastFourDigits),
                CardHolderName = VALUES(CardHolderName), ExpiryMonth = VALUES(ExpiryMonth), ExpiryYear = VALUES(ExpiryYear),
                SourceHelcimTransactionId = VALUES(SourceHelcimTransactionId), IsActive = 1, UpdatedOn = UTC_TIMESTAMP()";
        command.AddParameter("@CardId", card.CardId.ToByteArray()); command.AddParameter("@UserId", card.UserId.ToByteArray());
        command.AddParameter("@FamilyId", card.FamilyId == Guid.Empty ? null : card.FamilyId.ToByteArray());
        command.AddParameter("@Ciphertext", card.TokenCiphertext); command.AddParameter("@Nonce", card.TokenNonce); command.AddParameter("@Tag", card.TokenTag);
        command.AddParameter("@Hash", card.TokenHash); command.AddParameter("@Company", card.CardCompany); command.AddParameter("@Funding", card.CardFundingType);
        command.AddParameter("@Fingerprint", card.CardFingerprint);
        command.AddParameter("@LastFour", card.LastFourDigits); command.AddParameter("@Holder", card.CardHolderName);
        command.AddParameter("@ExpiryMonth", card.ExpiryMonth); command.AddParameter("@ExpiryYear", card.ExpiryYear);
        command.AddParameter("@SourceTransactionId", card.SourceHelcimTransactionId);
        await command.ExecuteNonQueryAsync();
    }

    public async Task SetDefault(Guid cardId, Guid userId)
    {
        using var connection = await Database.CreateAndOpenConnectionAsync();
        using var transaction = await connection.BeginTransactionAsync();
        using var clear = connection.CreateCommand(); clear.Transaction = transaction;
        clear.CommandText = "UPDATE helcim_saved_card SET IsDefault = 0 WHERE UserId = @UserId AND IsActive = 1";
        clear.AddParameter("@UserId", userId.ToByteArray()); await clear.ExecuteNonQueryAsync();
        using var set = connection.CreateCommand(); set.Transaction = transaction;
        set.CommandText = "UPDATE helcim_saved_card SET IsDefault = 1 WHERE CardId = @CardId AND UserId = @UserId AND IsActive = 1";
        set.AddParameter("@CardId", cardId.ToByteArray()); set.AddParameter("@UserId", userId.ToByteArray());
        if (await set.ExecuteNonQueryAsync() != 1) throw new KeyNotFoundException("Saved card was not found.");
        await transaction.CommitAsync();
    }

    public async Task Deactivate(Guid cardId, Guid userId)
    {
        using var connection = await Database.CreateAndOpenConnectionAsync(); using var command = connection.CreateCommand();
        command.CommandText = "UPDATE helcim_saved_card SET IsActive = 0, IsDefault = 0 WHERE CardId = @CardId AND UserId = @UserId AND IsActive = 1";
        command.AddParameter("@CardId", cardId.ToByteArray()); command.AddParameter("@UserId", userId.ToByteArray());
        if (await command.ExecuteNonQueryAsync() != 1) throw new KeyNotFoundException("Saved card was not found.");
    }

    private static HelcimSavedCardRecord Map(System.Data.IDataReader reader) => new()
    {
        CardId = ReadDbFieldGuid(reader, "CardId"), UserId = ReadDbFieldGuid(reader, "UserId"),
        FamilyId = ReadDbFieldNullableGuid(reader, "FamilyId") ?? Guid.Empty,
        TokenCiphertext = (byte[])reader["CardTokenCiphertext"], TokenNonce = (byte[])reader["CardTokenNonce"],
        TokenTag = (byte[])reader["CardTokenTag"], TokenHash = ReadDbFieldString(reader, "CardTokenHash"),
        CardFingerprint = ReadDbFieldString(reader, "CardFingerprint"),
        CardCompany = ReadDbFieldString(reader, "CardCompany"), CardFundingType = ReadDbFieldString(reader, "CardFundingType", "Unknown"),
        LastFourDigits = ReadDbFieldString(reader, "LastFourDigits"), CardHolderName = ReadDbFieldString(reader, "CardHolderName"),
        ExpiryMonth = reader["ExpiryMonth"] is DBNull ? null : Convert.ToInt32(reader["ExpiryMonth"]),
        ExpiryYear = reader["ExpiryYear"] is DBNull ? null : Convert.ToInt32(reader["ExpiryYear"]),
        SourceHelcimTransactionId = reader["SourceHelcimTransactionId"] is DBNull ? null : Convert.ToInt32(reader["SourceHelcimTransactionId"]),
        IsDefault = ReadDbFieldBool(reader, "IsDefault"), CreatedAt = ReadDbFieldDateTimeUtc(reader, "CreatedAt")
    };
}
