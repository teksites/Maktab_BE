using Application.Users.Contracts;
using Cumulus.Data;
using Data;
using MaktabDataContracts.Enums;
using MaktabDataContracts.Requests.Cards;
using Users.Repository;

namespace Application.Users.Repository.Implementation
{
    public class UserCardsRepository : DbRepository, IUserCardsRepository
    {
        public UserCardsRepository(IDatabase database) : base(database)
        {
        }

        public async Task<ClientCardInformation> AddClientCard(ClientCardInformation card)
        {
            using (var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false))
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"insert into client_card_information (`ClientCardId`, `UserId`, `CardHolderName`, `CardNumber`,  `CardProvider`, `CvcCode`, `ExpiryDate`, "
                        + "`IsActive`, `CreatedAt`, `UpdatedOn`,`IsDefault`,`SenderBankCardId`)"
                        + " Values(@cardId, @userId, @cardHolderName, @cardNumber, @cardProvider, @cvcCode, @expiryDate, @isActive, @createdAt, @updatedOn, @isDefault, @senderBankCardId )";

                    cmd.AddParameter("@cardId", card.CardId.ToByteArray());
                    cmd.AddParameter("@userId", card.UserId.ToByteArray());
                    cmd.AddParameter("@cardProvider", Convert.ToInt16(card.CardProvider));
                    cmd.AddParameter("@cardHolderName", card.CardHolderName);
                    cmd.AddParameter("@cardNumber", card.CardNumber);
                    cmd.AddParameter("@expiryDate", card.ExpiryDate);
                    cmd.AddParameter("@cvcCode", card.CvcCode);
                    cmd.AddParameter("@isActive", card.IsActive);
                    cmd.AddParameter("@createdAt", card.CreatedAt);
                    cmd.AddParameter("@updatedOn", card.UpdatedOn);
                    cmd.AddParameter("@isDefault", card.isDefault);
                    cmd.AddParameter("@senderBankCardId", card.SenderBankCardId);

                    if (await cmd.ExecuteNonQueryAsync().ConfigureAwait(false) > 0)
                    {
                        return card;   
                    }
                    else
                    {
                        return null;
                    }
                }
            }
        }

        public async Task<bool> CheckIfCardExisit(ClientCardVerification clientCard)
        {
            using (var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false))
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"Select ClientCardId from client_card_information where UPPER(CardNumber) like UPPER(@cardNumber) and IsActive = true";
                    cmd.AddParameter("@cardNumber", clientCard.CardNumber);
                    var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
                    return reader.HasRows;
                }
            }
        }

        public async Task<bool> DeleteClientCard(Guid clientCardId, bool ifHardDelete = false)
        {
            using (var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false))
            {
                using (var cmd = conn.CreateCommand())
                {
                    if (ifHardDelete)
                    {
                        cmd.CommandText = @"Delete from client_card_information where ClientCardId = @cardId";
                    }
                    else
                    {
                        cmd.CommandText = @"Update client_card_information SET IsActive = false where ClientCardId = @cardId";
                    }

                    cmd.AddParameter("@cardId", clientCardId.ToByteArray());
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }

        public async Task<bool> DeleteUserCards(Guid userId, bool ifHardDelete = false)
        {
            using (var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false))
            {
                using (var cmd = conn.CreateCommand())
                {
                    if (ifHardDelete)
                    {
                        cmd.CommandText = @"Delete from client_card_information where UserId = @userId";
                    }
                    else
                    {
                        cmd.CommandText = @"Update client_card_information SET IsActive = false where UserId = @userId";
                    }

                    cmd.AddParameter("@userId", userId.ToByteArray());
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }

        public async Task<ClientCardInformation> GetClientCard(Guid clientCardId)
        {
            using (var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false))
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"Select `ClientCardId`, `UserId`, `CardHolderName`, `CardNumber`, `CardProvider`, `CvcCode`, `ExpiryDate`, " +
                        " `IsActive`, `CreatedAt`, `UpdatedOn` ,`IsDefault`,`SenderBankCardId` from client_card_information " +
                        " where ClientCardId = @clientCardId";

                    cmd.AddParameter("@clientCardId", clientCardId.ToByteArray());
                    using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);

                    if (!await reader.ReadAsync().ConfigureAwait(false))
                    {
                        return null;
                    }

                    var id = reader.GetGuidFromByteArray(0);
                    var userId = reader.GetGuidFromByteArray(1);
                    var cardHolderName = reader.GetString(2);
                    var cardNumber = reader.GetString(3);
                    var cardProvider = reader.GetInt16(4);
                    var cvcCode = "";
                    
                    if (!await reader.IsDBNullAsync(5).ConfigureAwait(false))
                    {
                        cvcCode = reader.GetString(5);
                    }

                    var expiryDate = reader.GetDateTime(6);
                    var isActive = reader.GetBoolean(7);
                    var CreatedAt = reader.GetDateTime(8);
                    var UpdatedOn = reader.GetDateTime(9);
                    var isDefault = reader.GetBoolean(10);
                    var senderBankCardId = "";
                    
                    if (!await reader.IsDBNullAsync(11).ConfigureAwait(false))
                    {
                        senderBankCardId = reader.GetString(11);
                    }

                    return new ClientCardInformation
                    {
                        CardId = id,
                        UserId = userId,
                        CardHolderName = cardHolderName,
                        CardNumber = cardNumber,
                        CardProvider = (CardType)cardProvider,
                        CvcCode = cvcCode,
                        ExpiryDate = expiryDate,
                        IsActive = isActive,
                        CreatedAt = CreatedAt,
                        UpdatedOn = UpdatedOn,
                        isDefault = isDefault,
                        SenderBankCardId = senderBankCardId,
                    };
                }
            }
        }

        public async Task<ClientCardInformation> GetClientCardByNumber(string cardNumber)
        {
            using (var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false))
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"Select `ClientCardId`, `UserId`, `CardHolderName`, `CardNumber`, `CardProvider`, `CvcCode`, `ExpiryDate`, " +
                        " `IsActive`, `CreatedAt`, `UpdatedOn` ,`IsDefault`,`SenderBankCardId` from client_card_information " +
                        " where CardNumber like @cardNumber and IsActive = true";

                    cmd.AddParameter("@cardNumber", cardNumber);
                    using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);

                    if (!await reader.ReadAsync().ConfigureAwait(false))
                    {
                        return null;
                    }

                    var id = reader.GetGuidFromByteArray(0);
                    var userId = reader.GetGuidFromByteArray(1);
                    var cardHolderName = reader.GetString(2);
                    var cardNum = reader.GetString(3);
                    var cardProvider = reader.GetInt16(4);
                    var cvcCode = "";

                    if (!await reader.IsDBNullAsync(5).ConfigureAwait(false))
                    {
                        cvcCode = reader.GetString(5);
                    }

                    var expiryDate = reader.GetDateTime(6);
                    var isActive = reader.GetBoolean(7);
                    var CreatedAt = reader.GetDateTime(8);
                    var UpdatedOn = reader.GetDateTime(9);
                    var isDefault = reader.GetBoolean(10);
                    var senderBankCardId = "";

                    if (!await reader.IsDBNullAsync(11).ConfigureAwait(false))
                    {
                        senderBankCardId = reader.GetString(11);
                    }

                    return new ClientCardInformation
                    {
                        CardId = id,
                        UserId = userId,
                        CardHolderName = cardHolderName,
                        CardNumber = cardNum,
                        CardProvider = (CardType)cardProvider,
                        CvcCode = cvcCode,
                        ExpiryDate = expiryDate,
                        IsActive = isActive,
                        CreatedAt = CreatedAt,
                        UpdatedOn = UpdatedOn,
                        isDefault = isDefault,
                        SenderBankCardId = senderBankCardId,
                    };
                }
            }
        }

        public async Task<IEnumerable<ClientCardInformation>> GetUserClientCards(Guid userId)
        {
            var results = new List<ClientCardInformation>();

            using (var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false))
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"Select `ClientCardId`, `UserId`, `CardHolderName`, `CardNumber`, `CardProvider`, `CvcCode`, `ExpiryDate`, " +
                        " `IsActive`, `CreatedAt`, `UpdatedOn`,`IsDefault`,`SenderBankCardId` from client_card_information " +
                        " where UserId = @userId and IsActive = True";

                    cmd.AddParameter("@userId", userId.ToByteArray());
                    using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);

                    while (await reader.ReadAsync().ConfigureAwait(false))
                    {
                        var id = reader.GetGuidFromByteArray(0);
                        var usrId = reader.GetGuidFromByteArray(1);
                        var cardHolderName = reader.GetString(2);
                        var cardNumber = reader.GetString(3);
                        var cardProvider = reader.GetInt16(4);
                        var cvcCode = "";

                        if (!await reader.IsDBNullAsync(5).ConfigureAwait(false))
                        {
                            cvcCode = reader.GetString(5);
                        }

                        var expiryDate = reader.GetDateTime(6);
                        var isActive = reader.GetBoolean(7);
                        var CreatedAt = reader.GetDateTime(8);
                        var UpdatedOn = reader.GetDateTime(9);
                        var isDefault = reader.GetBoolean(10);
                        var senderBankCardId = "";

                        if (!await reader.IsDBNullAsync(11).ConfigureAwait(false))
                        {
                            senderBankCardId = reader.GetString(11);
                        }
                        results.Add(new ClientCardInformation
                        {
                            CardId = id,
                            UserId = usrId,
                            CardHolderName = cardHolderName,
                            CardNumber = cardNumber,
                            CardProvider = (CardType)cardProvider,
                            CvcCode = cvcCode,
                            ExpiryDate = expiryDate,
                            IsActive = isActive,
                            CreatedAt = CreatedAt,
                            UpdatedOn = UpdatedOn,
                            isDefault = isDefault,
                            SenderBankCardId = senderBankCardId
                        });
                    }
                }
            }

            return results;
        }

        public async Task<ClientCardInformation> SetSenderBankCardID(string cardNumber, Guid userId, string senderBankCardId)
        {
            using (var conn = await Database.CreateAndOpenConnectionAsync().ConfigureAwait(false))
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"Update client_card_information SET SenderBankCardId = @senderBankCardId, UpdatedOn = @updatedOn where UserId = @userId and " +
                        " UPPER(CardNumber) like UPPER(@cardNumber) and IsActive = true";

                    cmd.AddParameter("@userId", userId.ToByteArray());
                    cmd.AddParameter("@cardNumber", cardNumber);
                    cmd.AddParameter("@senderBankCardId", senderBankCardId);
                    cmd.AddParameter("@updatedOn", DateTime.Now);

                    if (await cmd.ExecuteNonQueryAsync().ConfigureAwait(false) > 0)
                    {
                        return await GetClientCardByNumber(cardNumber).ConfigureAwait(false);
                    }

                    return null;
                }
            }
        }

        public Task<ClientCardInformation> UpdateClientCard(UpdateClientCardInformation clientCardInformation)
        {
            throw new NotImplementedException("You cannot update a card. Please delete and add again.");
        }
    }
}
