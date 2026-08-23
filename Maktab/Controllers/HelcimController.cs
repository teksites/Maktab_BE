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

[Route("api/helcim")]
[ApiController]
[EnableCors("corspolicy")]
public class HelcimController : ControllerBase
{
    private readonly IHelcimTransactionService _service;

    public HelcimController(IHelcimTransactionService service)
    {
        _service = service;
    }

    [ApiAuthorize]
    [HttpPost("initialize-payment")]
    public Task<HelcimPayInitializeResponse> InitializePayment(InitiatePaymentRequest request)
        => _service.InitializePayment(request);

    [ApiAuthorize]
    [HttpPost("complete-payment")]
    public Task<HelcimPaymentCompletionResponse> CompletePayment(CompleteHelcimPayPaymentRequest request)
        => _service.CompleteHelcimPayPayment(request);

    [ApiAuthorize(false, false, UserRoleType.Admin | UserRoleType.SuperUser | UserRoleType.SchoolAdmin | UserRoleType.SchoolSupervisor)]
    [HttpPost("byadmin/sync-invoice/{invoiceReference}")]
    [HttpPost("byadmin/sync-invoice-number/{invoiceReference}")]
    public Task<HelcimPaymentCompletionResponse> SyncInvoicePayment(string invoiceReference)
        => int.TryParse(invoiceReference, out var invoiceId)
            ? _service.SyncInvoicePaymentByInvoiceId(invoiceId)
            : _service.SyncInvoicePaymentByInvoiceNumber(invoiceReference);

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
    [HttpPost("byadmin/ach/refund")]
    public Task<HelcimAchRefundResponse> RefundAchTransaction(RefundAchTransactionRequest request)
        => _service.RefundAchTransaction(request);

    [ApiAuthorize(false, false, UserRoleType.Admin | UserRoleType.SuperUser | UserRoleType.SchoolAdmin | UserRoleType.SchoolSupervisor)]
    [HttpPost("byadmin/ach/refund/invoice")]
    public Task<HelcimAchRefundResponse> RefundAchInvoice(RefundAchInvoiceRequest request)
        => _service.RefundAchInvoice(request);

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
