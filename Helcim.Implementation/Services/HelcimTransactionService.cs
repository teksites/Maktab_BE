using System.Text;
using System.Security.Cryptography;
using Courses.Services;
using Helcim.Configuration;
using Helcim.Repository;
using Helcim.Services;
using InternalContracts;
using MaktabDataContracts.Enums;
using MaktabDataContracts.Enums.Helcim;
using MaktabDataContracts.Requests.Course;
using MaktabDataContracts.Requests.Helcim;
using MaktabDataContracts.Responses.Course;
using MaktabDataContracts.Responses.Helcim;
using MaktabDataContracts.Responses.Transactions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WebMsgSender;

namespace Helcim.Implementation.Services
{
    public class HelcimTransactionService : IHelcimTransactionService
    {
        private static readonly JsonSerializer HelcimJsonSerializer = JsonSerializer.CreateDefault(
            new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                DefaultValueHandling = DefaultValueHandling.Ignore
            });
        private static readonly TimeZoneInfo EasternTimeZone = GetEasternTimeZone();
        private static readonly TimeSpan WebhookTimestampTolerance = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan WebhookProcessingReservationTimeout = TimeSpan.FromMinutes(15);

        private readonly IHelcimTransactionRepository _repository;
        private readonly IHelcimClientConfiguration _clientConfiguration;
        private readonly IWebMsgSenderService _senderService;
        private readonly IStudentCourseTransactionService _studentCourseTransactionService;
        private readonly IStudentCourseEnrollmentService _studentCourseEnrollmentService;
        private readonly ICoursePaymentService _coursePaymentService;

        public HelcimTransactionService(
            IHelcimTransactionRepository repository,
            IHelcimClientConfiguration clientConfiguration,
            IWebMsgSenderService senderService,
            IStudentCourseTransactionService studentCourseTransactionService,
            IStudentCourseEnrollmentService studentCourseEnrollmentService,
            ICoursePaymentService coursePaymentService)
        {
            _repository = repository;
            _clientConfiguration = clientConfiguration;
            _senderService = senderService;
            _studentCourseTransactionService = studentCourseTransactionService;
            _studentCourseEnrollmentService = studentCourseEnrollmentService;
            _coursePaymentService = coursePaymentService;
        }

        public async Task<HelcimPayInitializeResponse> InitializePayment(InitiatePaymentRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            var invoiceNumber = await BuildInvoiceNumber(request).ConfigureAwait(false);
            var amount = Convert.ToDecimal(request.Amount);

            var helcimRequestPayload = BuildInitializePaymentPayload(request, invoiceNumber, amount);

            var payload = new JsonMessageData
            {
                ExternalEndpoint = BuildVersionedEndpoint(_clientConfiguration.RelativeUrl),
                Payload = new StringContent(
                    helcimRequestPayload.ToString(Formatting.None),
                    Encoding.UTF8,
                    "application/json"),
                Headers = new Dictionary<string, string>
                {
                    ["accept"] = "application/json",
                    ["api-token"] = _clientConfiguration.ApiToken
                }
            };

            var responseJson = await _senderService.SendMessage(payload, _clientConfiguration, HttpMethod.Post).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(responseJson))
            {
                throw new InvalidOperationException("Helcim initialize payment returned an empty response.");
            }

            var responseReceived = JsonConvert.DeserializeObject<HelcimPayInitialize>(responseJson);

            if (responseReceived == null)
            {
                throw new InvalidOperationException($"Unable to deserialize Helcim initialize payment response. Raw response: {responseJson}");
            }
            var response = new HelcimPayInitializeResponse
            {
                CheckoutToken = responseReceived.CheckoutToken
            };

            return response;
        }

        public Task AddTransactionDetails(AddHelcimTransactionDetails transactionDetails)
            => _repository.Add(transactionDetails);

        public async Task<HelcimWebhookHandlingStatus> HandleWebhook(
            HelcimCardTransactionWebhookResponse webhook,
            string rawBody,
            string? webhookId,
            string? webhookTimestamp,
            string? signatureHeader)
        {
            ArgumentNullException.ThrowIfNull(webhook);

            if (!TryParseWebhookTimestamp(webhookTimestamp, out var parsedTimestamp)
                || IsExpiredWebhookTimestamp(parsedTimestamp))
            {
                return HelcimWebhookHandlingStatus.InvalidTimestamp;
            }

            if (!VerifyWebhookSignature(webhookId, webhookTimestamp, rawBody, signatureHeader))
            {
                return HelcimWebhookHandlingStatus.InvalidSignature;
            }

            if (webhook.Type != HelcimWebhookEventType.CardTransaction)
            {
                return HelcimWebhookHandlingStatus.Ignored;
            }

            var receivedAtUtc = DateTime.UtcNow;
            var reservationResult = await _repository.TryReserveWebhookProcessing(new ReserveHelcimWebhookProcessing
            {
                WebhookId = webhookId!,
                WebhookType = webhook.Type,
                TransactionId = webhook.Id,
                WebhookTimestamp = parsedTimestamp.ToUnixTimeMilliseconds(),
                RawRequest = rawBody,
                SignatureHeader = signatureHeader ?? string.Empty,
                ReceivedAtUtc = receivedAtUtc,
                StaleBeforeUtc = receivedAtUtc - WebhookProcessingReservationTimeout
            }).ConfigureAwait(false);

            if (reservationResult == HelcimWebhookReservationResult.AlreadyProcessing)
            {
                return HelcimWebhookHandlingStatus.RetryLater;
            }

            if (reservationResult == HelcimWebhookReservationResult.AlreadyProcessed)
            {
                return HelcimWebhookHandlingStatus.Duplicate;
            }

            try
            {
                var existingTransactions = await _repository.GetByTransactionId(webhook.Id).ConfigureAwait(false);
                if (existingTransactions.Any())
                {
                    await UpdateWebhookProcessing(
                        webhookId!,
                        HelcimWebhookProcessingStatus.Duplicate,
                        null,
                        null,
                        "Helcim transaction already exists locally.")
                        .ConfigureAwait(false);

                    return HelcimWebhookHandlingStatus.Duplicate;
                }

                var cardTransactionEndpoint = BuildVersionedEndpoint($"/card-transactions/{webhook.Id}");
                var cardTransactionRaw = await SendGetRequest(cardTransactionEndpoint).ConfigureAwait(false);
                var cardTransaction = JsonConvert.DeserializeObject<HelcimCardTransactionResponse>(cardTransactionRaw);

                if (cardTransaction == null)
                {
                    throw new InvalidOperationException($"Unable to deserialize Helcim card transaction response. Raw response: {cardTransactionRaw}");
                }

                if (string.IsNullOrWhiteSpace(cardTransaction.InvoiceNumber))
                {
                    throw new InvalidOperationException($"Helcim card transaction {webhook.Id} did not return an invoice number.");
                }

                var invoiceEndpoint = BuildVersionedEndpoint($"/invoices/?invoiceNumber={Uri.EscapeDataString(cardTransaction.InvoiceNumber)}");
                var invoiceRaw = await SendGetRequest(invoiceEndpoint).ConfigureAwait(false);
                var invoiceResponses = JsonConvert.DeserializeObject<List<HelcimInvoiceResponse>>(invoiceRaw);
                var invoice = invoiceResponses?.FirstOrDefault();

                if (invoice == null)
                {
                    throw new InvalidOperationException($"Helcim invoice lookup for invoice number {cardTransaction.InvoiceNumber} returned no results.");
                }

                StudentCourseTransactionResponse? localTransaction = null;
                if (!string.IsNullOrWhiteSpace(invoice.PaymentCode))
                {
                    localTransaction = await _studentCourseTransactionService
                        .GetTransactionByPaymentCode(invoice.PaymentCode)
                        .ConfigureAwait(false);
                }

                if (invoice.Status == HelcimInvoiceStatus.Paid)
                {
                    await ProcessPaidInvoiceAsync(invoice, cardTransaction, localTransaction).ConfigureAwait(false);
                }

                var lineItem = invoice.LineItems?.FirstOrDefault();
                var transactionDetails = new AddHelcimTransactionDetails
                {
                    PaymentCode = invoice.PaymentCode ?? string.Empty,
                    MaktabTransactionId = lineItem?.MaktabTransactionId ?? Guid.Empty,
                    UserIp = lineItem?.UserIp ?? string.Empty,
                    InvoiceId = invoice.InvoiceId,
                    InvoiceNumber = invoice.InvoiceNumber ?? string.Empty,
                    InvoiceToken = invoice.Token ?? string.Empty,
                    CustomerId = invoice.CustomerId,
                    CustomerCode = cardTransaction.CustomerCode ?? string.Empty,
                    TransactionId = cardTransaction.TransactionId,
                    CardBatchId = cardTransaction.CardBatchId,
                    User = cardTransaction.User ?? string.Empty,
                    ApprovalCode = cardTransaction.ApprovalCode ?? string.Empty,
                    CardToken = cardTransaction.CardToken ?? string.Empty,
                    CardNumber = cardTransaction.CardNumber ?? string.Empty,
                    CardHolderName = cardTransaction.CardHolderName ?? string.Empty,
                    CardType = cardTransaction.CardType ?? string.Empty,
                    AvsResponse = cardTransaction.AvsResponse ?? string.Empty,
                    CvvResponse = cardTransaction.CvvResponse ?? string.Empty,
                    Warning = cardTransaction.Warning ?? string.Empty,
                    Amount = cardTransaction.Amount,
                    AmountPaid = invoice.AmountPaid,
                    Currency = cardTransaction.Currency,
                    InvoiceStatus = invoice.Status,
                    CardTransactionStatus = cardTransaction.CardTransactionStatus,
                    InvoiceType = invoice.Type,
                    CardTransactionType = cardTransaction.Type,
                    CreatedAt = invoice.DateCreated,
                    UpdatedOn = invoice.DateUpdated,
                    DatePaid = invoice.DatePaid,
                    IsActive = true,
                    RawResponse = invoiceRaw,
                    TransactionResponse = cardTransactionRaw,
                    FamilyId = localTransaction?.FamilyId ?? Guid.Empty
                };

                try
                {
                    await _repository.Add(transactionDetails).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    var existingTransactionsAfterInsertFailure = await _repository.GetByTransactionId(webhook.Id).ConfigureAwait(false);
                    if (existingTransactionsAfterInsertFailure.Any())
                    {
                        await UpdateWebhookProcessing(
                            webhookId!,
                            HelcimWebhookProcessingStatus.Duplicate,
                            invoice.InvoiceNumber,
                            invoice.Status,
                            "Helcim transaction was already saved by another processor.",
                            processedOnUtc: DateTime.UtcNow)
                            .ConfigureAwait(false);

                        return HelcimWebhookHandlingStatus.Duplicate;
                    }

                    throw;
                }

                await UpdateWebhookProcessing(
                    webhookId!,
                    HelcimWebhookProcessingStatus.Processed,
                    invoice.InvoiceNumber,
                    invoice.Status,
                    null,
                    processedOnUtc: DateTime.UtcNow)
                    .ConfigureAwait(false);

                return HelcimWebhookHandlingStatus.Processed;
            }
            catch (Exception ex)
            {
                await UpdateWebhookProcessing(
                    webhookId!,
                    HelcimWebhookProcessingStatus.Failed,
                    null,
                    null,
                    ex.Message)
                    .ConfigureAwait(false);

                throw;
            }
        }

        private async Task ProcessPaidInvoiceAsync(
            HelcimInvoiceResponse invoice,
            HelcimCardTransactionResponse cardTransaction,
            StudentCourseTransactionResponse? transaction = null)
        {
            if (string.IsNullOrWhiteSpace(invoice.PaymentCode))
            {
                return;
            }

            transaction ??= await _studentCourseTransactionService
                .GetTransactionByPaymentCode(invoice.PaymentCode)
                .ConfigureAwait(false);

            if (transaction == null)
            {
                return;
            }

            var paymentMarker = BuildHelcimPaymentMarker(cardTransaction.TransactionId);
            var addPayment = new AddCoursePayment
            {
                AmountPaid = cardTransaction.Amount,
                Comments = paymentMarker,
                ExternalPaymentId = cardTransaction.TransactionId.ToString(),
                FamilyId = transaction.FamilyId,
                IsActive = true,
                PaymentMode = PaymentMode.Helcim,
                StudentCourseTransactionId = transaction.StudentCourseTransactionId
            };

            var paymentResult = await _coursePaymentService.TryAddPayment(addPayment).ConfigureAwait(false);
            if (!paymentResult.Created)
            {
                return;
            }
        }

        public Task<List<HelcimTransactionResponse>> GetByFamilyId(Guid familyId)
            => _repository.GetByFamilyId(familyId);

        public Task<List<HelcimTransactionResponse>> GetByPaymentCode(string paymentCode)
            => _repository.GetByPaymentCode(paymentCode);

        public Task<List<HelcimTransactionResponseDetailed>> GetDetailedByFamilyId(Guid familyId)
            => _repository.GetDetailedByFamilyId(familyId);

        public Task<List<HelcimTransactionResponseDetailed>> GetDetailedByPaymentCode(string paymentCode)
            => _repository.GetDetailedByPaymentCode(paymentCode);

        public Task<List<HelcimTransactionResponse>> GetByMaktabTransactionId(Guid maktabTransactionId)
            => _repository.GetByMaktabTransactionId(maktabTransactionId);

        public Task<List<HelcimTransactionResponseDetailed>> GetDetailedByMaktabTransactionId(Guid maktabTransactionId)
            => _repository.GetDetailedByMaktabTransactionId(maktabTransactionId);

        private async Task<string> BuildInvoiceNumber(InitiatePaymentRequest request)
        {
            var existingTransactions = new List<HelcimTransactionResponse>();
            var normalizedPaymentCode = string.IsNullOrWhiteSpace(request.PaymentCode)
                ? "NOPAYMENTCODE"
                : request.PaymentCode.Trim();
            var timestamp = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, EasternTimeZone)
                .ToString("yyyyMMddHHmm");

            existingTransactions.AddRange(
                await _repository.GetByMaktabTransactionId(request.TransactionId).ConfigureAwait(false));

            if (!string.IsNullOrWhiteSpace(request.PaymentCode))
            {
                existingTransactions.AddRange(
                    await _repository.GetByPaymentCode(request.PaymentCode).ConfigureAwait(false));
            }

            var latestSequence = existingTransactions
                .Select(transaction => transaction.InvoiceNumber)
                .Where(invoiceNumber => !string.IsNullOrWhiteSpace(invoiceNumber))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(invoiceNumber => invoiceNumber!.StartsWith($"INV-{normalizedPaymentCode}-", StringComparison.OrdinalIgnoreCase))
                .Select(ParseInvoiceSequence)
                .DefaultIfEmpty(0)
                .Max();

            return $"INV-{normalizedPaymentCode}-{timestamp}-{latestSequence + 1}";
        }

        private static int ParseInvoiceSequence(string? invoiceNumber)
        {
            if (string.IsNullOrWhiteSpace(invoiceNumber))
            {
                return 0;
            }

            var lastDashIndex = invoiceNumber.LastIndexOf('-');
            if (lastDashIndex < 0 || lastDashIndex == invoiceNumber.Length - 1)
            {
                return 0;
            }

            return int.TryParse(invoiceNumber[(lastDashIndex + 1)..], out var sequence)
                ? sequence
                : 0;
        }

        private static JObject BuildInitializePaymentPayload(
            InitiatePaymentRequest request,
            string invoiceNumber,
            decimal amount)
        {
            var lineItem = new JObject
            {
                ["sku"] = JToken.FromObject(request.TransactionId, HelcimJsonSerializer),
                ["quantity"] = JToken.FromObject(1m, HelcimJsonSerializer),
                ["price"] = JToken.FromObject(amount, HelcimJsonSerializer),
                ["total"] = JToken.FromObject(amount, HelcimJsonSerializer)
            };

            AddStringProperty(lineItem, "description", request.UserIp);

            var invoiceRequest = new JObject
            {
                ["type"] = JToken.FromObject(HelcimInvoiceType.Invoice, HelcimJsonSerializer),
                ["lineItems"] = new JArray(lineItem)
            };

            AddStringProperty(invoiceRequest, "invoiceNumber", invoiceNumber);
            AddStringProperty(invoiceRequest, "notes", request.PaymentCode);

            var payload = new JObject
            {
                ["paymentType"] = JToken.FromObject(HelcimPaymentType.Purchase, HelcimJsonSerializer),
                ["amount"] = JToken.FromObject(amount, HelcimJsonSerializer),
                ["currency"] = JToken.FromObject(HelcimCurrency.Cad, HelcimJsonSerializer),
                ["paymentMethod"] = JToken.FromObject(HelcimPaymentMethod.CreditCardOrAch, HelcimJsonSerializer),//changed on faisal request 15 05 2026
                ["HelcimDigitalWalletRequest"] = JToken.FromObject(1, HelcimJsonSerializer),
                ["invoiceRequest"] = invoiceRequest
            };

            return payload;
        }

        private static void AddStringProperty(JObject target, string propertyName, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                target[propertyName] = value;
            }
        }

        private bool VerifyWebhookSignature(
            string? webhookId,
            string? webhookTimestamp,
            string rawBody,
            string? signatureHeader)
        {
            if (string.IsNullOrWhiteSpace(webhookId)
                || string.IsNullOrWhiteSpace(webhookTimestamp)
                || string.IsNullOrWhiteSpace(rawBody)
                || string.IsNullOrWhiteSpace(signatureHeader))
            {
                return false;
            }

            var signedContent = $"{webhookId}.{webhookTimestamp}.{rawBody}";
            var keyBytes = Convert.FromBase64String(_clientConfiguration.SignatureVerificationToken);
            using var hmac = new HMACSHA256(keyBytes);
            var computedSignature = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(signedContent)));

            var signatures = signatureHeader
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(value => value.Split(',', 2, StringSplitOptions.TrimEntries))
                .Where(parts => parts.Length == 2)
                .Select(parts => parts[1]);

            foreach (var signature in signatures)
            {
                if (HasMatchingSignature(signature, computedSignature))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryParseWebhookTimestamp(string? webhookTimestamp, out DateTimeOffset parsedTimestamp)
        {
            parsedTimestamp = default;
            if (string.IsNullOrWhiteSpace(webhookTimestamp) || !long.TryParse(webhookTimestamp, out var value))
            {
                return false;
            }

            try
            {
                parsedTimestamp = webhookTimestamp.Length > 10
                    ? DateTimeOffset.FromUnixTimeMilliseconds(value)
                    : DateTimeOffset.FromUnixTimeSeconds(value);
                return true;
            }
            catch (ArgumentOutOfRangeException)
            {
                return false;
            }
        }

        private static bool IsExpiredWebhookTimestamp(DateTimeOffset timestamp)
            => timestamp < DateTimeOffset.UtcNow - WebhookTimestampTolerance
                || timestamp > DateTimeOffset.UtcNow + WebhookTimestampTolerance;

        private static bool HasMatchingSignature(string signature, string computedSignature)
        {
            var providedBytes = Encoding.UTF8.GetBytes(signature);
            var computedBytes = Encoding.UTF8.GetBytes(computedSignature);

            return providedBytes.Length == computedBytes.Length
                && CryptographicOperations.FixedTimeEquals(providedBytes, computedBytes);
        }

        private static string BuildHelcimPaymentMarker(int transactionId)
            => $"Helcim payment applied for transactionId: {transactionId}";

        private Task UpdateWebhookProcessing(
            string webhookId,
            HelcimWebhookProcessingStatus processingStatus,
            string? invoiceNumber,
            HelcimInvoiceStatus? invoiceStatus,
            string? errorMessage,
            DateTime? processedOnUtc = null)
            => _repository.UpdateWebhookProcessing(new UpdateHelcimWebhookProcessing
            {
                WebhookId = webhookId,
                ProcessingStatus = processingStatus,
                InvoiceNumber = invoiceNumber,
                InvoiceStatus = invoiceStatus,
                ErrorMessage = errorMessage,
                UpdatedOnUtc = DateTime.UtcNow,
                ProcessedOnUtc = processedOnUtc
            });

        private string BuildVersionedEndpoint(string relativePath)
            => string.Concat(
                _clientConfiguration.BaseUrl.TrimEnd('/'),
                NormalizePath(_clientConfiguration.ApiVersionPath),
                NormalizePath(relativePath));

        private static TimeZoneInfo GetEasternTimeZone()
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
            }
            catch (TimeZoneNotFoundException)
            {
                return TimeZoneInfo.FindSystemTimeZoneById("America/Toronto");
            }
        }

        private static string NormalizePath(string path)
            => string.IsNullOrWhiteSpace(path)
                ? string.Empty
                : path.StartsWith("/", StringComparison.Ordinal)
                    ? path
                    : "/" + path;

        private async Task<string> SendGetRequest(string endpoint)
        {
            var payload = new JsonMessageData
            {
                ExternalEndpoint = endpoint,
                Payload = null,
                Headers = new Dictionary<string, string>
                {
                    ["accept"] = "application/json",
                    ["api-token"] = _clientConfiguration.ApiToken
                }
            };

            var responseJson = await _senderService.SendMessage(payload, _clientConfiguration, HttpMethod.Get).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(responseJson))
            {
                throw new InvalidOperationException($"Helcim GET request returned an empty response for endpoint {endpoint}.");
            }

            return responseJson;
        }
    }
}
