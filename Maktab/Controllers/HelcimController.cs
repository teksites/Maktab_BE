using System.Threading.Tasks;
using Helcim.Services;
using Maktab.Attributes;
using MaktabDataContracts.Requests.Helcim;
using MaktabDataContracts.Responses.Helcim;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;

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
    public async Task<IActionResult> HelcimWebhook(HelcimCardTransactionWebhookResponse request)
    {
        await _service.HandleWebhook(request);
        return Ok();
    }
}
