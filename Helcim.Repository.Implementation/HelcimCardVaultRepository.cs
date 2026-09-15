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
        command.CommandText = @"SELECT CardId, UserId, CardTokenCiphertext, CardTokenNonce, CardTokenTag, CardTokenHash,
            CardCompany, CardFundingType, LastFourDigits, CardHolderName, IsDefault, CreatedAt
            FROM helcim_saved_card WHERE UserId = @UserId AND IsActive = 1 ORDER BY IsDefault DESC, CreatedAt DESC";
        command.AddParameter("@UserId", userId.ToByteArray());
        var cards = new List<HelcimSavedCardRecord>();
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) cards.Add(Map(reader));
        return cards;
    }

    public async Task<HelcimSavedCardRecord?> GetActiveCard(Guid cardId, Guid userId)
        => (await GetActiveCards(userId)).SingleOrDefault(card => card.CardId == cardId);

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
        TokenCiphertext = (byte[])reader["CardTokenCiphertext"], TokenNonce = (byte[])reader["CardTokenNonce"],
        TokenTag = (byte[])reader["CardTokenTag"], TokenHash = ReadDbFieldString(reader, "CardTokenHash"),
        CardCompany = ReadDbFieldString(reader, "CardCompany"), CardFundingType = ReadDbFieldString(reader, "CardFundingType", "Unknown"),
        LastFourDigits = ReadDbFieldString(reader, "LastFourDigits"), CardHolderName = ReadDbFieldString(reader, "CardHolderName"),
        IsDefault = ReadDbFieldBool(reader, "IsDefault"), CreatedAt = ReadDbFieldDateTimeUtc(reader, "CreatedAt")
    };
}
