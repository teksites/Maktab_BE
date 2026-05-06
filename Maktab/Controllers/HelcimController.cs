using System.Threading.Tasks;
using Helcim.Services;
using Maktab.Attributes;
using MaktabDataContracts.Requests.Helcim;
using MaktabDataContracts.Responses.Helcim;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Text.Json;

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

    [HttpPost("/api/helcim-webhook")]
    public async Task<IActionResult> HelcimWebhook([FromBody] JsonElement body)
    {
        var rawBody = body.GetRawText();
        if (string.IsNullOrWhiteSpace(rawBody))
        {
            return BadRequest(new { error = "Invalid payload" });
        }

        HelcimCardTransactionWebhookResponse? request;
        try
        {
            request = JsonConvert.DeserializeObject<HelcimCardTransactionWebhookResponse>(rawBody);
        }
        catch (Newtonsoft.Json.JsonException)
        {
            return BadRequest(new { error = "Invalid Helcim JSON format" });
        }

        if (request == null)
        {
            return BadRequest(new { error = "Invalid payload" });
        }

        var result = await _service.HandleWebhook(
            request,
            rawBody,
            Request.Headers["webhook-id"].ToString(),
            Request.Headers["webhook-timestamp"].ToString(),
            Request.Headers["webhook-signature"].ToString());

        return result switch
        {
            HelcimWebhookHandlingStatus.Processed => Ok(new { status = "processed" }),
            HelcimWebhookHandlingStatus.Duplicate => Ok(new { status = "duplicate" }),
            HelcimWebhookHandlingStatus.RetryLater => StatusCode(503, new { status = "retry_later" }),
            HelcimWebhookHandlingStatus.Ignored => Ok(new { status = "ignored", eventType = request.Type.ToString() }),
            HelcimWebhookHandlingStatus.InvalidTimestamp => Unauthorized(new { error = "Invalid webhook timestamp" }),
            _ => Unauthorized(new { error = "Invalid signature" })
        };
    }
}
