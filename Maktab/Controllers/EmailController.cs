using Email;
using Maktab.Attributes;
using MaktabDataContracts.Enums;
using MaktabDataContracts.Requests.Email;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Threading.Tasks;

[Route("api/email")]
[ApiController]
[ApiAuthorize(false, false, UserRoleType.Admin)]
[EnableCors("corspolicy")]
public class EmailController : ControllerBase
{
    private readonly ISendEmailService _service;

    public EmailController(ISendEmailService service)
    {
        _service = service;
    }

    [HttpPost("sendemail")]
    public async Task<bool> SendEmail([FromBody] SendEmailRequest request)
    {
        if (request == null || request.Recipients == null || !request.Recipients.Any(recipient => !string.IsNullOrWhiteSpace(recipient)))
        {
            return false;
        }

        try
        {
            return await _service.SendBulkEmail(new MultiUserEmailData
            {
                Subject = request.Subject,
                Body = request.Body,
                To = request.Recipients,
                Cc = request.RecipientsCC ?? Enumerable.Empty<string>(),
                Bcc = request.RecipientsBCC ?? Enumerable.Empty<string>()
            }).ConfigureAwait(false);
        }
        catch (Exception)
        {
            return false;
        }
    }
}
