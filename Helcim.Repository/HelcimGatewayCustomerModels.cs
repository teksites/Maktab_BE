namespace Helcim.Repository;

public static class PaymentGatewayTypes
{
    // Keep these stable because they are persisted in user_payment_gateway.
    public const byte Helcim = 1;
    public const byte Zeffy = 2;
    public const byte Stripe = 3;
    public const byte Other = 255;
}

public sealed class UserPaymentGatewayRecord
{
    public Guid UserPaymentGatewayId { get; init; }
    public Guid UserId { get; init; }
    public byte PaymentGatewayType { get; init; }
    public string UserPaymentGatewayCode { get; init; } = string.Empty;
    public string? ExternalCustomerId { get; init; }
    public bool IsActive { get; init; }
}

public sealed class HelcimGatewayCustomerProfile
{
    public Guid UserId { get; init; }
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
}

public interface IUserPaymentGatewayRepository
{
    Task<UserPaymentGatewayRecord?> GetActive(Guid userId, byte paymentGatewayType);
    Task<UserPaymentGatewayRecord?> GetActiveByCode(byte paymentGatewayType, string paymentGatewayCode);
    Task<HelcimGatewayCustomerProfile?> GetUserProfile(Guid userId);
    Task Save(UserPaymentGatewayRecord record);
}
