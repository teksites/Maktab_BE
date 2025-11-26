// Controllers/WebhookController.cs
using Microsoft.AspNetCore.Mvc;
using SquareTransactionApi.Models;
using SquareTransactionApi.Models.Response;
using SquareTransactionApi.Services;

namespace Maktab.Controllers
{
    [ApiController]
    [Route("api/webhook")]
    public class WebhookController : ControllerBase
    {
        private readonly ITransactionStore _transactionStore;

        public WebhookController(ITransactionStore transactionStore)
        {
            _transactionStore = transactionStore;
        }

        [HttpPost("checkout")]
        public IActionResult ReceiveCheckoutResponse([FromBody] CheckoutResponse response)
        {
            if (response == null)
            {
                return BadRequest("Invalid payload.");
            }

            _transactionStore.SaveCheckoutResponse(response);

            return Ok(new { message = "Checkout response received." });
        }

        // Optional: For testing/debugging
        [HttpGet("responses")]
        public IActionResult GetAllResponses()
        {
            var responses = _transactionStore.GetAllResponses();
            return Ok(responses);
        }
    }
}
