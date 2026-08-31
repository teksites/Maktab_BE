using AppConfigurations.Services;
using Email;
using Maktab.Attributes;
using MaktabDataContracts.Enums;
using RequestEmailAttachmentData = MaktabDataContracts.Requests.Email.EmailAttachmentData;
using MaktabDataContracts.Requests.Email;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

[Route("api/email")]
[ApiController]
[ApiAuthorize()]
[EnableCors("corspolicy")]
public class EmailController : ControllerBase
{
    private readonly ISendEmailService _service;
    private readonly IAppConfigService _appConfigService;

    public EmailController(ISendEmailService service, IAppConfigService appConfigService)
    {
        _service = service;
        _appConfigService = appConfigService;
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
            var attachments = await GetValidatedAttachmentsAsync(request).ConfigureAwait(false);
            if (attachments == null)
            {
                return false;
            }

            return await _service.SendBulkEmail(new MultiUserEmailData
            {
                Subject = request.Subject,
                Body = request.Body,
                To = request.Recipients,
                Cc = request.RecipientsCC ?? Enumerable.Empty<string>(),
                Bcc = request.RecipientsBCC ?? Enumerable.Empty<string>(),
                Attachments = attachments,
                IncludeSystemFooter = false
            }).ConfigureAwait(false);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private async Task<IReadOnlyList<EmailAttachmentPayload>?> GetValidatedAttachmentsAsync(SendEmailRequest request)
    {
        var attachments = MapAttachments(request.Attachments);
        if (attachments.Count == 0)
        {
            return attachments;
        }

        var config = await _appConfigService
            .GetLatestAppConfigByType(ConfigurationType.EmailAttachments, true)
            .ConfigureAwait(false);

        if (config == null || string.IsNullOrWhiteSpace(config.Content))
        {
            return null;
        }

        var settings = JsonSerializer.Deserialize<EmailAttachmentSettings>(
            config.Content,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (settings == null || !settings.IncludeAttachments)
        {
            return null;
        }

        if (settings.MaxAttachments <= 0 || attachments.Count > settings.MaxAttachments)
        {
            return null;
        }

        foreach (var attachment in attachments)
        {
            if (settings.MaxAttachmentSizeInBytes > 0 && attachment.Content.LongLength > settings.MaxAttachmentSizeInBytes)
            {
                return null;
            }

            if (!IsAllowedAttachmentType(attachment, settings.AllowedFileTypes))
            {
                return null;
            }
        }

        return attachments;
    }

    private static bool IsAllowedAttachmentType(EmailAttachmentPayload attachment, IEnumerable<string> allowedFileTypes)
    {
        var allowedTypes = (allowedFileTypes ?? Enumerable.Empty<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim().ToLowerInvariant())
            .ToList();

        if (!allowedTypes.Any())
        {
            return false;
        }

        var extension = Path.GetExtension(attachment.FileName)?.TrimStart('.').ToLowerInvariant() ?? string.Empty;
        var dottedExtension = string.IsNullOrWhiteSpace(extension) ? string.Empty : $".{extension}";
        var contentType = attachment.ContentType?.Trim().ToLowerInvariant() ?? string.Empty;

        return allowedTypes.Any(allowedType =>
            allowedType == extension ||
            allowedType == dottedExtension ||
            (!string.IsNullOrWhiteSpace(contentType) && allowedType == contentType));
    }

    private static List<EmailAttachmentPayload> MapAttachments(IEnumerable<RequestEmailAttachmentData>? attachments)
    {
        if (attachments == null)
        {
            return new List<EmailAttachmentPayload>();
        }

        var mappedAttachments = new List<EmailAttachmentPayload>();
        foreach (var attachment in attachments)
        {
            if (attachment == null || string.IsNullOrWhiteSpace(attachment.FileName) || attachment.Content == null || attachment.Content.Length == 0)
            {
                return new List<EmailAttachmentPayload>();
            }

            mappedAttachments.Add(new EmailAttachmentPayload
            {
                FileName = attachment.FileName,
                ContentType = attachment.ContentType ?? string.Empty,
                Content = attachment.Content
            });
        }

        return mappedAttachments;
    }
}
