using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using Courses.Services;
using MaktabDataContracts.Enums;
using MaktabDataContracts.Requests.Course;
using Stripe.Contracts;
using Stripe.Services;

namespace Maktab.Services;

// Converts only verified, application-owned Stripe events into Maktab ledger mutations.
public sealed class MaktabStripeWebhookEventHandler : IStripeWebhookEventHandler
{
    private readonly IStudentCourseTransactionService _transactions;
    private readonly ICoursePaymentService _payments;

    public MaktabStripeWebhookEventHandler(IStudentCourseTransactionService transactions, ICoursePaymentService payments)
    {
        _transactions = transactions;
        _payments = payments;
    }

    public async Task HandleAsync(StripeWebhookHandlingResult stripeEvent, CancellationToken cancellationToken = default)
    {
        using var document = JsonDocument.Parse(stripeEvent.RawPayload);
        var providerObject = document.RootElement.GetProperty("data").GetProperty("object");
        var referenceData = GetMetadataValue(providerObject, "reference_data");
        var reference = ParseMaktabReference(referenceData);
        if (reference == null) return;

        var transaction = await _transactions.GetTransaction(reference.StudentCourseTransactionId).ConfigureAwait(false);
        if (transaction == null || !transaction.IsActive || transaction.FamilyId != reference.FamilyId)
            throw new InvalidOperationException("Stripe reference data does not resolve to an active Maktab transaction.");

        if (string.Equals(stripeEvent.EventType, "payment_intent.succeeded", StringComparison.Ordinal))
        {
            var paymentIntentId = providerObject.GetProperty("id").GetString();
            var amountReceived = providerObject.TryGetProperty("amount_received", out var received)
                ? received.GetInt64() : providerObject.GetProperty("amount").GetInt64();
            if (string.IsNullOrWhiteSpace(paymentIntentId) || amountReceived <= 0) return;

            var existingPayments = await _payments
                .GetAllPaymentsByStudentTransactionId(transaction.StudentCourseTransactionId)
                .ConfigureAwait(false);
            if (existingPayments.Any(payment => payment.IsActive
                && payment.PaymentMode == PaymentMode.Stripe
                && payment.PaymentType == PaymentType.Credit
                && string.Equals(payment.ExternalPaymentId, paymentIntentId, StringComparison.Ordinal)))
                return;

            var outstandingMinor = decimal.ToInt64(decimal.Round(
                (transaction.TotalPayable - transaction.TotalAmountPaid) * 100m,
                0,
                MidpointRounding.AwayFromZero));
            if (amountReceived > outstandingMinor)
                throw new InvalidOperationException("Stripe payment exceeds the remaining Maktab transaction balance.");

            await AddLedgerPayment(transaction, amountReceived, paymentIntentId, PaymentType.Credit, "Stripe payment applied for paymentIntentId").ConfigureAwait(false);
        }
        else if ((string.Equals(stripeEvent.EventType, "refund.created", StringComparison.Ordinal)
                  || string.Equals(stripeEvent.EventType, "refund.updated", StringComparison.Ordinal))
                 && providerObject.TryGetProperty("status", out var status)
                 && string.Equals(status.GetString(), "succeeded", StringComparison.OrdinalIgnoreCase))
        {
            var refundId = providerObject.GetProperty("id").GetString();
            var amount = providerObject.GetProperty("amount").GetInt64();
            if (string.IsNullOrWhiteSpace(refundId) || amount <= 0) return;
            await AddLedgerPayment(transaction, amount, refundId, PaymentType.Refund, "Stripe refund applied for refundId").ConfigureAwait(false);
        }
    }

    private static string? GetMetadataValue(JsonElement paymentIntent, string key)
        => paymentIntent.TryGetProperty("metadata", out var metadata)
           && metadata.ValueKind == JsonValueKind.Object
           && metadata.TryGetProperty(key, out var value)
            ? value.GetString()
            : null;

    private static MaktabStripeReference? ParseMaktabReference(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        try
        {
            var reference = JsonSerializer.Deserialize<MaktabStripeReference>(value, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            return reference is { Application: "maktab", Version: 1 }
                   && reference.StudentCourseTransactionId != Guid.Empty
                   && reference.FamilyId != Guid.Empty
                ? reference
                : null;
        }
        catch (JsonException) { return null; }
    }

    private async Task AddLedgerPayment(MaktabDataContracts.Responses.Transactions.StudentCourseTransactionResponse transaction, long amountMinor, string externalId, PaymentType paymentType, string marker)
    {
        await _payments.TryAddPayment(new AddCoursePayment
        {
            StudentCourseTransactionId = transaction.StudentCourseTransactionId,
            FamilyId = transaction.FamilyId,
            AmountPaid = amountMinor / 100m,
            ExternalPaymentId = externalId,
            PaymentMode = PaymentMode.Stripe,
            PaymentType = paymentType,
            IsActive = true,
            Comments = $"{marker}: {externalId}"
        }).ConfigureAwait(false);
    }

    private sealed class MaktabStripeReference
    {
        public string Application { get; set; } = string.Empty;
        public int Version { get; set; }
        public Guid StudentCourseTransactionId { get; set; }
        public Guid FamilyId { get; set; }
    }
}
