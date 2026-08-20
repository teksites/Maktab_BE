using System.Text;
using System.Security.Cryptography;
using System.Globalization;
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
        private static readonly SemaphoreSlim ReconciliationExecutionGate = new(1, 1);
        private const string RawWebhookPlaceholderCardType = "WEBHOOK_RAW";

        private readonly IHelcimTransactionRepository _repository;
        private readonly IHelcimClientConfiguration _clientConfiguration;
        private readonly IWebMsgSenderService _senderService;
        private readonly ICourseService _courseService;
        private readonly IStudentCourseTransactionService _studentCourseTransactionService;
        private readonly IStudentCourseEnrollmentService _studentCourseEnrollmentService;
        private readonly ICoursePaymentService _coursePaymentService;

        public HelcimTransactionService(
            IHelcimTransactionRepository repository,
            IHelcimClientConfiguration clientConfiguration,
            IWebMsgSenderService senderService,
            ICourseService courseService,
            IStudentCourseTransactionService studentCourseTransactionService,
            IStudentCourseEnrollmentService studentCourseEnrollmentService,
            ICoursePaymentService coursePaymentService)
        {
            _repository = repository;
            _clientConfiguration = clientConfiguration;
            _senderService = senderService;
            _courseService = courseService;
            _studentCourseTransactionService = studentCourseTransactionService;
            _studentCourseEnrollmentService = studentCourseEnrollmentService;
            _coursePaymentService = coursePaymentService;
        }

        public async Task<HelcimPayInitializeResponse> InitializePayment(InitiatePaymentRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            var invoiceNumber = await BuildInvoiceNumber(request).ConfigureAwait(false);
            var amount = Convert.ToDecimal(request.Amount);
            var terminalId = await ResolveTerminalId(request).ConfigureAwait(false);
            ValidateTerminalId(terminalId);

            var helcimRequestPayload = BuildInitializePaymentPayload(request, invoiceNumber, amount, terminalId);

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

        public async Task<HelcimPaymentCompletionResponse> CompleteHelcimPayPayment(CompleteHelcimPayPaymentRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            var rawDataToken = JToken.Parse(request.RawDataResponse.GetRawText());
            if (rawDataToken.Type != JTokenType.Object)
            {
                throw new InvalidOperationException("HelcimPay completion payload must be a JSON object.");
            }

            var responseData = (JObject)rawDataToken;
            var transactionId = responseData.Value<int?>("transactionId")
                ?? throw new InvalidOperationException("HelcimPay completion payload did not include a transactionId.");
            var invoiceNumber = responseData.Value<string>("invoiceNumber");
            if (string.IsNullOrWhiteSpace(invoiceNumber))
            {
                throw new InvalidOperationException("HelcimPay completion payload did not include an invoiceNumber.");
            }

            var paymentFlow = IsAchTransactionResponse(responseData) ? "ach" : "card";
            var existingTransactions = await _repository.GetByTransactionId(transactionId).ConfigureAwait(false)
                ?? new List<HelcimTransactionResponse>();
            if (existingTransactions.Any())
            {
                return new HelcimPaymentCompletionResponse
                {
                    Success = true,
                    Duplicate = true,
                    InvoiceId = 0,
                    TransactionId = transactionId,
                    InvoiceNumber = invoiceNumber,
                    PaymentFlow = paymentFlow
                };
            }

            var invoice = await GetInvoiceByInvoiceNumber(invoiceNumber).ConfigureAwait(false);
            var localTransaction = await ResolveLocalTransactionAsync(invoice, transactionId).ConfigureAwait(false);

            AddHelcimTransactionDetails transactionDetails;
            if (paymentFlow == "ach")
            {
                var achTransactionRaw = await SendGetRequest(BuildVersionedEndpoint($"/ach/transactions/{transactionId}")).ConfigureAwait(false);
                var achTransaction = ExtractAchTransaction(achTransactionRaw);

                if (ShouldApplyAchPayment(invoice, achTransaction))
                {
                    await ProcessPaidInvoiceAsync(
                        invoice,
                        transactionId,
                        achTransaction.Amount,
                        MapAchPaymentType(achTransaction),
                        localTransaction).ConfigureAwait(false);
                }

                transactionDetails = MapAchTransactionDetails(
                    invoice,
                    localTransaction,
                    responseData,
                    achTransactionRaw,
                    achTransaction,
                    null,
                    null);
            }
            else
            {
                var cardTransactionRaw = await SendGetRequest(BuildVersionedEndpoint($"/card-transactions/{transactionId}")).ConfigureAwait(false);
                var cardTransaction = JsonConvert.DeserializeObject<HelcimCardTransactionResponse>(cardTransactionRaw)
                    ?? throw new InvalidOperationException($"Unable to deserialize Helcim card transaction response. Raw response: {cardTransactionRaw}");

                if (ShouldApplyCardPayment(invoice, cardTransaction))
                {
                    await ProcessPaidInvoiceAsync(invoice, cardTransaction, localTransaction).ConfigureAwait(false);
                }

                transactionDetails = MapCardTransactionDetails(
                    invoice,
                    localTransaction,
                    cardTransactionRaw,
                    cardTransaction,
                    null,
                    null);
            }

            await _repository.Add(transactionDetails).ConfigureAwait(false);

            return new HelcimPaymentCompletionResponse
            {
                Success = true,
                Duplicate = false,
                InvoiceId = invoice.InvoiceId,
                TransactionId = transactionId,
                InvoiceNumber = invoiceNumber,
                PaymentFlow = paymentFlow
            };
        }

        public async Task<HelcimPaymentCompletionResponse> SyncInvoicePaymentByInvoiceId(int invoiceId)
            => await SyncInvoicePaymentByInvoiceIdInternal(invoiceId, null).ConfigureAwait(false);

        public async Task<HelcimReconciliationResponse> ReconcileTransactions(HelcimReconciliationRequest? request = null)
        {
            var endDate = (request?.EndDate ?? DateTime.UtcNow.Date).Date;
            var startDate = (request?.StartDate ?? endDate.AddDays(-(_clientConfiguration.ReconciliationLookbackDays - 1))).Date;
            if (startDate > endDate)
            {
                throw new ArgumentOutOfRangeException(nameof(request), "Reconciliation start date cannot be after the end date.");
            }

            if (!await ReconciliationExecutionGate.WaitAsync(0).ConfigureAwait(false))
            {
                return new HelcimReconciliationResponse
                {
                    StartDate = startDate,
                    EndDate = endDate,
                    AlreadyRunning = true,
                    Message = "Helcim reconciliation is already running. Wait for the current run to finish before starting another run."
                };
            }

            try
            {
                var response = new HelcimReconciliationResponse
                {
                    StartDate = startDate,
                    EndDate = endDate,
                    Message = "Helcim reconciliation completed."
                };

                var cardTransactions = await GetCardTransactionsForReconciliation(startDate, endDate).ConfigureAwait(false);
                response.CardTransactionsFetched = cardTransactions.Count;

                foreach (var cardTransaction in cardTransactions)
                {
                    var existingTransactions = await _repository.GetByTransactionId(cardTransaction.TransactionId).ConfigureAwait(false)
                        ?? new List<HelcimTransactionResponse>();
                    if (HasCompletedTransactionDetails(existingTransactions))
                    {
                        response.SkippedDuplicates++;
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(cardTransaction.InvoiceNumber))
                    {
                        response.UnmatchedTransactions++;
                        continue;
                    }

                    HelcimInvoiceResponse invoice;
                    try
                    {
                        invoice = await GetInvoiceByInvoiceNumber(cardTransaction.InvoiceNumber).ConfigureAwait(false);
                    }
                    catch
                    {
                        response.UnmatchedTransactions++;
                        continue;
                    }

                    var localTransaction = await ResolveLocalTransactionAsync(invoice).ConfigureAwait(false);
                    var completion = await SaveCardTransactionDetailsAsync(
                        invoice,
                        localTransaction,
                        JsonConvert.SerializeObject(cardTransaction),
                        cardTransaction,
                        null).ConfigureAwait(false);

                    if (!completion.Duplicate)
                    {
                        response.StoredTransactions++;
                        UpdateAppliedCounts(response, MapPaymentType(cardTransaction.Type), WasLocalPaymentApplied(invoice, cardTransaction));
                    }
                    else
                    {
                        response.SkippedDuplicates++;
                    }
                }

                var achTransactions = await GetAchTransactionsForReconciliation(startDate, endDate).ConfigureAwait(false);
                response.AchTransactionsFetched = achTransactions.Count;

                foreach (var achLookup in achTransactions)
                {
                    var existingTransactions = await _repository.GetByTransactionId(achLookup.Transaction.TransactionId).ConfigureAwait(false)
                        ?? new List<HelcimTransactionResponse>();
                    if (HasCompletedTransactionDetails(existingTransactions))
                    {
                        response.SkippedDuplicates++;
                        continue;
                    }

                    if (!IsAchEligibleForStorage(achLookup.Transaction))
                    {
                        response.SkippedPendingAchTransactions++;
                        continue;
                    }

                    HelcimInvoiceResponse invoice;
                    try
                    {
                        invoice = await ResolveInvoiceForAchTransactionAsync(
                            achLookup.Transaction,
                            achLookup.Transaction.OrderId,
                            achLookup.Transaction.InvoiceNumber).ConfigureAwait(false);
                    }
                    catch
                    {
                        response.UnmatchedTransactions++;
                        continue;
                    }

                    var localTransaction = await ResolveLocalTransactionAsync(invoice).ConfigureAwait(false);
                    var completion = await SaveAchTransactionDetailsAsync(
                        invoice,
                        localTransaction,
                        achLookup.TransactionData,
                        achLookup.TransactionRaw,
                        achLookup.Transaction,
                        null).ConfigureAwait(false);

                    if (!completion.Duplicate)
                    {
                        response.StoredTransactions++;
                        if (IsAchSettled(achLookup.Transaction))
                        {
                            UpdateAppliedCounts(
                                response,
                                MapAchPaymentType(achLookup.Transaction),
                                WasLocalPaymentApplied(invoice, achLookup.Transaction));
                        }
                        else
                        {
                            response.StoredNonSettledAchTransactions++;
                        }
                    }
                    else
                    {
                        response.SkippedDuplicates++;
                    }
                }

                return response;
            }
            finally
            {
                ReconciliationExecutionGate.Release();
            }
        }

        public async Task<IReadOnlyList<HelcimAchRefundInvoiceSummaryResponse>> GetAchRefundInvoices(GetAchRefundInvoicesRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            var limit = request.Limit <= 0 ? 50 : Math.Min(request.Limit, 200);
            var page = request.Page <= 0 ? 1 : request.Page;
            var endDate = (request.EndDate ?? DateTime.UtcNow.Date).Date;
            var startDate = (request.StartDate ?? endDate.AddDays(-29)).Date;
            if (startDate > endDate)
            {
                throw new ArgumentOutOfRangeException(nameof(request), "StartDate cannot be after EndDate.");
            }

            var searchText = request.SearchText?.Trim() ?? string.Empty;
            var transactions = await GetAchTransactionsForReconciliation(startDate, endDate).ConfigureAwait(false);
            var summaries = new List<HelcimAchRefundInvoiceSummaryResponse>();

            foreach (var lookup in transactions)
            {
                var achTransaction = lookup.Transaction;
                var isRefundTransaction = IsAchRefundTransaction(achTransaction);
                if (!request.IncludeRefundTransactions && isRefundTransaction)
                {
                    continue;
                }

                var isRefundable = !isRefundTransaction && IsAchRefundable(achTransaction);
                if (request.OnlyRefundable && !isRefundable)
                {
                    continue;
                }

                HelcimInvoiceResponse? invoice = null;
                StudentCourseTransactionResponse? localTransaction = null;

                try
                {
                    invoice = await ResolveInvoiceForAchTransactionAsync(
                        achTransaction,
                        achTransaction.OrderId,
                        achTransaction.InvoiceNumber).ConfigureAwait(false);
                    localTransaction = await ResolveLocalTransactionAsync(invoice, achTransaction.TransactionId).ConfigureAwait(false);
                }
                catch
                {
                    // Keep the transaction in the list even if invoice resolution fails.
                }

                var summary = new HelcimAchRefundInvoiceSummaryResponse
                {
                    InvoiceId = invoice?.InvoiceId ?? achTransaction.InvoiceId ?? achTransaction.OrderId ?? 0,
                    InvoiceNumber = invoice?.InvoiceNumber
                        ?? achTransaction.InvoiceNumber
                        ?? achTransaction.OrderNumber
                        ?? string.Empty,
                    PaymentCode = invoice?.PaymentCode ?? localTransaction?.PaymentCode ?? string.Empty,
                    CustomerId = invoice?.CustomerId ?? 0,
                    CustomerCode = achTransaction.CustomerCode ?? string.Empty,
                    MaktabTransactionId = localTransaction?.StudentCourseTransactionId ?? Guid.Empty,
                    FamilyId = localTransaction?.FamilyId ?? Guid.Empty,
                    TransactionId = achTransaction.TransactionId,
                    OriginalTransactionId = achTransaction.OriginalTransactionId,
                    InvoiceAmount = invoice?.Amount ?? achTransaction.Amount,
                    InvoiceAmountPaid = invoice?.AmountPaid ?? 0m,
                    TransactionAmount = achTransaction.Amount,
                    Currency = invoice?.Currency ?? achTransaction.Currency,
                    InvoiceStatus = invoice?.Status ?? MaktabDataContracts.Enums.Helcim.HelcimInvoiceStatus.Due,
                    StatusAuth = achTransaction.StatusAuth,
                    StatusClearing = achTransaction.StatusClearing,
                    StatusBatch = achTransaction.StatusBatch,
                    InvoiceDateCreated = invoice?.DateCreated,
                    InvoiceDateUpdated = invoice?.DateUpdated,
                    InvoiceDatePaid = invoice?.DatePaid,
                    TransactionDateCreated = achTransaction.DateCreated,
                    TransactionDateClosed = achTransaction.DateClosed,
                    IsRefundTransaction = isRefundTransaction,
                    IsSettled = IsAchSettled(achTransaction),
                    IsRefundable = isRefundable
                };

                if (!string.IsNullOrWhiteSpace(searchText) && !MatchesRefundInvoiceSearch(summary, searchText))
                {
                    continue;
                }

                summaries.Add(summary);
            }

            return summaries
                .OrderByDescending(item => item.InvoiceDatePaid ?? item.TransactionDateClosed ?? item.TransactionDateCreated ?? DateTime.MinValue)
                .ThenByDescending(item => item.TransactionId)
                .Skip((page - 1) * limit)
                .Take(limit)
                .ToList();
        }

        public async Task<HelcimAchRefundResponse> RefundAchTransaction(RefundAchTransactionRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (request.TransactionId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(request.TransactionId), "ACH transactionId must be a positive value.");
            }

            if (request.Amount <= 0m)
            {
                throw new ArgumentOutOfRangeException(nameof(request.Amount), "Refund amount must be greater than zero.");
            }

            var achTransactionLookup = await TryGetAchTransactionByTransactionId(request.TransactionId).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"Unable to find Helcim ACH transaction {request.TransactionId}.");

            var invoice = await ResolveInvoiceForAchTransactionAsync(
                achTransactionLookup.Transaction,
                achTransactionLookup.Transaction.OrderId,
                achTransactionLookup.Transaction.InvoiceNumber).ConfigureAwait(false);
            var localTransaction = await ResolveLocalTransactionAsync(invoice, request.TransactionId).ConfigureAwait(false);

            return await RefundAchTransactionInternal(
                achTransactionLookup,
                invoice,
                localTransaction,
                request.Amount,
                request.IdempotencyKey).ConfigureAwait(false);
        }

        public async Task<HelcimAchRefundResponse> RefundAchInvoice(RefundAchInvoiceRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (request.InvoiceId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(request.InvoiceId), "InvoiceId must be a positive value.");
            }

            if (request.Amount <= 0m)
            {
                throw new ArgumentOutOfRangeException(nameof(request.Amount), "Refund amount must be greater than zero.");
            }

            var invoice = await GetInvoiceByInvoiceId(request.InvoiceId).ConfigureAwait(false);
            var refundableTransaction = (await GetAchTransactionsByInvoiceAsync(invoice).ConfigureAwait(false))
                .Where(lookup => !IsAchRefundTransaction(lookup.Transaction))
                .Where(lookup => IsAchRefundable(lookup.Transaction))
                .OrderByDescending(lookup => lookup.Transaction.TransactionId)
                .FirstOrDefault();

            if (refundableTransaction == null)
            {
                throw new InvalidOperationException($"No refundable ACH transaction was found for invoiceId {request.InvoiceId}.");
            }

            var localTransaction = await ResolveLocalTransactionAsync(
                invoice,
                refundableTransaction.Transaction.TransactionId).ConfigureAwait(false);

            return await RefundAchTransactionInternal(
                refundableTransaction,
                invoice,
                localTransaction,
                request.Amount,
                request.IdempotencyKey).ConfigureAwait(false);
        }

        private async Task<HelcimAchRefundResponse> RefundAchTransactionInternal(
            AchTransactionLookup achTransactionLookup,
            HelcimInvoiceResponse invoice,
            StudentCourseTransactionResponse? localTransaction,
            decimal amount,
            string? idempotencyKeyInput)
        {
            var requestTransactionId = achTransactionLookup.Transaction.TransactionId;

            if (amount > achTransactionLookup.Transaction.Amount)
            {
                throw new InvalidOperationException("Refund amount cannot exceed the original ACH transaction amount.");
            }

            if (!IsAchRefundable(achTransactionLookup.Transaction))
            {
                throw new InvalidOperationException(
                    $"Helcim ACH transaction {requestTransactionId} is not in a refundable state.");
            }

            var idempotencyKey = string.IsNullOrWhiteSpace(idempotencyKeyInput)
                ? Guid.NewGuid().ToString()
                : idempotencyKeyInput.Trim();

            var responseJson = await SendJsonRequest(
                BuildVersionedEndpoint($"/ach/transactions/{requestTransactionId}/refund"),
                HttpMethod.Put,
                new JObject
                {
                    ["amount"] = amount
                },
                new Dictionary<string, string>
                {
                    ["idempotency-key"] = idempotencyKey
                }).ConfigureAwait(false);

            var localRefundRecorded = await TryApplyImmediateRefundPayment(
                invoice,
                requestTransactionId,
                amount,
                localTransaction).ConfigureAwait(false);

            var refundTransactionId = TryExtractRefundTransactionId(responseJson);
            var storedRefundTransaction = await TryStoreRefundTransactionAsync(
                invoice,
                localTransaction,
                requestTransactionId,
                refundTransactionId).ConfigureAwait(false);
            if (storedRefundTransaction.HasValue)
            {
                refundTransactionId = storedRefundTransaction;
            }

            return new HelcimAchRefundResponse
            {
                Success = true,
                TransactionId = requestTransactionId,
                InvoiceId = invoice.InvoiceId,
                InvoiceNumber = invoice.InvoiceNumber,
                Amount = amount,
                RefundTransactionId = refundTransactionId,
                LocalRefundRecorded = localRefundRecorded || refundTransactionId.HasValue,
                RequiresReconciliation = !refundTransactionId.HasValue,
                IdempotencyKey = idempotencyKey,
                RawResponse = responseJson
            };
        }

        private async Task<HelcimPaymentCompletionResponse> SyncInvoicePaymentByInvoiceIdInternal(int invoiceId, string? webhookRawBody)
        {
            var invoice = await GetInvoiceByInvoiceId(invoiceId).ConfigureAwait(false);
            var localTransaction = await ResolveLocalTransactionAsync(invoice).ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(invoice.InvoiceNumber))
            {
                var cardTransactionLookup = await TryGetCardTransactionByInvoiceNumber(invoice.InvoiceNumber).ConfigureAwait(false);
                if (cardTransactionLookup != null)
                {
                    return await SaveCardTransactionDetailsAsync(
                        invoice,
                        localTransaction,
                        cardTransactionLookup.TransactionRaw,
                        cardTransactionLookup.Transaction,
                        webhookRawBody).ConfigureAwait(false);
                }
            }

            var achTransactionLookup = await TryGetAchTransactionByInvoiceId(invoice).ConfigureAwait(false);
            if (achTransactionLookup != null)
            {
                return await SaveAchTransactionDetailsAsync(
                    invoice,
                    localTransaction,
                    achTransactionLookup.TransactionData,
                    achTransactionLookup.TransactionRaw,
                    achTransactionLookup.Transaction,
                    webhookRawBody).ConfigureAwait(false);
            }

            throw new InvalidOperationException(
                $"No Helcim payment transaction could be found for invoiceId {invoiceId} (invoiceNumber: {invoice.InvoiceNumber}).");
        }

        public Task AddTransactionDetails(AddHelcimTransactionDetails transactionDetails)
            => _repository.Add(transactionDetails);

        public async Task<HelcimWebhookHandlingStatus> HandleWebhook(
            string rawBody,
            string? webhookId,
            string? webhookTimestamp,
            string? signatureHeader)
        {
            if (string.IsNullOrWhiteSpace(rawBody))
            {
                throw new InvalidOperationException("Helcim webhook payload was empty.");
            }

            var webhookPayload = JObject.Parse(rawBody);

            if (!TryParseWebhookTimestamp(webhookTimestamp, out var parsedTimestamp)
                || IsExpiredWebhookTimestamp(parsedTimestamp))
            {
                return HelcimWebhookHandlingStatus.InvalidTimestamp;
            }

            if (!VerifyWebhookSignature(webhookId, webhookTimestamp, rawBody, signatureHeader))
            {
                return HelcimWebhookHandlingStatus.InvalidSignature;
            }

            var eventType = GetWebhookEventType(webhookPayload);
            if (string.Equals(eventType, "terminalCancel", StringComparison.OrdinalIgnoreCase))
            {
                return HelcimWebhookHandlingStatus.Ignored;
            }

            var transactionId = TryGetWebhookTransactionId(webhookPayload);
            var invoiceId = TryGetWebhookInvoiceId(webhookPayload);
            var invoiceNumber = TryGetWebhookInvoiceNumber(webhookPayload);
            var receivedAtUtc = DateTime.UtcNow;
            var reservationResult = await _repository.TryReserveWebhookProcessing(new ReserveHelcimWebhookProcessing
            {
                WebhookId = webhookId!,
                WebhookType = MapWebhookTypeForReservation(eventType),
                TransactionId = BuildWebhookReservationTransactionId(transactionId, invoiceId),
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
                if (transactionId.HasValue)
                {
                    var existingTransactions = await _repository.GetByTransactionId(transactionId.Value).ConfigureAwait(false)
                        ?? new List<HelcimTransactionResponse>();
                    if (HasCompletedTransactionDetails(existingTransactions))
                    {
                        await UpdateWebhookProcessing(
                            webhookId!,
                            HelcimWebhookProcessingStatus.Duplicate,
                            invoiceNumber,
                            null,
                            "Helcim transaction already exists locally.")
                            .ConfigureAwait(false);

                        return HelcimWebhookHandlingStatus.Duplicate;
                    }
                }

                if (transactionId.HasValue)
                {
                    await PersistRawWebhookRecordAsync(
                        transactionId.Value,
                        rawBody,
                        invoiceId,
                        invoiceNumber,
                        LooksLikeAchWebhook(webhookPayload)).ConfigureAwait(false);
                }

                HelcimPaymentCompletionResponse? result = null;
                if (transactionId.HasValue)
                {
                    var cardTransactionLookup = await TryGetCardTransactionById(transactionId.Value).ConfigureAwait(false);
                    if (cardTransactionLookup != null)
                    {
                        var invoice = await GetInvoiceByInvoiceNumber(cardTransactionLookup.Transaction.InvoiceNumber ?? invoiceNumber ?? string.Empty).ConfigureAwait(false);
                        var localTransaction = await ResolveLocalTransactionAsync(invoice, transactionId.Value).ConfigureAwait(false);
                        result = await SaveCardTransactionDetailsAsync(
                            invoice,
                            localTransaction,
                            cardTransactionLookup.TransactionRaw,
                            cardTransactionLookup.Transaction,
                            rawBody).ConfigureAwait(false);
                    }
                    else
                    {
                        var achTransactionLookup = await TryGetAchTransactionByTransactionId(transactionId.Value).ConfigureAwait(false);
                        if (achTransactionLookup != null)
                        {
                            var invoice = await ResolveInvoiceForAchTransactionAsync(
                                achTransactionLookup.Transaction,
                                invoiceId,
                                invoiceNumber).ConfigureAwait(false);
                            var localTransaction = await ResolveLocalTransactionAsync(invoice, transactionId.Value).ConfigureAwait(false);
                            result = await SaveAchTransactionDetailsAsync(
                                invoice,
                                localTransaction,
                                achTransactionLookup.TransactionData,
                                achTransactionLookup.TransactionRaw,
                                achTransactionLookup.Transaction,
                                rawBody).ConfigureAwait(false);
                        }
                    }
                }

                if (result == null && invoiceId.HasValue)
                {
                    result = await SyncInvoicePaymentByInvoiceIdInternal(invoiceId.Value, rawBody).ConfigureAwait(false);
                }

                if (result == null && !string.IsNullOrWhiteSpace(invoiceNumber))
                {
                    result = await SyncInvoicePaymentByInvoiceNumberAsync(invoiceNumber!, rawBody).ConfigureAwait(false);
                }

                if (result == null)
                {
                    throw new InvalidOperationException("Unable to resolve a Helcim transaction or invoice from the webhook payload.");
                }

                await UpdateWebhookProcessing(
                    webhookId!,
                    result.Duplicate ? HelcimWebhookProcessingStatus.Duplicate : HelcimWebhookProcessingStatus.Processed,
                    result.InvoiceNumber,
                    result.InvoiceId > 0 ? HelcimInvoiceStatus.Paid : null,
                    result.Duplicate ? "Helcim transaction already exists locally." : null,
                    processedOnUtc: DateTime.UtcNow)
                    .ConfigureAwait(false);

                return result.Duplicate
                    ? HelcimWebhookHandlingStatus.Duplicate
                    : HelcimWebhookHandlingStatus.Processed;
            }
            catch (Exception ex)
            {
                await UpdateWebhookProcessing(
                    webhookId!,
                    HelcimWebhookProcessingStatus.Failed,
                    invoiceNumber,
                    null,
                    ex.Message)
                    .ConfigureAwait(false);

                throw;
            }
        }

        public async Task<HelcimWebhookHandlingStatus> HandleWebhook(
            HelcimCardTransactionWebhookResponse webhook,
            string rawBody,
            string? webhookId,
            string? webhookTimestamp,
            string? signatureHeader)
            => await HandleWebhook(rawBody, webhookId, webhookTimestamp, signatureHeader).ConfigureAwait(false);

        private async Task ProcessPaidInvoiceAsync(
            HelcimInvoiceResponse invoice,
            HelcimCardTransactionResponse cardTransaction,
            StudentCourseTransactionResponse? transaction = null)
            => await ProcessPaidInvoiceAsync(
                invoice,
                cardTransaction.TransactionId,
                cardTransaction.Amount,
                MapPaymentType(cardTransaction.Type),
                transaction,
                null).ConfigureAwait(false);

        private async Task ProcessPaidInvoiceAsync(
            HelcimInvoiceResponse invoice,
            int transactionId,
            decimal amount,
            PaymentType paymentType,
            StudentCourseTransactionResponse? transaction = null,
            string? externalPaymentId = null)
        {
            transaction ??= await ResolveLocalTransactionAsync(invoice, transactionId).ConfigureAwait(false);

            if (transaction == null)
            {
                return;
            }

            var paymentMarker = BuildHelcimPaymentMarker(transactionId);
            var addPayment = new AddCoursePayment
            {
                AmountPaid = amount,
                Comments = paymentMarker,
                ExternalPaymentId = string.IsNullOrWhiteSpace(externalPaymentId)
                    ? transactionId.ToString(CultureInfo.InvariantCulture)
                    : externalPaymentId,
                FamilyId = transaction.FamilyId,
                IsActive = true,
                PaymentType = paymentType,
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

        private async Task<int?> ResolveTerminalId(InitiatePaymentRequest request)
        {
            var transaction = await ResolveTransaction(request).ConfigureAwait(false);
            if (transaction?.Enrollments == null || transaction.Enrollments.Count == 0)
            {
                return null;
            }

            var courseIds = transaction.Enrollments
                .Select(enrollment => enrollment.CourseId)
                .Where(courseId => courseId != Guid.Empty)
                .Distinct()
                .ToList();

            if (courseIds.Count == 0)
            {
                return null;
            }

            if (courseIds.Count > 1)
            {
                throw new InvalidOperationException(
                    $"Unable to resolve Helcim terminal for transaction {transaction.StudentCourseTransactionId} because it spans multiple courses.");
            }

            return await _courseService.GetHelcimTerminalId(courseIds[0]).ConfigureAwait(false);
        }

        private async Task<StudentCourseTransactionResponse?> ResolveTransaction(InitiatePaymentRequest request)
        {
            if (!string.IsNullOrWhiteSpace(request.PaymentCode))
            {
                var transactionByPaymentCode = await _studentCourseTransactionService
                    .GetTransactionByPaymentCode(request.PaymentCode)
                    .ConfigureAwait(false);

                if (transactionByPaymentCode != null)
                {
                    return transactionByPaymentCode;
                }
            }

            var transactionById = await _studentCourseTransactionService
                .GetTransaction(request.TransactionId)
                .ConfigureAwait(false);

            if (transactionById != null && !transactionById.IsActive)
            {
                throw new InvalidOperationException("Active student course transaction not found for the provided payment code or transaction id.");
            }

            return transactionById;
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
            decimal amount,
            int? terminalId)
        {
            var initializeRequest = new HelcimPayInitializeRequest
            {
                Amount = amount,
                PaymentType = HelcimPaymentType.Purchase,
                Currency = HelcimCurrency.Cad,
                PaymentMethod = HelcimPaymentMethod.CreditCardOrAch,
                InvoiceRequest = new HelcimInvoiceRequest
                {
                    InvoiceNumber = invoiceNumber,
                    PaymentCode = string.IsNullOrWhiteSpace(request.PaymentCode) ? null : request.PaymentCode,
                    Type = HelcimInvoiceType.Invoice,
                    LineItems = new List<HelcimInvoiceLineItemRequest>
                    {
                        new()
                        {
                            MaktabTransactionId = request.TransactionId,
                            UserIp = string.IsNullOrWhiteSpace(request.UserIp) ? null : request.UserIp,
                            Quantity = 1m,
                            Price = amount,
                            Total = amount
                        }
                    }
                }
            };

            if (terminalId.HasValue)
            {
                initializeRequest.TerminalId = terminalId.Value;
            }

            var serializedRequest = JObject.FromObject(initializeRequest, HelcimJsonSerializer);
            var payload = new JObject
            {
                ["paymentType"] = JToken.FromObject(HelcimPaymentType.Purchase, HelcimJsonSerializer),
                ["amount"] = JToken.FromObject(amount, HelcimJsonSerializer),
                ["currency"] = JToken.FromObject(HelcimCurrency.Cad, HelcimJsonSerializer),
                ["paymentMethod"] = JToken.FromObject(HelcimPaymentMethod.CreditCardOrAch, HelcimJsonSerializer),
                ["HelcimDigitalWalletRequest"] = JToken.FromObject(1, HelcimJsonSerializer)
            };

            if (serializedRequest.TryGetValue("terminalId", out var terminalIdToken))
            {
                payload["terminalId"] = terminalIdToken;
            }

            if (serializedRequest.TryGetValue("invoiceRequest", out var invoiceRequestToken)
                && invoiceRequestToken is JObject serializedInvoiceRequest)
            {
                var orderedInvoiceRequest = new JObject();

                if (serializedInvoiceRequest.TryGetValue("type", out var typeToken))
                {
                    orderedInvoiceRequest["type"] = typeToken;
                }

                if (serializedInvoiceRequest.TryGetValue("lineItems", out var lineItemsToken)
                    && lineItemsToken is JArray serializedLineItems)
                {
                    var orderedLineItems = new JArray();
                    foreach (var lineItemToken in serializedLineItems.OfType<JObject>())
                    {
                        var orderedLineItem = new JObject();

                        if (lineItemToken.TryGetValue("sku", out var skuToken))
                        {
                            orderedLineItem["sku"] = skuToken;
                        }

                        if (lineItemToken.TryGetValue("quantity", out var quantityToken))
                        {
                            orderedLineItem["quantity"] = quantityToken;
                        }

                        if (lineItemToken.TryGetValue("price", out var priceToken))
                        {
                            orderedLineItem["price"] = priceToken;
                        }

                        if (lineItemToken.TryGetValue("total", out var totalToken))
                        {
                            orderedLineItem["total"] = totalToken;
                        }

                        if (lineItemToken.TryGetValue("description", out var descriptionToken))
                        {
                            orderedLineItem["description"] = descriptionToken;
                        }

                        orderedLineItems.Add(orderedLineItem);
                    }

                    orderedInvoiceRequest["lineItems"] = orderedLineItems;
                }

                if (serializedInvoiceRequest.TryGetValue("invoiceNumber", out var invoiceNumberToken))
                {
                    orderedInvoiceRequest["invoiceNumber"] = invoiceNumberToken;
                }

                if (serializedInvoiceRequest.TryGetValue("notes", out var notesToken))
                {
                    orderedInvoiceRequest["notes"] = notesToken;
                }

                payload["invoiceRequest"] = orderedInvoiceRequest;
            }

            return payload;
        }

        private static void ValidateTerminalId(int? terminalId)
        {
            if (terminalId is <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(terminalId), "TerminalId must be a positive Helcim terminal identifier.");
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

        private static string BuildHelcimRefundExternalPaymentId(int originalTransactionId)
            => $"HEL-REFUND-{originalTransactionId}";

        private static string BuildHelcimExternalPaymentId(HelcimAchTransactionResponse achTransaction)
            => IsAchRefundTransaction(achTransaction)
                ? BuildHelcimRefundExternalPaymentId(
                    achTransaction.OriginalTransactionId > 0
                        ? achTransaction.OriginalTransactionId
                        : achTransaction.TransactionId)
                : achTransaction.TransactionId.ToString(CultureInfo.InvariantCulture);

        private static bool IsAchTransactionResponse(JObject responseData)
            => responseData["statusAuth"] != null
                || responseData["statusClearing"] != null
                || responseData["bankToken"] != null
                || string.Equals(responseData.Value<string>("type"), "WITHDRAWAL", StringComparison.OrdinalIgnoreCase);

        private static PaymentType MapAchPaymentType(HelcimAchTransactionResponse achTransaction)
            => IsAchRefundTransaction(achTransaction)
                ? PaymentType.Refund
                : PaymentType.Credit;

        private static HelcimCardTransactionStatus MapAchTransactionStatus(HelcimAchAuthorizationStatus statusAuth)
            => statusAuth switch
            {
                HelcimAchAuthorizationStatus.Declined => HelcimCardTransactionStatus.Declined,
                _ => HelcimCardTransactionStatus.Approved
            };

        private static bool IsAchRefundTransaction(HelcimAchTransactionResponse achTransaction)
            => achTransaction.OriginalTransactionId > 0
                || string.Equals(achTransaction.LegacyType, "REFUND", StringComparison.OrdinalIgnoreCase);

        private static bool IsAchSettled(HelcimAchTransactionResponse achTransaction)
            => achTransaction.StatusAuth == HelcimAchAuthorizationStatus.Approved
                && achTransaction.StatusClearing == HelcimAchClearingStatus.Approved
                && achTransaction.DateClosed.HasValue;

        private static bool IsAchEligibleForStorage(HelcimAchTransactionResponse achTransaction)
            => IsAchSettled(achTransaction)
                || (achTransaction.DateClosed.HasValue
                    && (achTransaction.StatusAuth == HelcimAchAuthorizationStatus.Declined
                        || achTransaction.StatusAuth == HelcimAchAuthorizationStatus.Cancelled
                        || achTransaction.StatusClearing == HelcimAchClearingStatus.Declined));

        private static bool IsAchRefundable(HelcimAchTransactionResponse achTransaction)
            => achTransaction.StatusBatch == HelcimAchBatchStatus.Closed
                && achTransaction.StatusAuth == HelcimAchAuthorizationStatus.Approved;

        private static bool ShouldApplyCardPayment(HelcimInvoiceResponse invoice, HelcimCardTransactionResponse cardTransaction)
            => cardTransaction.CardTransactionStatus == HelcimCardTransactionStatus.Approved
                && ShouldApplyInvoicePayment(invoice, MapPaymentType(cardTransaction.Type));

        private static bool ShouldApplyAchPayment(HelcimInvoiceResponse invoice, HelcimAchTransactionResponse achTransaction)
            => IsAchSettled(achTransaction)
                && ShouldApplyInvoicePayment(invoice, MapAchPaymentType(achTransaction));

        private static bool ShouldApplyInvoicePayment(HelcimInvoiceResponse invoice, PaymentType paymentType)
            => paymentType == PaymentType.Refund
                || invoice.Status == HelcimInvoiceStatus.Paid
                || invoice.Status == HelcimInvoiceStatus.Completed;

        private static bool WasLocalPaymentApplied(HelcimInvoiceResponse invoice, HelcimCardTransactionResponse cardTransaction)
            => ShouldApplyCardPayment(invoice, cardTransaction);

        private static bool WasLocalPaymentApplied(HelcimInvoiceResponse invoice, HelcimAchTransactionResponse achTransaction)
            => ShouldApplyAchPayment(invoice, achTransaction);

        private static void UpdateAppliedCounts(
            HelcimReconciliationResponse response,
            PaymentType paymentType,
            bool wasApplied)
        {
            if (!wasApplied)
            {
                return;
            }

            if (paymentType == PaymentType.Refund)
            {
                response.AppliedRefunds++;
                return;
            }

            response.AppliedPayments++;
        }

        private async Task<HelcimInvoiceResponse> GetInvoiceByInvoiceNumber(string invoiceNumber)
        {
            var invoiceEndpoint = BuildVersionedEndpoint($"/invoices/?invoiceNumber={Uri.EscapeDataString(invoiceNumber)}");
            var invoiceRaw = await SendGetRequest(invoiceEndpoint).ConfigureAwait(false);
            var invoiceResponses = JsonConvert.DeserializeObject<List<HelcimInvoiceResponse>>(invoiceRaw);
            return invoiceResponses?.FirstOrDefault()
                ?? throw new InvalidOperationException($"Helcim invoice lookup for invoice number {invoiceNumber} returned no results.");
        }

        private async Task<HelcimInvoiceResponse> GetInvoiceByInvoiceId(int invoiceId)
        {
            var invoiceEndpoint = BuildVersionedEndpoint($"/invoices/{invoiceId}");
            var invoiceRaw = await SendGetRequest(invoiceEndpoint).ConfigureAwait(false);
            return JsonConvert.DeserializeObject<HelcimInvoiceResponse>(invoiceRaw)
                ?? throw new InvalidOperationException($"Unable to deserialize Helcim invoice response for invoiceId {invoiceId}. Raw response: {invoiceRaw}");
        }

        private async Task<StudentCourseTransactionResponse?> ResolveLocalTransactionAsync(
            HelcimInvoiceResponse invoice,
            int? helcimTransactionId = null)
        {
            if (helcimTransactionId.HasValue)
            {
                var transactionFromStoredHelcim = await ResolveLocalTransactionFromStoredHelcimAsync(helcimTransactionId.Value).ConfigureAwait(false);
                if (transactionFromStoredHelcim != null)
                {
                    return transactionFromStoredHelcim;
                }
            }

            if (string.IsNullOrWhiteSpace(invoice.PaymentCode))
            {
                var lineItemTransactionId = invoice.LineItems?.FirstOrDefault()?.MaktabTransactionId ?? Guid.Empty;
                return await ResolveLocalTransactionByIdentifiersAsync(lineItemTransactionId, null).ConfigureAwait(false);
            }

            var transaction = await ResolveLocalTransactionByIdentifiersAsync(Guid.Empty, invoice.PaymentCode).ConfigureAwait(false);

            if (transaction != null)
            {
                return transaction;
            }

            var fallbackTransactionId = invoice.LineItems?.FirstOrDefault()?.MaktabTransactionId ?? Guid.Empty;
            return await ResolveLocalTransactionByIdentifiersAsync(fallbackTransactionId, null).ConfigureAwait(false);
        }

        private async Task<StudentCourseTransactionResponse?> ResolveLocalTransactionFromStoredHelcimAsync(int helcimTransactionId)
        {
            var existingDetailedTransaction = await GetExistingDetailedTransactionAsync(helcimTransactionId).ConfigureAwait(false);
            var resolvedTransaction = await ResolveLocalTransactionByIdentifiersAsync(
                existingDetailedTransaction?.MaktabTransactionId ?? Guid.Empty,
                existingDetailedTransaction?.PaymentCode).ConfigureAwait(false);

            if (resolvedTransaction != null)
            {
                return resolvedTransaction;
            }

            var storedTransactions = await _repository.GetByTransactionId(helcimTransactionId).ConfigureAwait(false)
                ?? new List<HelcimTransactionResponse>();
            foreach (var storedTransaction in storedTransactions)
            {
                resolvedTransaction = await ResolveLocalTransactionByIdentifiersAsync(
                    storedTransaction.MaktabTransactionId,
                    storedTransaction.PaymentCode).ConfigureAwait(false);

                if (resolvedTransaction != null)
                {
                    return resolvedTransaction;
                }
            }

            return null;
        }

        private async Task<StudentCourseTransactionResponse?> ResolveLocalTransactionByIdentifiersAsync(
            Guid maktabTransactionId,
            string? paymentCode)
        {
            if (maktabTransactionId != Guid.Empty)
            {
                var transactionById = await _studentCourseTransactionService.GetTransaction(maktabTransactionId).ConfigureAwait(false);
                if (transactionById != null)
                {
                    return transactionById;
                }
            }

            if (string.IsNullOrWhiteSpace(paymentCode))
            {
                return null;
            }

            return await _studentCourseTransactionService
                .GetTransactionByPaymentCode(paymentCode)
                .ConfigureAwait(false);
        }

        private async Task<HelcimPaymentCompletionResponse> SyncInvoicePaymentByInvoiceNumberAsync(string invoiceNumber, string? webhookRawBody)
        {
            var invoice = await GetInvoiceByInvoiceNumber(invoiceNumber).ConfigureAwait(false);
            return await SyncInvoicePaymentByInvoiceIdInternal(invoice.InvoiceId, webhookRawBody).ConfigureAwait(false);
        }

        private async Task<CardTransactionLookup?> TryGetCardTransactionById(int transactionId)
        {
            try
            {
                var cardTransactionRaw = await SendGetRequest(BuildVersionedEndpoint($"/card-transactions/{transactionId}")).ConfigureAwait(false);
                var cardTransaction = JsonConvert.DeserializeObject<HelcimCardTransactionResponse>(cardTransactionRaw);
                if (cardTransaction == null || string.IsNullOrWhiteSpace(cardTransaction.InvoiceNumber))
                {
                    return null;
                }

                return new CardTransactionLookup
                {
                    Transaction = cardTransaction,
                    TransactionRaw = cardTransactionRaw
                };
            }
            catch
            {
                return null;
            }
        }

        private async Task<CardTransactionLookup?> TryGetCardTransactionByInvoiceNumber(string invoiceNumber)
        {
            var cardTransactionEndpoint = BuildVersionedEndpoint($"/card-transactions?invoiceNumber={Uri.EscapeDataString(invoiceNumber)}");
            var cardTransactionRaw = await SendGetRequest(cardTransactionEndpoint).ConfigureAwait(false);
            var cardTransactions = JsonConvert.DeserializeObject<List<HelcimCardTransactionResponse>>(cardTransactionRaw);
            var cardTransaction = cardTransactions?
                .Where(transaction => string.Equals(transaction.InvoiceNumber, invoiceNumber, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(transaction => transaction.TransactionId)
                .FirstOrDefault();

            if (cardTransaction == null)
            {
                return null;
            }

            return new CardTransactionLookup
            {
                Transaction = cardTransaction,
                TransactionRaw = JsonConvert.SerializeObject(cardTransaction)
            };
        }

        private async Task<AchTransactionLookup?> TryGetAchTransactionByTransactionId(int transactionId)
        {
            try
            {
                var achTransactionRaw = await SendGetRequest(BuildVersionedEndpoint($"/ach/transactions/{transactionId}")).ConfigureAwait(false);
                var achPayload = JObject.Parse(achTransactionRaw);
                var singleTransactionData = achPayload["transaction"] as JObject ?? achPayload;
                var achTransaction = ExtractAchTransaction(singleTransactionData);

                return new AchTransactionLookup
                {
                    Transaction = achTransaction,
                    TransactionData = singleTransactionData,
                    TransactionRaw = achTransactionRaw
                };
            }
            catch
            {
                return null;
            }
        }

        private async Task<AchTransactionLookup?> TryGetAchTransactionByInvoiceId(HelcimInvoiceResponse invoice)
        {
            var matchedTransaction = (await GetAchTransactionsByInvoiceAsync(invoice).ConfigureAwait(false))
                .OrderByDescending(lookup => lookup.Transaction.TransactionId)
                .FirstOrDefault();
            if (matchedTransaction == null)
            {
                return null;
            }

            var achTransactionEndpoint = BuildVersionedEndpoint($"/ach/transactions/{matchedTransaction.Transaction.TransactionId}");
            var achTransactionRaw = await SendGetRequest(achTransactionEndpoint).ConfigureAwait(false);
            var achPayload = JObject.Parse(achTransactionRaw);
            var singleTransactionData = achPayload["transaction"] as JObject ?? achPayload;
            var achTransaction = ExtractAchTransaction(singleTransactionData);

            return new AchTransactionLookup
            {
                Transaction = achTransaction,
                TransactionData = singleTransactionData,
                TransactionRaw = achTransactionRaw
            };
        }

        private async Task<List<AchTransactionLookup>> GetAchTransactionsByInvoiceAsync(HelcimInvoiceResponse invoice)
        {
            var searchStartDate = ResolveAchInvoiceSearchStartDate(invoice);
            var searchEndDate = DateTime.UtcNow.Date;
            var page = 1;
            var matchedTransactions = new List<AchTransactionLookup>();

            while (true)
            {
                var achTransactionsEndpoint = BuildVersionedEndpoint(
                    $"/ach/transactions?startDate={searchStartDate:yyyy-MM-dd}&endDate={searchEndDate:yyyy-MM-dd}&page={page}&limit={_clientConfiguration.AchReconciliationPageSize}");
                var achTransactionsRaw = await SendGetRequest(achTransactionsEndpoint, allowEmptyResponse: true).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(achTransactionsRaw))
                {
                    break;
                }

                var payload = JToken.Parse(achTransactionsRaw);
                var pageTransactions = EnumerateAchTransactions(payload).ToList();

                matchedTransactions.AddRange(pageTransactions
                    .Where(transaction =>
                        (transaction.Value<int?>("orderId") ?? transaction.Value<int?>("invoiceId")) == invoice.InvoiceId
                        || (!string.IsNullOrWhiteSpace(invoice.InvoiceNumber)
                            && string.Equals(
                                transaction.Value<string>("invoiceNumber")
                                    ?? transaction.Value<string>("orderNumber"),
                                invoice.InvoiceNumber,
                                StringComparison.OrdinalIgnoreCase)))
                    .Select(transactionData => new AchTransactionLookup
                    {
                        Transaction = ExtractAchTransaction(transactionData),
                        TransactionData = transactionData,
                        TransactionRaw = transactionData.ToString(Formatting.None)
                    }));

                if (pageTransactions.Count < _clientConfiguration.AchReconciliationPageSize)
                {
                    break;
                }

                page++;
            }

            return matchedTransactions;
        }

        private async Task<bool> TryApplyImmediateRefundPayment(
            HelcimInvoiceResponse invoice,
            int originalTransactionId,
            decimal amount,
            StudentCourseTransactionResponse? localTransaction)
        {
            if (localTransaction == null)
            {
                return false;
            }

            await ProcessPaidInvoiceAsync(
                invoice,
                originalTransactionId,
                amount,
                PaymentType.Refund,
                localTransaction,
                BuildHelcimRefundExternalPaymentId(originalTransactionId)).ConfigureAwait(false);

            return true;
        }

        private async Task<int?> TryStoreRefundTransactionAsync(
            HelcimInvoiceResponse invoice,
            StudentCourseTransactionResponse? localTransaction,
            int originalTransactionId,
            int? refundTransactionId)
        {
            AchTransactionLookup? refundLookup = null;

            if (refundTransactionId.HasValue && refundTransactionId.Value > 0)
            {
                refundLookup = await TryGetAchTransactionByTransactionId(refundTransactionId.Value).ConfigureAwait(false);
            }

            refundLookup ??= await TryFindRecentAchRefundTransaction(invoice, originalTransactionId).ConfigureAwait(false);
            if (refundLookup == null)
            {
                return null;
            }

            await SaveAchTransactionDetailsAsync(
                invoice,
                localTransaction,
                refundLookup.TransactionData,
                refundLookup.TransactionRaw,
                refundLookup.Transaction,
                null).ConfigureAwait(false);

            return refundLookup.Transaction.TransactionId;
        }

        private async Task<AchTransactionLookup?> TryFindRecentAchRefundTransaction(HelcimInvoiceResponse invoice, int originalTransactionId)
        {
            var transactions = await GetAchTransactionsByInvoiceAsync(invoice).ConfigureAwait(false);
            return transactions
                .Where(lookup => lookup.Transaction.OriginalTransactionId == originalTransactionId)
                .OrderByDescending(lookup => lookup.Transaction.TransactionId)
                .FirstOrDefault();
        }

        private static bool MatchesRefundInvoiceSearch(HelcimAchRefundInvoiceSummaryResponse summary, string searchText)
        {
            var normalizedSearch = searchText.Trim();
            if (string.IsNullOrWhiteSpace(normalizedSearch))
            {
                return true;
            }

            return summary.InvoiceId.ToString(CultureInfo.InvariantCulture).Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)
                || summary.TransactionId.ToString(CultureInfo.InvariantCulture).Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)
                || summary.InvoiceNumber.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)
                || summary.PaymentCode.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)
                || summary.CustomerCode.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase);
        }

        private static int? TryExtractRefundTransactionId(string responseJson)
        {
            try
            {
                var token = JToken.Parse(responseJson);
                return token["transaction"]?.Value<int?>("id")
                    ?? token.Value<int?>("id")
                    ?? token.Value<int?>("transactionId");
            }
            catch
            {
                return null;
            }
        }

        private async Task<HelcimPaymentCompletionResponse> SaveCardTransactionDetailsAsync(
            HelcimInvoiceResponse invoice,
            StudentCourseTransactionResponse? localTransaction,
            string cardTransactionRaw,
            HelcimCardTransactionResponse cardTransaction,
            string? webhookRawBody)
        {
            var existingTransactions = await _repository.GetByTransactionId(cardTransaction.TransactionId).ConfigureAwait(false)
                ?? new List<HelcimTransactionResponse>();
            if (HasCompletedTransactionDetails(existingTransactions))
            {
                return CreatePaymentCompletionResponse(invoice, cardTransaction.TransactionId, "card", duplicate: true);
            }

            if (ShouldApplyCardPayment(invoice, cardTransaction))
            {
                await ProcessPaidInvoiceAsync(invoice, cardTransaction, localTransaction).ConfigureAwait(false);
            }

            var existingDetailedTransaction = await GetExistingDetailedTransactionAsync(cardTransaction.TransactionId).ConfigureAwait(false);
            var transactionDetails = MapCardTransactionDetails(
                invoice,
                localTransaction,
                cardTransactionRaw,
                cardTransaction,
                webhookRawBody,
                existingDetailedTransaction);

            await SaveTransactionDetailsAsync(transactionDetails, existingTransactions.Any()).ConfigureAwait(false);

            return CreatePaymentCompletionResponse(invoice, cardTransaction.TransactionId, "card", duplicate: false);
        }

        private async Task<HelcimPaymentCompletionResponse> SaveAchTransactionDetailsAsync(
            HelcimInvoiceResponse invoice,
            StudentCourseTransactionResponse? localTransaction,
            JObject transactionData,
            string achTransactionRaw,
            HelcimAchTransactionResponse achTransaction,
            string? webhookRawBody)
        {
            var existingTransactions = await _repository.GetByTransactionId(achTransaction.TransactionId).ConfigureAwait(false)
                ?? new List<HelcimTransactionResponse>();
            if (HasCompletedTransactionDetails(existingTransactions))
            {
                return CreatePaymentCompletionResponse(invoice, achTransaction.TransactionId, "ach", duplicate: true);
            }

            if (ShouldApplyAchPayment(invoice, achTransaction))
            {
                await ProcessPaidInvoiceAsync(
                    invoice,
                    achTransaction.TransactionId,
                    achTransaction.Amount,
                    MapAchPaymentType(achTransaction),
                    localTransaction,
                    BuildHelcimExternalPaymentId(achTransaction)).ConfigureAwait(false);
            }

            var existingDetailedTransaction = await GetExistingDetailedTransactionAsync(achTransaction.TransactionId).ConfigureAwait(false);
            var transactionDetails = MapAchTransactionDetails(
                invoice,
                localTransaction,
                transactionData,
                achTransactionRaw,
                achTransaction,
                webhookRawBody,
                existingDetailedTransaction);

            await SaveTransactionDetailsAsync(transactionDetails, existingTransactions.Any()).ConfigureAwait(false);

            return CreatePaymentCompletionResponse(invoice, achTransaction.TransactionId, "ach", duplicate: false);
        }

        private static HelcimPaymentCompletionResponse CreatePaymentCompletionResponse(
            HelcimInvoiceResponse invoice,
            int transactionId,
            string paymentFlow,
            bool duplicate)
            => new()
            {
                Success = true,
                Duplicate = duplicate,
                InvoiceId = invoice.InvoiceId,
                TransactionId = transactionId,
                InvoiceNumber = invoice.InvoiceNumber ?? string.Empty,
                PaymentFlow = paymentFlow
            };

        private static AddHelcimTransactionDetails MapCardTransactionDetails(
            HelcimInvoiceResponse invoice,
            StudentCourseTransactionResponse? localTransaction,
            string cardTransactionRaw,
            HelcimCardTransactionResponse cardTransaction,
            string? webhookRawBody,
            HelcimTransactionResponseDetailed? existingDetailedTransaction)
        {
            var lineItem = invoice.LineItems?.FirstOrDefault();
            return new AddHelcimTransactionDetails
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
                RawResponse = BuildStoredInvoiceResponse(JsonConvert.SerializeObject(new[] { invoice }), webhookRawBody, existingDetailedTransaction?.RawResponse),
                TransactionResponse = BuildStoredTransactionResponse(cardTransactionRaw, webhookRawBody, existingDetailedTransaction?.TransactionResponse),
                FamilyId = localTransaction?.FamilyId ?? Guid.Empty
            };
        }

        private static AddHelcimTransactionDetails MapAchTransactionDetails(
            HelcimInvoiceResponse invoice,
            StudentCourseTransactionResponse? localTransaction,
            JObject transactionData,
            string achTransactionRaw,
            HelcimAchTransactionResponse achTransaction,
            string? webhookRawBody,
            HelcimTransactionResponseDetailed? existingDetailedTransaction)
        {
            var lineItem = invoice.LineItems?.FirstOrDefault();
            return new AddHelcimTransactionDetails
            {
                PaymentCode = invoice.PaymentCode ?? string.Empty,
                MaktabTransactionId = lineItem?.MaktabTransactionId ?? Guid.Empty,
                UserIp = lineItem?.UserIp ?? string.Empty,
                InvoiceId = invoice.InvoiceId,
                InvoiceNumber = invoice.InvoiceNumber ?? string.Empty,
                InvoiceToken = invoice.Token ?? string.Empty,
                CustomerId = invoice.CustomerId,
                CustomerCode = transactionData.Value<string>("customerCode") ?? achTransaction.CustomerCode,
                TransactionId = achTransaction.TransactionId,
                CardBatchId = achTransaction.BatchId,
                User = string.Empty,
                ApprovalCode = transactionData.Value<string>("approvalCode") ?? achTransaction.ApprovalCode,
                CardToken = transactionData.Value<string>("bankToken") ?? achTransaction.BankToken,
                CardNumber = MaskBankAccountNumber(transactionData.Value<string>("bankAccountNumber") ?? achTransaction.BankAccountNumber),
                CardHolderName = string.Empty,
                CardType = "ACH",
                AvsResponse = string.Empty,
                CvvResponse = string.Empty,
                Warning = string.Empty,
                Amount = achTransaction.Amount,
                AmountPaid = invoice.AmountPaid,
                Currency = achTransaction.Currency,
                InvoiceStatus = invoice.Status,
                CardTransactionStatus = MapAchTransactionStatus(achTransaction.StatusAuth),
                InvoiceType = invoice.Type,
                CardTransactionType = IsAchRefundTransaction(achTransaction)
                    ? HelcimCardTransactionType.Refund
                    : HelcimCardTransactionType.Purchase,
                CreatedAt = invoice.DateCreated ?? achTransaction.DateCreated,
                UpdatedOn = invoice.DateUpdated,
                DatePaid = invoice.DatePaid,
                IsActive = true,
                RawResponse = BuildStoredInvoiceResponse(JsonConvert.SerializeObject(new[] { invoice }), webhookRawBody, existingDetailedTransaction?.RawResponse),
                TransactionResponse = BuildStoredTransactionResponse(achTransactionRaw, webhookRawBody, existingDetailedTransaction?.TransactionResponse),
                FamilyId = localTransaction?.FamilyId ?? Guid.Empty
            };
        }

        private static HelcimAchTransactionResponse ExtractAchTransaction(string achTransactionRaw)
        {
            var payload = JObject.Parse(achTransactionRaw);
            return ExtractAchTransaction(payload["transaction"] as JObject ?? payload);
        }

        private static HelcimAchTransactionResponse ExtractAchTransaction(JObject transaction)
            => transaction.ToObject<HelcimAchTransactionResponse>()
                ?? throw new InvalidOperationException("Unable to deserialize Helcim ACH transaction response.");

        private static IEnumerable<JObject> EnumerateAchTransactions(JToken payload)
        {
            if (payload is JArray array)
            {
                return array.OfType<JObject>();
            }

            if (payload is not JObject root)
            {
                return Enumerable.Empty<JObject>();
            }

            if (root["transactions"] is JArray transactionsArray)
            {
                return transactionsArray.OfType<JObject>();
            }

            if (root["data"] is JArray dataArray)
            {
                return dataArray.OfType<JObject>();
            }

            if (root["transaction"] is JObject singleTransaction)
            {
                return new[] { singleTransaction };
            }

            return new[] { root };
        }

        private static DateTime ResolveAchInvoiceSearchStartDate(HelcimInvoiceResponse invoice)
        {
            var anchorDate = invoice.DatePaid
                ?? invoice.DateUpdated
                ?? invoice.DateIssued
                ?? invoice.DateCreated
                ?? DateTime.UtcNow;

            return anchorDate.Date.AddDays(-7);
        }

        private async Task<HelcimInvoiceResponse> ResolveInvoiceForAchTransactionAsync(
            HelcimAchTransactionResponse achTransaction,
            int? invoiceIdHint,
            string? invoiceNumberHint)
        {
            var resolvedInvoiceId = achTransaction.OrderId ?? achTransaction.InvoiceId;
            if (resolvedInvoiceId.HasValue && resolvedInvoiceId.Value > 0)
            {
                return await GetInvoiceByInvoiceId(resolvedInvoiceId.Value).ConfigureAwait(false);
            }

            if (invoiceIdHint.HasValue && invoiceIdHint.Value > 0)
            {
                return await GetInvoiceByInvoiceId(invoiceIdHint.Value).ConfigureAwait(false);
            }

            var resolvedInvoiceNumber = achTransaction.InvoiceNumber
                ?? achTransaction.OrderNumber
                ?? invoiceNumberHint;
            if (!string.IsNullOrWhiteSpace(resolvedInvoiceNumber))
            {
                return await GetInvoiceByInvoiceNumber(resolvedInvoiceNumber).ConfigureAwait(false);
            }

            throw new InvalidOperationException("Unable to resolve a Helcim invoice for the ACH transaction.");
        }

        private async Task PersistRawWebhookRecordAsync(
            int transactionId,
            string rawBody,
            int? invoiceId,
            string? invoiceNumber,
            bool isAch)
        {
            var existingTransactions = await _repository.GetByTransactionId(transactionId).ConfigureAwait(false)
                ?? new List<HelcimTransactionResponse>();
            if (HasCompletedTransactionDetails(existingTransactions))
            {
                return;
            }

            var existingDetailedTransaction = await GetExistingDetailedTransactionAsync(transactionId).ConfigureAwait(false);
            var placeholder = new AddHelcimTransactionDetails
            {
                PaymentCode = existingDetailedTransaction?.PaymentCode ?? string.Empty,
                MaktabTransactionId = existingDetailedTransaction?.MaktabTransactionId ?? Guid.Empty,
                UserIp = existingDetailedTransaction?.UserIp ?? string.Empty,
                InvoiceId = invoiceId ?? existingDetailedTransaction?.InvoiceId ?? 0,
                InvoiceNumber = invoiceNumber ?? existingDetailedTransaction?.InvoiceNumber ?? string.Empty,
                InvoiceToken = existingDetailedTransaction?.InvoiceToken ?? string.Empty,
                CustomerId = existingDetailedTransaction?.CustomerId ?? 0,
                CustomerCode = existingDetailedTransaction?.CustomerCode ?? string.Empty,
                TransactionId = transactionId,
                CardBatchId = existingDetailedTransaction?.CardBatchId ?? 0,
                User = existingDetailedTransaction?.User ?? string.Empty,
                ApprovalCode = existingDetailedTransaction?.ApprovalCode ?? string.Empty,
                CardToken = existingDetailedTransaction?.CardToken ?? string.Empty,
                CardNumber = existingDetailedTransaction?.CardNumber ?? string.Empty,
                CardHolderName = existingDetailedTransaction?.CardHolderName ?? string.Empty,
                CardType = RawWebhookPlaceholderCardType,
                AvsResponse = existingDetailedTransaction?.AvsResponse ?? string.Empty,
                CvvResponse = existingDetailedTransaction?.CvvResponse ?? string.Empty,
                Warning = existingDetailedTransaction?.Warning ?? string.Empty,
                Amount = existingDetailedTransaction?.Amount ?? 0m,
                AmountPaid = existingDetailedTransaction?.AmountPaid ?? 0m,
                Currency = existingDetailedTransaction?.Currency ?? HelcimCurrency.Cad,
                InvoiceStatus = existingDetailedTransaction?.InvoiceStatus ?? HelcimInvoiceStatus.Due,
                CardTransactionStatus = existingDetailedTransaction?.CardTransactionStatus ?? HelcimCardTransactionStatus.Approved,
                InvoiceType = existingDetailedTransaction?.InvoiceType ?? HelcimInvoiceType.Invoice,
                CardTransactionType = existingDetailedTransaction?.CardTransactionType ?? HelcimCardTransactionType.Purchase,
                CreatedAt = existingDetailedTransaction?.CreatedAt,
                UpdatedOn = DateTime.UtcNow,
                DatePaid = existingDetailedTransaction?.DatePaid,
                IsActive = true,
                RawResponse = BuildWebhookOnlyStoredResponse(rawBody, existingDetailedTransaction?.RawResponse),
                TransactionResponse = BuildWebhookOnlyStoredResponse(rawBody, existingDetailedTransaction?.TransactionResponse),
                FamilyId = existingDetailedTransaction?.FamilyId ?? Guid.Empty
            };

            await SaveTransactionDetailsAsync(placeholder, existingTransactions.Any()).ConfigureAwait(false);
        }

        private async Task<HelcimTransactionResponseDetailed?> GetExistingDetailedTransactionAsync(int transactionId)
        {
            var transactions = await _repository.GetDetailedByTransactionId(transactionId).ConfigureAwait(false);
            return transactions?.FirstOrDefault();
        }

        private async Task SaveTransactionDetailsAsync(AddHelcimTransactionDetails transactionDetails, bool updateExisting)
        {
            if (updateExisting)
            {
                await _repository.Update(transactionDetails).ConfigureAwait(false);
                return;
            }

            await _repository.Add(transactionDetails).ConfigureAwait(false);
        }

        private static bool HasCompletedTransactionDetails(IEnumerable<HelcimTransactionResponse> existingTransactions)
            => existingTransactions.Any(transaction => !IsPlaceholderTransaction(transaction));

        private static bool IsPlaceholderTransaction(HelcimTransactionResponse transaction)
            => string.Equals(transaction.CardType, RawWebhookPlaceholderCardType, StringComparison.OrdinalIgnoreCase)
                || (transaction.InvoiceId <= 0
                    && string.IsNullOrWhiteSpace(transaction.InvoiceNumber)
                    && transaction.Amount == 0m
                    && transaction.AmountPaid == 0m);

        private static string GetWebhookEventType(JObject webhookPayload)
            => webhookPayload.Value<string>("type")
                ?? (webhookPayload["data"] as JObject)?.Value<string>("type")
                ?? string.Empty;

        private static int? TryGetWebhookTransactionId(JObject webhookPayload)
        {
            var data = webhookPayload["data"] as JObject ?? webhookPayload;
            return data.Value<int?>("id") ?? data.Value<int?>("transactionId");
        }

        private static int? TryGetWebhookInvoiceId(JObject webhookPayload)
        {
            var data = webhookPayload["data"] as JObject ?? webhookPayload;
            return data.Value<int?>("invoiceId") ?? data.Value<int?>("orderId");
        }

        private static string? TryGetWebhookInvoiceNumber(JObject webhookPayload)
        {
            var data = webhookPayload["data"] as JObject ?? webhookPayload;
            return data.Value<string>("invoiceNumber") ?? data.Value<string>("orderNumber");
        }

        private static bool LooksLikeAchWebhook(JObject webhookPayload)
        {
            var data = webhookPayload["data"] as JObject ?? webhookPayload;
            var eventType = GetWebhookEventType(webhookPayload);
            return eventType.Contains("ach", StringComparison.OrdinalIgnoreCase)
                || data["statusAuth"] != null
                || data["statusClearing"] != null
                || data["bankToken"] != null
                || data["orderId"] != null;
        }

        private static HelcimWebhookEventType MapWebhookTypeForReservation(string eventType)
            => string.Equals(eventType, "terminalCancel", StringComparison.OrdinalIgnoreCase)
                ? HelcimWebhookEventType.TerminalCancel
                : HelcimWebhookEventType.CardTransaction;

        private static int BuildWebhookReservationTransactionId(int? transactionId, int? invoiceId)
        {
            if (transactionId.HasValue)
            {
                return transactionId.Value;
            }

            if (invoiceId.HasValue && invoiceId.Value > 0)
            {
                return -invoiceId.Value;
            }

            return 0;
        }

        private static string BuildWebhookOnlyStoredResponse(string rawBody, string? existingStoredResponse)
        {
            var wrapper = new JObject
            {
                ["webhookPayload"] = ParseStoredToken(rawBody)
            };

            var existingWebhookPayload = TryExtractStoredSection(existingStoredResponse, "webhookPayload");
            if (existingWebhookPayload != null)
            {
                wrapper["webhookPayload"] = existingWebhookPayload;
            }

            return wrapper.ToString(Formatting.None);
        }

        private static string BuildStoredInvoiceResponse(string invoiceRaw, string? webhookRawBody, string? existingStoredResponse)
        {
            var webhookPayload = TryExtractStoredSection(existingStoredResponse, "webhookPayload")
                ?? ParseStoredTokenOrNull(webhookRawBody);

            if (webhookPayload == null)
            {
                return invoiceRaw;
            }

            return new JObject
            {
                ["webhookPayload"] = webhookPayload,
                ["invoiceResponse"] = ParseStoredToken(invoiceRaw)
            }.ToString(Formatting.None);
        }

        private static string BuildStoredTransactionResponse(string transactionRaw, string? webhookRawBody, string? existingStoredResponse)
        {
            var webhookPayload = TryExtractStoredSection(existingStoredResponse, "webhookPayload")
                ?? ParseStoredTokenOrNull(webhookRawBody);

            if (webhookPayload == null)
            {
                return transactionRaw;
            }

            return new JObject
            {
                ["webhookPayload"] = webhookPayload,
                ["transactionResponse"] = ParseStoredToken(transactionRaw)
            }.ToString(Formatting.None);
        }

        private static JToken ParseStoredToken(string raw)
            => ParseStoredTokenOrNull(raw) ?? JValue.CreateString(raw);

        private static JToken? ParseStoredTokenOrNull(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            try
            {
                return JToken.Parse(raw);
            }
            catch
            {
                return JValue.CreateString(raw);
            }
        }

        private static JToken? TryExtractStoredSection(string? storedResponse, string sectionName)
        {
            if (string.IsNullOrWhiteSpace(storedResponse))
            {
                return null;
            }

            try
            {
                var token = JToken.Parse(storedResponse);
                return token is JObject wrapper && wrapper.TryGetValue(sectionName, out var section)
                    ? section
                    : null;
            }
            catch
            {
                return null;
            }
        }

        private static string MaskBankAccountNumber(string? bankAccountNumber)
        {
            if (string.IsNullOrWhiteSpace(bankAccountNumber))
            {
                return string.Empty;
            }

            var digits = new string(bankAccountNumber.Where(char.IsDigit).ToArray());
            if (digits.Length <= 4)
            {
                return digits;
            }

            return $"****{digits[^4..]}";
        }

        private static PaymentType MapPaymentType(HelcimCardTransactionType cardTransactionType)
            => cardTransactionType == HelcimCardTransactionType.Refund
                ? PaymentType.Refund
                : PaymentType.Credit;

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

        private async Task<List<HelcimCardTransactionResponse>> GetCardTransactionsForReconciliation(DateTime startDate, DateTime endDate)
        {
            var transactions = new List<HelcimCardTransactionResponse>();
            var page = 1;

            while (true)
            {
                var endpoint = BuildVersionedEndpoint(
                    $"/card-transactions?dateFrom={startDate:yyyy-MM-dd}&dateTo={endDate:yyyy-MM-dd}&page={page}&limit={_clientConfiguration.CardReconciliationPageSize}");
                var responseJson = await SendGetRequest(endpoint).ConfigureAwait(false);
                var pageTransactions = JsonConvert.DeserializeObject<List<HelcimCardTransactionResponse>>(responseJson)
                    ?? new List<HelcimCardTransactionResponse>();

                transactions.AddRange(pageTransactions);

                if (pageTransactions.Count < _clientConfiguration.CardReconciliationPageSize)
                {
                    break;
                }

                page++;
            }

            return transactions;
        }

        private async Task<List<AchTransactionLookup>> GetAchTransactionsForReconciliation(DateTime startDate, DateTime endDate)
        {
            var transactions = new List<AchTransactionLookup>();
            var page = 1;

            while (true)
            {
                var endpoint = BuildVersionedEndpoint(
                    $"/ach/transactions?startDate={startDate:yyyy-MM-dd}&endDate={endDate:yyyy-MM-dd}&page={page}&limit={_clientConfiguration.AchReconciliationPageSize}");
                var responseJson = await SendGetRequest(endpoint, allowEmptyResponse: true).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(responseJson))
                {
                    break;
                }

                var payload = JToken.Parse(responseJson);
                var pageTransactions = EnumerateAchTransactions(payload)
                    .Select(transactionData => new AchTransactionLookup
                    {
                        Transaction = ExtractAchTransaction(transactionData),
                        TransactionData = transactionData,
                        TransactionRaw = transactionData.ToString(Formatting.None)
                    })
                    .ToList();

                transactions.AddRange(pageTransactions);

                if (pageTransactions.Count < _clientConfiguration.AchReconciliationPageSize)
                {
                    break;
                }

                page++;
            }

            return transactions;
        }

        private async Task<string> SendGetRequest(string endpoint, bool allowEmptyResponse = false)
            => await SendJsonRequest(endpoint, HttpMethod.Get, null, allowEmptyResponse: allowEmptyResponse).ConfigureAwait(false);

        private async Task<string> SendJsonRequest(
            string endpoint,
            HttpMethod method,
            JToken? body,
            IDictionary<string, string>? additionalHeaders = null,
            bool allowEmptyResponse = false)
        {
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["accept"] = "application/json",
                ["api-token"] = _clientConfiguration.ApiToken
            };

            if (additionalHeaders != null)
            {
                foreach (var header in additionalHeaders)
                {
                    headers[header.Key] = header.Value;
                }
            }

            var payload = new JsonMessageData
            {
                ExternalEndpoint = endpoint,
                Payload = body == null
                    ? null
                    : new StringContent(body.ToString(Formatting.None), Encoding.UTF8, "application/json"),
                Headers = headers
            };

            var responseJson = await _senderService.SendMessage(payload, _clientConfiguration, method).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(responseJson))
            {
                if (allowEmptyResponse)
                {
                    return string.Empty;
                }

                throw new InvalidOperationException($"Helcim {method} request returned an empty response for endpoint {endpoint}.");
            }

            return responseJson;
        }

        private sealed class CardTransactionLookup
        {
            public HelcimCardTransactionResponse Transaction { get; set; } = null!;
            public string TransactionRaw { get; set; } = string.Empty;
        }

        private sealed class AchTransactionLookup
        {
            public HelcimAchTransactionResponse Transaction { get; set; } = null!;
            public JObject TransactionData { get; set; } = null!;
            public string TransactionRaw { get; set; } = string.Empty;
        }
    }
}
