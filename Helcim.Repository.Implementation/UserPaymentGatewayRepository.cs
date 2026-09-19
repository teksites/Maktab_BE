using Cumulus.Data;
using Data;
using Helcim.Repository;

namespace Helcim.Repository.Implementation;

public sealed class UserPaymentGatewayRepository : DbRepository, IUserPaymentGatewayRepository
{
    public UserPaymentGatewayRepository(IDatabase database) : base(database) { }

    public async Task<UserPaymentGatewayRecord?> GetActive(Guid userId, byte paymentGatewayType)
    {
        using var connection = await Database.CreateAndOpenConnectionAsync();
        using var command = connection.CreateCommand();
        command.CommandText = @"SELECT UserPaymentGatewayId, UserId, PaymentGatewayType, UserPaymentGatewayCode, ExternalCustomerId, IsActive
            FROM user_payment_gateway
            WHERE UserId = @UserId AND PaymentGatewayType = @GatewayType AND IsActive = 1
            LIMIT 1";
        command.AddParameter("@UserId", userId.ToByteArray());
        command.AddParameter("@GatewayType", paymentGatewayType);
        using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;
        return new UserPaymentGatewayRecord
        {
            UserPaymentGatewayId = ReadDbFieldGuid(reader, "UserPaymentGatewayId"),
            UserId = ReadDbFieldGuid(reader, "UserId"),
            PaymentGatewayType = Convert.ToByte(reader["PaymentGatewayType"]),
            UserPaymentGatewayCode = ReadDbFieldString(reader, "UserPaymentGatewayCode"),
            ExternalCustomerId = reader["ExternalCustomerId"] is DBNull ? null : ReadDbFieldString(reader, "ExternalCustomerId"),
            IsActive = ReadDbFieldBool(reader, "IsActive")
        };
    }

    public async Task<HelcimGatewayCustomerProfile?> GetUserProfile(Guid userId)
    {
        using var connection = await Database.CreateAndOpenConnectionAsync();
        using var command = connection.CreateCommand();
        command.CommandText = @"SELECT UserId, FirstName, LastName, Phone, Email
            FROM user_info WHERE UserId = @UserId AND IsActive = 1";
        command.AddParameter("@UserId", userId.ToByteArray());
        using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;
        return new HelcimGatewayCustomerProfile
        {
            UserId = ReadDbFieldGuid(reader, "UserId"),
            FirstName = ReadDbFieldString(reader, "FirstName"),
            LastName = ReadDbFieldString(reader, "LastName"),
            Phone = ReadDbFieldString(reader, "Phone"),
            Email = ReadDbFieldString(reader, "Email")
        };
    }

    public async Task<UserPaymentGatewayRecord?> GetActiveByCode(byte paymentGatewayType, string paymentGatewayCode)
    {
        using var connection = await Database.CreateAndOpenConnectionAsync();
        using var command = connection.CreateCommand();
        command.CommandText = @"SELECT UserPaymentGatewayId, UserId, PaymentGatewayType, UserPaymentGatewayCode, ExternalCustomerId, IsActive
            FROM user_payment_gateway
            WHERE PaymentGatewayType = @GatewayType AND UserPaymentGatewayCode = @Code AND IsActive = 1
            LIMIT 1";
        command.AddParameter("@GatewayType", paymentGatewayType);
        command.AddParameter("@Code", paymentGatewayCode);
        using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;
        return new UserPaymentGatewayRecord
        {
            UserPaymentGatewayId = ReadDbFieldGuid(reader, "UserPaymentGatewayId"),
            UserId = ReadDbFieldGuid(reader, "UserId"),
            PaymentGatewayType = Convert.ToByte(reader["PaymentGatewayType"]),
            UserPaymentGatewayCode = ReadDbFieldString(reader, "UserPaymentGatewayCode"),
            ExternalCustomerId = reader["ExternalCustomerId"] is DBNull ? null : ReadDbFieldString(reader, "ExternalCustomerId"),
            IsActive = ReadDbFieldBool(reader, "IsActive")
        };
    }

    public async Task Save(UserPaymentGatewayRecord record)
    {
        using var connection = await Database.CreateAndOpenConnectionAsync();
        using var command = connection.CreateCommand();
        command.CommandText = @"INSERT INTO user_payment_gateway
            (UserPaymentGatewayId, UserId, PaymentGatewayType, UserPaymentGatewayCode, ExternalCustomerId, IsActive)
            VALUES (@Id, @UserId, @GatewayType, @Code, @ExternalCustomerId, 1)
            ON DUPLICATE KEY UPDATE UserPaymentGatewayCode = VALUES(UserPaymentGatewayCode),
                ExternalCustomerId = VALUES(ExternalCustomerId), IsActive = 1, UpdatedOn = UTC_TIMESTAMP()";
        command.AddParameter("@Id", record.UserPaymentGatewayId.ToByteArray());
        command.AddParameter("@UserId", record.UserId.ToByteArray());
        command.AddParameter("@GatewayType", record.PaymentGatewayType);
        command.AddParameter("@Code", record.UserPaymentGatewayCode);
        command.AddParameter("@ExternalCustomerId", record.ExternalCustomerId);
        await command.ExecuteNonQueryAsync();
    }
}
