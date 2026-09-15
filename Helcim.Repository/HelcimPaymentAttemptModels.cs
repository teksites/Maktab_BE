namespace Helcim.Repository;

public enum HelcimPaymentAttemptStatus : byte
{
    Created = 0,
    Submitted = 1,
    ApprovedAwaitingConfirmation = 2,
    Declined = 3,
    Confirmed = 4,
    Failed = 5
}

public sealed class HelcimPaymentAttemptRecord
{
    public Guid PaymentAttemptId { get; init; }
    public Guid UserId { get; init; }
    public Guid CardId { get; init; }
    public Guid MaktabTransactionId { get; init; }
    public string PaymentCode { get; init; } = string.Empty;
    public string InvoiceNumber { get; init; } = string.Empty;
    public string IdempotencyKey { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public HelcimPaymentAttemptStatus Status { get; init; }
    public int? HelcimTransactionId { get; init; }
    public string? FailureReason { get; init; }
}

public interface IHelcimPaymentAttemptRepository
{
    Task<HelcimPaymentAttemptRecord?> GetByIdempotencyKey(string idempotencyKey, Guid userId);
    Task Add(HelcimPaymentAttemptRecord attempt);
    Task UpdateResult(Guid paymentAttemptId, HelcimPaymentAttemptStatus status, int? helcimTransactionId, string? failureReason);
}
