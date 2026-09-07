using Courses.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using Maktab.Attributes;
using Maktab.Contracts;
using MaktabDataContracts.Enums;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Stripe.Contracts;
using Stripe.Services;
using Stripe;
using Users.Services;

namespace Maktab.Controllers;

[Route("api/maktab/stripe")]
[ApiController]
[EnableCors("corspolicy")]
public sealed class MaktabStripeController : ControllerBase
{
    private readonly IStudentCourseTransactionService _transactions;
    private readonly IStripePaymentService _stripe;
    private readonly IDataAccessVerificationService _access;
    private readonly ICoursePaymentService _payments;

    public MaktabStripeController(IStudentCourseTransactionService transactions, IStripePaymentService stripe, IDataAccessVerificationService access, ICoursePaymentService payments)
    {
        _transactions = transactions;
        _stripe = stripe;
        _access = access;
        _payments = payments;
    }

    [ApiAuthorize]
    [HttpPost("payment-intents")]
    public async Task<ActionResult<StripePaymentIntentResponse>> CreatePaymentIntent(CreateStripeCoursePaymentIntentRequest request)
    {
        var transaction = await _transactions.GetTransaction(request.StudentCourseTransactionId).ConfigureAwait(false);
        if (transaction == null || !transaction.IsActive)
            return NotFound("An active course transaction was not found.");

        var session = await GetSessionContext().ConfigureAwait(false);
        if (!_access.HasElevatedAccess(session.UserRoles) && session.FamilyId != transaction.FamilyId)
            return Forbid();

        var outstanding = transaction.TotalPayable - transaction.TotalAmountPaid;
        var amount = request.Amount ?? outstanding;
        if (amount <= 0m || amount > outstanding)
            return BadRequest("Payment amount must be greater than zero and cannot exceed the outstanding balance.");

        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
            return BadRequest("IdempotencyKey is required. Reuse it only when retrying the same payment request.");

        var amountMinor = decimal.ToInt64(decimal.Round(amount * 100m, 0, MidpointRounding.AwayFromZero));
        var paymentMethodType = request.PaymentMethodType.Trim().ToLowerInvariant();
        if (paymentMethodType is not ("card" or "acss_debit"))
            return BadRequest("PaymentMethodType must be card or acss_debit.");
        try { var result = await _stripe.CreatePaymentIntentAsync(new StripePaymentIntentCreateRequest
        {
            AmountMinor = amountMinor,
            Currency = "cad",
            ReceiptEmail = request.ReceiptEmail,
            Description = $"Maktab payment {transaction.PaymentCode}",
            PaymentMethodTypes = new[] { paymentMethodType },
            IdempotencyKey = request.IdempotencyKey,
            ReferenceData = JsonSerializer.Serialize(new
            {
                application = "maktab",
                version = 1,
                studentCourseTransactionId = transaction.StudentCourseTransactionId,
                familyId = transaction.FamilyId,
                paymentCode = transaction.PaymentCode
            })
        }).ConfigureAwait(false);

        return Ok(result); } catch (StripeIntegrationException ex) { return BuildStripeFailure(ex); }
    }

    [ApiAuthorize]
    [HttpPost("parent/payment-intents")]
    public Task<ActionResult<StripePaymentIntentResponse>> CreateParentPaymentIntent(CreateStripeCoursePaymentIntentRequest request)
        => CreatePaymentIntent(request);

    [ApiAuthorize(false, false, UserRoleType.Admin | UserRoleType.SuperUser | UserRoleType.SchoolAdmin | UserRoleType.SchoolSupervisor)]
    [HttpPost("admin/payment-intents")]
    public Task<ActionResult<StripePaymentIntentResponse>> CreateAdminPaymentIntent(CreateStripeCoursePaymentIntentRequest request)
        => CreatePaymentIntent(request);

    [ApiAuthorize(false, false, UserRoleType.Admin | UserRoleType.SuperUser | UserRoleType.SchoolAdmin | UserRoleType.SchoolSupervisor)]
    [HttpPost("admin/refunds")]
    public async Task<ActionResult<StripeRefundResponse>> CreateRefund(CreateStripeRefundRequest request)
    {
        if (request.Amount <= 0m)
            return BadRequest("Refund amount must be greater than zero.");

        if (string.IsNullOrWhiteSpace(request.PaymentIntentId))
            return BadRequest("PaymentIntentId is required for a Maktab Stripe refund.");

        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
            return BadRequest("IdempotencyKey is required. Reuse it only when retrying the same refund request.");

        var transaction = await _transactions.GetTransaction(request.StudentCourseTransactionId).ConfigureAwait(false);
        if (transaction == null || !transaction.IsActive)
            return NotFound("An active course transaction was not found.");

        var existingPayments = await _payments.GetAllPaymentsByStudentTransactionId(transaction.StudentCourseTransactionId).ConfigureAwait(false);
        var originalPayment = existingPayments.FirstOrDefault(payment =>
            payment.IsActive
            && payment.PaymentMode == PaymentMode.Stripe
            && payment.PaymentType == PaymentType.Credit
            && string.Equals(payment.ExternalPaymentId, request.PaymentIntentId, StringComparison.Ordinal));
        if (originalPayment == null)
            return BadRequest("The Stripe PaymentIntentId is not an active Maktab payment for this course transaction.");

        var amountMinor = decimal.ToInt64(decimal.Round(request.Amount * 100m, 0, MidpointRounding.AwayFromZero));
        try { var result = await _stripe.CreateRefundAsync(new StripeRefundCreateRequest
        {
            PaymentIntentId = request.PaymentIntentId,
            AmountMinor = amountMinor,
            Reason = request.Reason,
            ReferenceData = JsonSerializer.Serialize(new
            {
                application = "maktab",
                version = 1,
                studentCourseTransactionId = transaction.StudentCourseTransactionId,
                familyId = transaction.FamilyId,
                originalPaymentIntentId = request.PaymentIntentId,
                originalCoursePaymentId = originalPayment.PaymentId
            }),
            IdempotencyKey = request.IdempotencyKey
        }).ConfigureAwait(false);

        return Ok(result); } catch (StripeIntegrationException ex) { return BuildStripeFailure(ex); }
    }

    private async Task<SessionAccessContext> GetSessionContext()
    {
        if (!Request.Headers.TryGetValue("Session_Info", out var value) || !Guid.TryParse(value, out var sessionId))
            throw new UnauthorizedAccessException("Session header not found or invalid.");

        return await _access.GetSessionAccessContext(sessionId).ConfigureAwait(false)
            ?? throw new UnauthorizedAccessException("No active session found.");
    }

    private ObjectResult BuildStripeFailure(StripeIntegrationException exception)
        => StatusCode(exception.IsUpstreamFailure ? 502 : 400, new StripeApiErrorResponse
        {
            Success = false,
            Error = new StripeApiErrorDetail { Code = exception.Code ?? "stripe_error", Message = exception.Message, Type = exception.ErrorType, DeclineCode = exception.DeclineCode, Retryable = exception.IsUpstreamFailure }
        });
}
