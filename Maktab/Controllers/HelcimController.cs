using System;
using System.Threading.Tasks;
using Helcim.Services;
using Helcim;
using Maktab.Attributes;
using MaktabDataContracts.Requests.Helcim;
using MaktabDataContracts.Responses.Helcim;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Text.Json;
using MaktabDataContracts.Enums;
using Users.Services;

[Route("api/helcim")]
[ApiController]
[EnableCors("corspolicy")]
public class HelcimController : ControllerBase
{
    private readonly IHelcimTransactionService _service;
    private readonly IDataAccessVerificationService _access;

    public HelcimController(IHelcimTransactionService service, IDataAccessVerificationService access)
    {
        _service = service;
        _access = access;
    }

    [ApiAuthorize]
    [HttpPost("initialize-payment")]
    public async Task<HelcimPayInitializeResponse> InitializePayment(InitiatePaymentRequest request)
    {
        var session = await GetSessionContext();
        return await _service.InitializePaymentForSession(request, session.UserId, session.FamilyId);
    }

    [ApiAuthorize]
    [HttpPost("complete-payment")]
    public Task<HelcimPaymentCompletionResponse> CompletePayment(CompleteHelcimPayPaymentRequest request)
        => _service.CompleteHelcimPayPayment(request);

    [ApiAuthorize(false, false, UserRoleType.Admin | UserRoleType.SuperUser | UserRoleType.SchoolAdmin | UserRoleType.SchoolSupervisor)]
    [HttpPost("byadmin/sync-invoice/{invoiceReference}")]
    [HttpPost("byadmin/sync-invoice-number/{invoiceReference}")]
    public async Task<ActionResult<HelcimPaymentCompletionResponse>> SyncInvoicePayment(string invoiceReference)
    {
        try
        {
            return Ok(await _service.SyncInvoicePayment(invoiceReference).ConfigureAwait(false));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [ApiAuthorize(false, false, UserRoleType.Admin | UserRoleType.SuperUser | UserRoleType.SchoolAdmin | UserRoleType.SchoolSupervisor)]
    [HttpPost("byadmin/reconcile")]
    public Task<HelcimReconciliationResponse> ReconcileTransactions([FromBody] HelcimReconciliationRequest? request)
        => _service.ReconcileTransactions(request);

    [ApiAuthorize(false, false, UserRoleType.Admin | UserRoleType.SuperUser | UserRoleType.SchoolAdmin | UserRoleType.SchoolSupervisor)]
    [HttpPost("byadmin/reconcile/run")]
    public Task<HelcimReconciliationResponse> ReconcileTransactions()
        => _service.ReconcileTransactions();

    [ApiAuthorize(false, false, UserRoleType.Admin | UserRoleType.SuperUser | UserRoleType.SchoolAdmin | UserRoleType.SchoolSupervisor)]
    [HttpPost("byadmin/ach/refund/invoices/search")]
    public Task<IReadOnlyList<HelcimAchRefundInvoiceSummaryResponse>> GetAchRefundInvoices(GetAchRefundInvoicesRequest request)
        => _service.GetAchRefundInvoices(request);

    [ApiAuthorize(false, false, UserRoleType.Admin | UserRoleType.SuperUser | UserRoleType.SchoolAdmin | UserRoleType.SchoolSupervisor)]
    [HttpPost("byadmin/refund")]
    public async Task<ActionResult<HelcimTransactionAdjustmentResponse>> RefundTransaction(RefundTransactionRequest request)
    {
        try
        {
            return Ok(await _service.RefundTransaction(request).ConfigureAwait(false));
        }
        catch (HelcimRequestException ex)
        {
            return BuildRefundFailure(ex);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(BuildRefundFailure(ex));
        }
        catch (InvalidOperationException ex) when (
            ex.Message.Contains("Unable to find", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("Unable to resolve", StringComparison.OrdinalIgnoreCase))
        {
            return NotFound(BuildRefundFailure(ex));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(BuildRefundFailure(ex));
        }
    }

    [ApiAuthorize(false, false, UserRoleType.Admin | UserRoleType.SuperUser | UserRoleType.SchoolAdmin | UserRoleType.SchoolSupervisor)]
    [HttpPost("byadmin/ach/refund")]
    public async Task<ActionResult<HelcimAchRefundResponse>> RefundAchTransaction(RefundAchTransactionRequest request)
    {
        try
        {
            return Ok(await _service.RefundAchTransaction(request).ConfigureAwait(false));
        }
        catch (HelcimRequestException ex)
        {
            return BuildRefundFailure(ex);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(BuildRefundFailure(ex));
        }
        catch (InvalidOperationException ex) when (
            ex.Message.Contains("Unable to find", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("Unable to resolve", StringComparison.OrdinalIgnoreCase))
        {
            return NotFound(BuildRefundFailure(ex));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(BuildRefundFailure(ex));
        }
    }

    [ApiAuthorize(false, false, UserRoleType.Admin | UserRoleType.SuperUser | UserRoleType.SchoolAdmin | UserRoleType.SchoolSupervisor)]
    [HttpPost("byadmin/ach/refund/invoice")]
    public async Task<ActionResult<HelcimAchRefundResponse>> RefundAchInvoice(RefundAchInvoiceRequest request)
    {
        try
        {
            return Ok(await _service.RefundAchInvoice(request).ConfigureAwait(false));
        }
        catch (HelcimRequestException ex)
        {
            return BuildRefundFailure(ex);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(BuildRefundFailure(ex));
        }
        catch (InvalidOperationException ex) when (
            ex.Message.Contains("Unable to find", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("Unable to resolve", StringComparison.OrdinalIgnoreCase))
        {
            return NotFound(BuildRefundFailure(ex));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(BuildRefundFailure(ex));
        }
    }

    [ApiAuthorize(false, false, UserRoleType.Admin | UserRoleType.SuperUser | UserRoleType.SchoolAdmin | UserRoleType.SchoolSupervisor)]
    [HttpPost("byadmin/card/refund")]
    public async Task<ActionResult<HelcimCardRefundResponse>> RefundCardTransaction(RefundCardTransactionRequest request)
    {
        try
        {
            return Ok(await _service.RefundCardTransaction(request).ConfigureAwait(false));
        }
        catch (HelcimRequestException ex)
        {
            return BuildRefundFailure(ex);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(BuildRefundFailure(ex));
        }
        catch (InvalidOperationException ex) when (
            ex.Message.Contains("Unable to find", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("Unable to resolve", StringComparison.OrdinalIgnoreCase))
        {
            return NotFound(BuildRefundFailure(ex));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(BuildRefundFailure(ex));
        }
    }

    private static ObjectResult BuildRefundFailure(HelcimRequestException exception)
        => new(BuildRefundFailure((Exception)exception))
        {
            StatusCode = exception.IsUpstreamFailure ? 502 : 400
        };

    private static HelcimRefundFailureResponse BuildRefundFailure(Exception exception)
    {
        var helcimException = exception as HelcimRequestException;
        var isHelcimFailure = helcimException != null;
        var isUpstreamFailure = helcimException?.IsUpstreamFailure ?? false;

        return new HelcimRefundFailureResponse
        {
            Success = false,
            Error = exception.Message,
            ErrorSource = isHelcimFailure ? "Helcim" : "Maktab",
            IsUpstreamFailure = isUpstreamFailure,
            Retryable = isUpstreamFailure
        };
    }

    private async Task<SessionAccessContext> GetSessionContext()
    {
        if (!Request.Headers.TryGetValue("Session_Info", out var value)
            || !Guid.TryParse(value, out var sessionId)
            || sessionId == Guid.Empty)
        {
            throw new UnauthorizedAccessException("Session header not found or invalid.");
        }

        return await _access.GetSessionAccessContext(sessionId).ConfigureAwait(false)
            ?? throw new UnauthorizedAccessException("No active session found.");
    }

    [HttpPost("/api/payment-notifier")]
    public async Task<IActionResult> HelcimWebhook([FromBody] JsonElement body)
    {
        var rawBody = body.GetRawText();
        if (string.IsNullOrWhiteSpace(rawBody))
        {
            return BadRequest(new { error = "Invalid payload" });
        }

        JObject? payload;
        try
        {
            payload = JsonConvert.DeserializeObject<JObject>(rawBody);
        }
        catch (Newtonsoft.Json.JsonException)
        {
            return BadRequest(new { error = "Invalid Helcim JSON format" });
        }

        if (payload == null)
        {
            return BadRequest(new { error = "Invalid payload" });
        }

        var result = await _service.HandleWebhook(
            rawBody,
            Request.Headers["webhook-id"].ToString(),
            Request.Headers["webhook-timestamp"].ToString(),
            Request.Headers["webhook-signature"].ToString());

        return result switch
        {
            HelcimWebhookHandlingStatus.Processed => Ok(new { status = "processed" }),
            HelcimWebhookHandlingStatus.Duplicate => Ok(new { status = "duplicate" }),
            HelcimWebhookHandlingStatus.RetryLater => StatusCode(503, new { status = "retry_later" }),
            HelcimWebhookHandlingStatus.Ignored => Ok(new { status = "ignored", eventType = payload.Value<string>("type") ?? string.Empty }),
            HelcimWebhookHandlingStatus.InvalidTimestamp => Unauthorized(new { error = "Invalid webhook timestamp" }),
            _ => Unauthorized(new { error = "Invalid signature" })
        };
    }
}
