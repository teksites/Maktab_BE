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
        command.CommandText = @"INSERT INTO helcim_checkout_context (InvoiceNumber, UserId, FamilyId, SaveCardInfo, ExpiresAt)
            VALUES (@Invoice, @User, @Family, @Save, DATE_ADD(UTC_TIMESTAMP(), INTERVAL 1 HOUR))
            ON DUPLICATE KEY UPDATE UserId=@User, FamilyId=@Family, SaveCardInfo=@Save, ExpiresAt=VALUES(ExpiresAt)";
        command.AddParameter("@Invoice", context.InvoiceNumber); command.AddParameter("@User", context.UserId.ToByteArray());
        command.AddParameter("@Family", context.FamilyId == Guid.Empty ? null : context.FamilyId.ToByteArray()); command.AddParameter("@Save", context.SaveCardInfo);
        await command.ExecuteNonQueryAsync();
    }
    public async Task<HelcimCheckoutContext?> Get(string invoiceNumber)
    {
        using var connection = await Database.CreateAndOpenConnectionAsync(); using var command = connection.CreateCommand();
        command.CommandText = "SELECT InvoiceNumber, UserId, FamilyId, SaveCardInfo FROM helcim_checkout_context WHERE InvoiceNumber=@Invoice AND ExpiresAt >= UTC_TIMESTAMP()";
        command.AddParameter("@Invoice", invoiceNumber); using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;
        return new HelcimCheckoutContext { InvoiceNumber = ReadDbFieldString(reader, "InvoiceNumber"), UserId = ReadDbFieldGuid(reader, "UserId"), FamilyId = ReadDbFieldNullableGuid(reader, "FamilyId") ?? Guid.Empty, SaveCardInfo = ReadDbFieldBool(reader, "SaveCardInfo") };
    }
}
