using Cumulus.Data;
using Data;
using Helcim.Repository;

namespace Helcim.Repository.Implementation;

public sealed class HelcimCheckoutContextRepository : DbRepository, IHelcimCheckoutContextRepository
{
    public HelcimCheckoutContextRepository(IDatabase database) : base(database) { }
    public async Task Save(HelcimCheckoutContext context)
    {
        using var connection = await Database.CreateAndOpenConnectionAsync(); using var command = connection.CreateCommand();
        command.CommandText = @"INSERT INTO helcim_checkout_context (InvoiceNumber, CheckoutToken, UserId, FamilyId, SaveCardInfo, ExpiresAt, IsActive)
            VALUES (@Invoice, @CheckoutToken, @User, @Family, @Save, DATE_ADD(UTC_TIMESTAMP(), INTERVAL 1 HOUR), 1)
            ON DUPLICATE KEY UPDATE CheckoutToken=@CheckoutToken, UserId=@User, FamilyId=@Family, SaveCardInfo=@Save, ExpiresAt=VALUES(ExpiresAt), IsActive=1";
        command.AddParameter("@Invoice", context.InvoiceNumber); command.AddParameter("@User", context.UserId.ToByteArray());
        command.AddParameter("@CheckoutToken", string.IsNullOrWhiteSpace(context.CheckoutToken) ? null : context.CheckoutToken);
        command.AddParameter("@Family", context.FamilyId == Guid.Empty ? null : context.FamilyId.ToByteArray()); command.AddParameter("@Save", context.SaveCardInfo);
        await command.ExecuteNonQueryAsync();
    }
    public async Task<HelcimCheckoutContext?> Get(string invoiceNumber)
    {
        using var connection = await Database.CreateAndOpenConnectionAsync(); using var command = connection.CreateCommand();
        command.CommandText = "SELECT InvoiceNumber, CheckoutToken, UserId, FamilyId, SaveCardInfo FROM helcim_checkout_context WHERE InvoiceNumber=@Invoice AND IsActive=1 AND ExpiresAt >= UTC_TIMESTAMP()";
        command.AddParameter("@Invoice", invoiceNumber); using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;
        return Map(reader);
    }

    public async Task<HelcimCheckoutContext?> GetByCheckoutToken(string checkoutToken)
    {
        using var connection = await Database.CreateAndOpenConnectionAsync(); using var command = connection.CreateCommand();
        command.CommandText = "SELECT InvoiceNumber, CheckoutToken, UserId, FamilyId, SaveCardInfo FROM helcim_checkout_context WHERE CheckoutToken=@CheckoutToken AND IsActive=1 AND ExpiresAt >= UTC_TIMESTAMP()";
        command.AddParameter("@CheckoutToken", checkoutToken); using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? Map(reader) : null;
    }

    public async Task Deactivate(string invoiceNumber)
    {
        using var connection = await Database.CreateAndOpenConnectionAsync();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE helcim_checkout_context SET IsActive=0, UpdatedOn=UTC_TIMESTAMP() WHERE InvoiceNumber=@Invoice AND IsActive=1";
        command.AddParameter("@Invoice", invoiceNumber);
        await command.ExecuteNonQueryAsync();
    }

    private static HelcimCheckoutContext Map(System.Data.IDataReader reader) => new()
    {
        InvoiceNumber = ReadDbFieldString(reader, "InvoiceNumber"),
        CheckoutToken = reader["CheckoutToken"] is DBNull ? null : ReadDbFieldString(reader, "CheckoutToken"),
        UserId = ReadDbFieldGuid(reader, "UserId"),
        FamilyId = ReadDbFieldNullableGuid(reader, "FamilyId") ?? Guid.Empty,
        SaveCardInfo = ReadDbFieldBool(reader, "SaveCardInfo")
    };
}
