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
        private const int AchStatusAuthApproved = 1;
        private const int AchStatusAuthDeclined = 2;
        private const int AchStatusAuthCancelled = 4;
        private const int AchStatusAuthPending = 5;
        private const int AchStatusClearingApproved = 1;
        private const int AchStatusClearingDeclined = 4;
        private const int AchStatusBatchOpen = 1;
        private const int AchStatusBatchClosed = 2;
        private const string SavedCardVerificationInvoicePrefix = "INV-CARD-VERIFY-";

        private readonly IHelcimTransactionRepository _repository;
        private readonly IHelcimClientConfiguration _clientConfiguration;
        private readonly IWebMsgSenderService _senderService;
        private readonly ICourseService _courseService;
        private readonly IStudentCourseTransactionService _studentCourseTransactionService;
        private readonly IStudentCourseEnrollmentService _studentCourseEnrollmentService;
        private readonly ICoursePaymentService _coursePaymentService;
        private readonly ICardBinLookupService _cardBinLookupService;
        private readonly IHelcimCardVaultRepository? _cardVault;
        private readonly IHelcimCardTokenProtector? _cardTokenProtector;
        private readonly IHelcimPaymentAttemptRepository? _paymentAttempts;
        private readonly IHelcimCheckoutContextRepository? _checkoutContexts;

        public HelcimTransactionService(
            IHelcimTransactionRepository repository,
            IHelcimClientConfiguration clientConfiguration,
            IWebMsgSenderService senderService,
            ICourseService courseService,
            IStudentCourseTransactionService studentCourseTransactionService,
            IStudentCourseEnrollmentService studentCourseEnrollmentService,
            ICoursePaymentService coursePaymentService,
            ICardBinLookupService cardBinLookupService,
            IHelcimCardVaultRepository? cardVault = null,
            IHelcimCardTokenProtector? cardTokenProtector = null,
            IHelcimPaymentAttemptRepository? paymentAttempts = null,
            IHelcimCheckoutContextRepository? checkoutContexts = null)
        {
            _repository = repository;
            _clientConfiguration = clientConfiguration;
            _senderService = senderService;
            _courseService = courseService;
            _studentCourseTransactionService = studentCourseTransactionService;
            _studentCourseEnrollmentService = studentCourseEnrollmentService;
            _coursePaymentService = coursePaymentService;
            _cardBinLookupService = cardBinLookupService;
            _cardVault = cardVault;
            _cardTokenProtector = cardTokenProtector;
            _paymentAttempts = paymentAttempts;
            _checkoutContexts = checkoutContexts;
        }

        public async Task<HelcimPayInitializeResponse> InitializePayment(InitiatePaymentRequest request)
            => await InitializePaymentForSession(request, Guid.Empty, Guid.Empty).ConfigureAwait(false);

        public async Task<HelcimPayInitializeResponse> InitializePaymentForSession(InitiatePaymentRequest request, Guid userId, Guid familyId)
        {
            ArgumentNullException.ThrowIfNull(request);

            var invoiceNumber = await BuildInvoiceNumber(request).ConfigureAwait(false);
            if (request.SaveCardInfo)
            {
                if (userId == Guid.Empty || _checkoutContexts == null)
                    throw new InvalidOperationException("A signed-in user is required to save a payment card.");
            }
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

            // Only retain the opt-in after Helcim has created a usable checkout session.
            if (request.SaveCardInfo)
            {
                await _checkoutContexts!.Save(new HelcimCheckoutContext
                {
                    InvoiceNumber = invoiceNumber,
                    UserId = userId,
                    FamilyId = familyId,
                    SaveCardInfo = true
                }).ConfigureAwait(false);
            }

            var response = new HelcimPayInitializeResponse
            {
                CheckoutToken = responseReceived.CheckoutToken
            };

            return response;
        }

        public async Task<HelcimPayInitializeResponse> InitializeSavedCardVerification(Guid userId, Guid familyId)
        {
            if (userId == Guid.Empty)
            {
                throw new UnauthorizedAccessException("An active user session is required to save a payment card.");
            }

            if (_checkoutContexts == null || _cardVault == null || _cardTokenProtector == null)
            {
                throw new InvalidOperationException("Saved-card verification is not configured.");
            }

            var invoiceNumber = BuildSavedCardVerificationInvoiceNumber();
            var helcimRequestPayload = new JObject
            {
                ["paymentType"] = "verify",
                ["amount"] = 0m,
                ["currency"] = "CAD",
                // A profile vault stores reusable card tokens only. Bank-account collection is not part of this endpoint.
                ["paymentMethod"] = "cc",
                ["HelcimDigitalWalletRequest"] = 0,
                ["invoiceRequest"] = new JObject
                {
                    ["invoiceNumber"] = invoiceNumber,
                    ["type"] = "INVOICE",
                    ["notes"] = "Maktab profile saved-card verification"
                }
            };

            var payload = new JsonMessageData
            {
                ExternalEndpoint = BuildVersionedEndpoint(_clientConfiguration.RelativeUrl),
                Payload = new StringContent(helcimRequestPayload.ToString(Formatting.None), Encoding.UTF8, "application/json"),
                Headers = new Dictionary<string, string>
                {
                    ["accept"] = "application/json",
                    ["api-token"] = _clientConfiguration.ApiToken
                }
            };

            var responseJson = await _senderService.SendMessage(payload, _clientConfiguration, HttpMethod.Post).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(responseJson))
            {
                throw new InvalidOperationException("Helcim saved-card verification returned an empty response.");
            }

            var responseReceived = JsonConvert.DeserializeObject<HelcimPayInitialize>(responseJson)
                ?? throw new InvalidOperationException("Unable to deserialize Helcim saved-card verification response.");
            if (string.IsNullOrWhiteSpace(responseReceived.CheckoutToken))
            {
                throw new InvalidOperationException("Helcim saved-card verification did not return a checkout token.");
            }

            // Persist only after Helcim has issued a usable checkout session. The invoice prefix identifies this as a non-course flow.
            await _checkoutContexts.Save(new HelcimCheckoutContext
            {
                InvoiceNumber = invoiceNumber,
                UserId = userId,
                FamilyId = familyId,
                SaveCardInfo = true
            }).ConfigureAwait(false);

            return new HelcimPayInitializeResponse { CheckoutToken = responseReceived.CheckoutToken };
        }

        public async Task<SavedCardPaymentAttemptResponse> ChargeSavedCard(
            ChargeSavedCardRequest request,
            Guid userId,
            Guid familyId)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (userId == Guid.Empty || familyId == Guid.Empty)
                throw new UnauthorizedAccessException("An active parent session is required to charge a saved card.");
            if (request.CardId == Guid.Empty || request.TransactionId == Guid.Empty || request.Amount <= 0m
                || string.IsNullOrWhiteSpace(request.PaymentCode) || string.IsNullOrWhiteSpace(request.IdempotencyKey))
                throw new ArgumentException("Card, payment code, transaction, positive amount, and idempotency key are required.");
            if (_paymentAttempts == null)
                throw new InvalidOperationException("Saved-card payments are not configured.");

            var existingAttempt = await _paymentAttempts
                .GetByIdempotencyKey(request.IdempotencyKey.Trim(), userId).ConfigureAwait(false);
            if (existingAttempt != null)
                return MapAttempt(existingAttempt);

            if (_cardVault == null || _cardTokenProtector == null)
                throw new InvalidOperationException("Saved-card payments are not configured.");

            var transaction = await _studentCourseTransactionService
                .GetTransactionByPaymentCode(request.PaymentCode.Trim()).ConfigureAwait(false)
                ?? throw new KeyNotFoundException("No course transaction was found for the payment code.");
            if (!transaction.IsActive || transaction.StudentCourseTransactionId != request.TransactionId
                || transaction.FamilyId != familyId)
                throw new InvalidOperationException("The selected transaction does not belong to the active family.");

            var remainingAmount = decimal.Round(transaction.TotalPayable - transaction.TotalAmountPaid, 2);
            if (request.Amount > remainingAmount)
                throw new InvalidOperationException("The requested payment amount exceeds the outstanding balance.");

            var card = await _cardVault.GetActiveCard(request.CardId, userId).ConfigureAwait(false)
                ?? throw new KeyNotFoundException("The selected saved card is unavailable.");
            var initializeRequest = new InitiatePaymentRequest
            {
                PaymentCode = transaction.PaymentCode,
                TransactionId = transaction.StudentCourseTransactionId,
                Amount = (double)request.Amount,
                UserIp = request.UserIp
            };
            var invoiceNumber = await BuildInvoiceNumber(initializeRequest).ConfigureAwait(false);
            var attempt = new HelcimPaymentAttemptRecord
            {
                PaymentAttemptId = Guid.NewGuid(),
                UserId = userId,
                CardId = card.CardId,
                MaktabTransactionId = transaction.StudentCourseTransactionId,
                PaymentCode = transaction.PaymentCode,
                InvoiceNumber = invoiceNumber,
                IdempotencyKey = request.IdempotencyKey.Trim(),
                Amount = request.Amount,
                Status = HelcimPaymentAttemptStatus.Created
            };

            try
            {
                await _paymentAttempts.Add(attempt).ConfigureAwait(false);
            }
            catch
            {
                var duplicate = await _paymentAttempts
                    .GetByIdempotencyKey(attempt.IdempotencyKey, userId).ConfigureAwait(false);
                if (duplicate != null)
                {
                    return MapAttempt(duplicate);
                }

                throw;
            }

            try
            {
                var terminalId = await ResolveTerminalId(initializeRequest).ConfigureAwait(false);
                ValidateTerminalId(terminalId);
                var token = _cardTokenProtector.Unprotect(new ProtectedHelcimCardToken
                {
                    Ciphertext = card.TokenCiphertext,
                    Nonce = card.TokenNonce,
                    Tag = card.TokenTag,
                    Hash = card.TokenHash
                });
                var responseJson = await SendJsonRequest(
                    BuildVersionedEndpoint("/payment/purchase"),
                    HttpMethod.Post,
                    BuildSavedCardPurchasePayload(initializeRequest, invoiceNumber, request.Amount, token, terminalId),
                    new Dictionary<string, string> { ["idempotency-key"] = attempt.IdempotencyKey }).ConfigureAwait(false);
                var providerResponse = JObject.Parse(responseJson);
                var status = providerResponse.Value<string>("status");
                var transactionId = providerResponse.Value<int?>("transactionId")
                    ?? providerResponse["transaction"]?.Value<int?>("transactionId");
                if (!string.Equals(status, "APPROVED", StringComparison.OrdinalIgnoreCase))
                {
                    var error = providerResponse.Value<string>("error") ?? "Helcim declined the saved-card payment.";
                    await _paymentAttempts.UpdateResult(attempt.PaymentAttemptId, HelcimPaymentAttemptStatus.Declined, transactionId, error).ConfigureAwait(false);
                    return new SavedCardPaymentAttemptResponse { PaymentAttemptId = attempt.PaymentAttemptId, Status = "declined", InvoiceNumber = invoiceNumber, HelcimTransactionId = transactionId, Error = error };
                }

                await _paymentAttempts.UpdateResult(attempt.PaymentAttemptId, HelcimPaymentAttemptStatus.ApprovedAwaitingConfirmation, transactionId, null).ConfigureAwait(false);
                return new SavedCardPaymentAttemptResponse { PaymentAttemptId = attempt.PaymentAttemptId, Status = "awaiting_confirmation", AcceptedByHelcim = true, AwaitingConfirmation = true, InvoiceNumber = invoiceNumber, HelcimTransactionId = transactionId };
            }
            catch (Exception exception)
            {
                await _paymentAttempts.UpdateResult(attempt.PaymentAttemptId, HelcimPaymentAttemptStatus.Failed, null, exception.Message).ConfigureAwait(false);
                throw;
            }
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
            var existingTransactions = await GetStoredTransactionsByIdAsync(transactionId).ConfigureAwait(false);
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

        public async Task<HelcimPaymentCompletionResponse> SyncInvoicePayment(string invoiceReference)
        {
            if (string.IsNullOrWhiteSpace(invoiceReference))
            {
                throw new ArgumentException("Invoice reference is required.", nameof(invoiceReference));
            }

            var normalizedReference = invoiceReference.Trim();
            return int.TryParse(normalizedReference, out var invoiceId)
                ? await SyncInvoicePaymentByInvoiceId(invoiceId).ConfigureAwait(false)
                : await SyncInvoicePaymentByInvoiceNumber(normalizedReference).ConfigureAwait(false);
        }

        public async Task<HelcimPaymentCompletionResponse> SyncInvoicePaymentByInvoiceNumber(string invoiceNumber)
        {
            if (string.IsNullOrWhiteSpace(invoiceNumber))
            {
                throw new ArgumentException("Invoice number is required.", nameof(invoiceNumber));
            }

            return await SyncInvoicePaymentByInvoiceNumberAsync(invoiceNumber.Trim(), null).ConfigureAwait(false);
        }

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

        public async Task<HelcimTransactionAdjustmentResponse> RefundTransaction(RefundTransactionRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (request.TransactionId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(request.TransactionId), "Helcim transactionId must be a positive value.");
            }

            if (request.Amount.HasValue && request.Amount.Value <= 0m)
            {
                throw new ArgumentOutOfRangeException(nameof(request.Amount), "Refund amount must be greater than zero when provided.");
            }

            var cardTransactionLookup = await TryGetCardTransactionById(request.TransactionId, requireInvoiceNumber: false).ConfigureAwait(false);
            if (cardTransactionLookup != null)
            {
                var cardResult = await RefundCardTransactionInternal(
                    cardTransactionLookup,
                    request.Amount,
                    request.IpAddress,
                    request.IdempotencyKey).ConfigureAwait(false);
                return MapAdjustmentResponse(cardResult);
            }

            var achTransactionLookup = await TryGetAchTransactionByTransactionId(request.TransactionId).ConfigureAwait(false);
            if (achTransactionLookup != null)
            {
                var achResult = await AdjustAchTransactionFromLookup(
                    achTransactionLookup,
                    request.Amount,
                    request.IdempotencyKey).ConfigureAwait(false);
                return MapAdjustmentResponse(achResult);
            }

            throw new InvalidOperationException($"Unable to find Helcim transaction {request.TransactionId}.");
        }

        public async Task<HelcimAchRefundResponse> RefundAchTransaction(RefundAchTransactionRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (request.TransactionId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(request.TransactionId), "ACH transactionId must be a positive value.");
            }

            var achTransactionLookup = await TryGetAchTransactionByTransactionId(request.TransactionId).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"Unable to find Helcim ACH transaction {request.TransactionId}.");

            return await AdjustAchTransactionFromLookup(
                achTransactionLookup,
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

            var invoice = await GetInvoiceByInvoiceId(request.InvoiceId).ConfigureAwait(false);
            var adjustableTransaction = (await GetAchTransactionsByInvoiceAsync(invoice).ConfigureAwait(false))
                .Where(lookup => !IsAchRefundTransaction(lookup.Transaction))
                .Where(lookup => CanAdjustAchTransaction(lookup.Transaction))
                .OrderByDescending(lookup => lookup.Transaction.TransactionId)
                .FirstOrDefault();

            if (adjustableTransaction == null)
            {
                throw new InvalidOperationException($"No adjustable ACH transaction was found for invoiceId {request.InvoiceId}.");
            }

            var localTransaction = await ResolveLocalTransactionAsync(
                invoice,
                adjustableTransaction.Transaction.TransactionId).ConfigureAwait(false);

            return await AdjustAchTransactionInternal(
                adjustableTransaction,
                invoice,
                localTransaction,
                request.Amount,
                request.IdempotencyKey).ConfigureAwait(false);
        }

        public async Task<HelcimCardRefundResponse> RefundCardTransaction(RefundCardTransactionRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (request.TransactionId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(request.TransactionId), "Card transactionId must be a positive value.");
            }

            if (request.Amount.HasValue && request.Amount.Value <= 0m)
            {
                throw new ArgumentOutOfRangeException(nameof(request.Amount), "Refund amount must be greater than zero when provided.");
            }

            var cardTransactionLookup = await TryGetCardTransactionById(request.TransactionId, requireInvoiceNumber: false).ConfigureAwait(false);
            if (cardTransactionLookup == null)
            {
                var achTransactionLookup = await TryGetAchTransactionByTransactionId(request.TransactionId).ConfigureAwait(false);
                if (achTransactionLookup != null)
                {
                    throw new InvalidOperationException(
                        $"Helcim transaction {request.TransactionId} is an ACH transaction. Use the ACH refund flow instead.");
                }

                throw new InvalidOperationException($"Unable to find Helcim card transaction {request.TransactionId}.");
            }

            return await RefundCardTransactionInternal(
                cardTransactionLookup,
                request.Amount,
                request.IpAddress,
                request.IdempotencyKey).ConfigureAwait(false);
        }

        private async Task<HelcimCardRefundResponse> RefundCardTransactionInternal(
            CardTransactionLookup cardTransactionLookup,
            decimal? requestedAmount,
            string? requestIpAddress,
            string? idempotencyKeyInput)
        {
            var requestTransactionId = cardTransactionLookup.Transaction.TransactionId;

            if (cardTransactionLookup.Transaction.CardTransactionStatus != HelcimCardTransactionStatus.Approved)
            {
                throw new InvalidOperationException(
                    $"Helcim card transaction {requestTransactionId} is not approved and cannot be refunded or reversed.");
            }

            if (cardTransactionLookup.Transaction.Type != HelcimCardTransactionType.Purchase
                && cardTransactionLookup.Transaction.Type != HelcimCardTransactionType.Capture)
            {
                throw new InvalidOperationException(
                    $"Helcim card transaction {requestTransactionId} has type {cardTransactionLookup.Transaction.Type} and cannot be refunded or reversed.");
            }

            if (IsDebitCardType(cardTransactionLookup.Transaction.CardType))
            {
                throw new InvalidOperationException(
                    $"Helcim card transaction {requestTransactionId} was processed as debit (cardType DB). Debit refunds must be completed in person using Helcim payment hardware.");
            }

            var invoice = await ResolveInvoiceForCardTransactionAsync(cardTransactionLookup.Transaction).ConfigureAwait(false);
            var localTransaction = await ResolveLocalTransactionAsync(invoice, requestTransactionId).ConfigureAwait(false);

            var cardBatchId = cardTransactionLookup.Transaction.CardBatchId;
            if (cardBatchId <= 0)
            {
                throw new InvalidOperationException(
                    $"Helcim card transaction {requestTransactionId} does not have a valid card batch id.");
            }

            var cardBatch = await TryGetCardBatchById(cardBatchId).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"Unable to find Helcim card batch {cardBatchId}.");

            var idempotencyKey = string.IsNullOrWhiteSpace(idempotencyKeyInput)
                ? Guid.NewGuid().ToString()
                : idempotencyKeyInput.Trim();
            var ipAddress = ResolveCardAdjustmentIpAddress(requestIpAddress, invoice, localTransaction);
            var amount = requestedAmount ?? cardTransactionLookup.Transaction.Amount;

            string operation;
            JObject payload;
            if (cardBatch.Closed)
            {
                if (amount > cardTransactionLookup.Transaction.Amount)
                {
                    throw new InvalidOperationException("Refund amount cannot exceed the original card transaction amount.");
                }

                operation = "refund";
                payload = new JObject
                {
                    ["originalTransactionId"] = requestTransactionId,
                    ["amount"] = amount,
                    ["ipAddress"] = ipAddress
                };
            }
            else
            {
                if (requestedAmount.HasValue && requestedAmount.Value != cardTransactionLookup.Transaction.Amount)
                {
                    throw new InvalidOperationException(
                        "Open card batches can only be reversed for the full original amount. Omit amount or use the full amount.");
                }

                operation = "reverse";
                amount = cardTransactionLookup.Transaction.Amount;
                payload = new JObject
                {
                    ["cardTransactionId"] = requestTransactionId,
                    ["ipAddress"] = ipAddress
                };
            }

            var responseJson = await SendJsonRequest(
                BuildVersionedEndpoint($"/payment/{operation}"),
                HttpMethod.Post,
                payload,
                new Dictionary<string, string>
                {
                    ["idempotency-key"] = idempotencyKey
                }).ConfigureAwait(false);

            var adjustmentTransactionId = TryExtractTransactionId(responseJson);

            return new HelcimCardRefundResponse
            {
                Success = true,
                TransactionId = requestTransactionId,
                InvoiceId = invoice.InvoiceId,
                InvoiceNumber = invoice.InvoiceNumber ?? string.Empty,
                Amount = amount,
                BatchClosed = cardBatch.Closed,
                Operation = operation,
                AdjustmentTransactionId = adjustmentTransactionId,
                LocalRefundRecorded = false,
                RequiresReconciliation = true,
                IdempotencyKey = idempotencyKey,
                RawResponse = responseJson
            };
        }

        private async Task<HelcimAchRefundResponse> AdjustAchTransactionFromLookup(
            AchTransactionLookup achTransactionLookup,
            decimal? requestedAmount,
            string? idempotencyKeyInput)
        {
            var invoice = await ResolveInvoiceForAchTransactionAsync(
                achTransactionLookup.Transaction,
                achTransactionLookup.Transaction.OrderId,
                achTransactionLookup.Transaction.InvoiceNumber).ConfigureAwait(false);
            var localTransaction = await ResolveLocalTransactionAsync(
                invoice,
                achTransactionLookup.Transaction.TransactionId).ConfigureAwait(false);

            return await AdjustAchTransactionInternal(
                achTransactionLookup,
                invoice,
                localTransaction,
                requestedAmount,
                idempotencyKeyInput).ConfigureAwait(false);
        }

        private async Task<HelcimAchRefundResponse> AdjustAchTransactionInternal(
            AchTransactionLookup achTransactionLookup,
            HelcimInvoiceResponse invoice,
            StudentCourseTransactionResponse? localTransaction,
            decimal? requestedAmount,
            string? idempotencyKeyInput)
        {
            var requestTransactionId = achTransactionLookup.Transaction.TransactionId;
            var operation = DetermineAchAdjustmentOperation(achTransactionLookup.Transaction, requestTransactionId);

            var idempotencyKey = string.IsNullOrWhiteSpace(idempotencyKeyInput)
                ? Guid.NewGuid().ToString()
                : idempotencyKeyInput.Trim();

            decimal amount;
            int? refundTransactionId = null;
            var localRefundRecorded = false;
            var requiresReconciliation = false;
            var latestAchTransaction = achTransactionLookup.Transaction;
            string responseJson;

            if (operation == "refund")
            {
                amount = requestedAmount ?? achTransactionLookup.Transaction.Amount;
                if (amount <= 0m)
                {
                    throw new ArgumentOutOfRangeException(nameof(requestedAmount), "Refund amount must be greater than zero.");
                }

                if (amount > achTransactionLookup.Transaction.Amount)
                {
                    throw new InvalidOperationException("Refund amount cannot exceed the original ACH transaction amount.");
                }

                responseJson = await SendJsonRequest(
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

                refundTransactionId = TryExtractRefundTransactionId(responseJson);
            }
            else
            {
                amount = achTransactionLookup.Transaction.Amount;

                if (requestedAmount.HasValue
                    && requestedAmount.Value > 0m
                    && requestedAmount.Value != achTransactionLookup.Transaction.Amount)
                {
                    throw new InvalidOperationException(
                        $"ACH {operation} can only be applied to the full original amount. Omit amount or use the full amount.");
                }

                responseJson = await SendJsonRequest(
                    BuildVersionedEndpoint($"/ach/transactions/{requestTransactionId}/{operation}"),
                    operation == "cancel" ? HttpMethod.Patch : HttpMethod.Put,
                    null,
                    new Dictionary<string, string>
                    {
                        ["idempotency-key"] = idempotencyKey
                    }).ConfigureAwait(false);
            }

            var refreshedInvoice = await RefreshInvoiceForAchSynchronizationAsync(invoice).ConfigureAwait(false);
            await TrySyncAchTransactionsByInvoiceAsync(refreshedInvoice, localTransaction, null).ConfigureAwait(false);

            if (operation == "refund")
            {
                if (!refundTransactionId.HasValue || refundTransactionId.Value <= 0)
                {
                    var refundLookup = await TryFindRecentAchRefundTransaction(refreshedInvoice, requestTransactionId).ConfigureAwait(false);
                    if (refundLookup != null)
                    {
                        refundTransactionId = refundLookup.Transaction.TransactionId;
                    }
                }

                if (refundTransactionId.HasValue && refundTransactionId.Value > 0)
                {
                    var storedRefundTransactions = await GetStoredTransactionsByIdAsync(refundTransactionId.Value).ConfigureAwait(false);
                    localRefundRecorded = HasCompletedTransactionDetails(storedRefundTransactions);
                }

                requiresReconciliation = !localRefundRecorded;
            }
            else
            {
                var refreshedLookup = await TryGetAchTransactionByTransactionId(requestTransactionId).ConfigureAwait(false);
                if (refreshedLookup != null)
                {
                    latestAchTransaction = refreshedLookup.Transaction;
                }

                requiresReconciliation = !IsAchTerminalReversalState(latestAchTransaction);
            }

            return new HelcimAchRefundResponse
            {
                Success = true,
                TransactionId = requestTransactionId,
                InvoiceId = invoice.InvoiceId,
                InvoiceNumber = invoice.InvoiceNumber,
                Amount = amount,
                Operation = operation,
                RefundTransactionId = refundTransactionId,
                LocalRefundRecorded = localRefundRecorded,
                RequiresReconciliation = requiresReconciliation,
                IdempotencyKey = idempotencyKey,
                RawResponse = responseJson,
                StatusAuth = latestAchTransaction.StatusAuth,
                StatusClearing = latestAchTransaction.StatusClearing,
                StatusBatch = latestAchTransaction.StatusBatch
            };
        }

        private async Task<HelcimInvoiceResponse> RefreshInvoiceForAchSynchronizationAsync(HelcimInvoiceResponse invoice)
        {
            if (invoice.InvoiceId > 0)
            {
                try
                {
                    return await GetInvoiceByInvoiceId(invoice.InvoiceId).ConfigureAwait(false);
                }
                catch (InvalidOperationException)
                {
                }
                catch (JsonException)
                {
                }
            }

            if (!string.IsNullOrWhiteSpace(invoice.InvoiceNumber))
            {
                try
                {
                    return await GetInvoiceByInvoiceNumber(invoice.InvoiceNumber).ConfigureAwait(false);
                }
                catch (InvalidOperationException)
                {
                }
            }

            return invoice;
        }

        private async Task<HelcimPaymentCompletionResponse> SyncInvoicePaymentByInvoiceIdInternal(int invoiceId, string? webhookRawBody)
        {
            var invoice = await GetInvoiceByInvoiceId(invoiceId).ConfigureAwait(false);
            return await SyncInvoicePaymentInternal(invoice, webhookRawBody).ConfigureAwait(false);
        }

        private async Task<HelcimPaymentCompletionResponse> SyncInvoicePaymentInternal(
            HelcimInvoiceResponse invoice,
            string? webhookRawBody)
        {
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

            var achCompletion = await TrySyncAchTransactionsByInvoiceAsync(
                invoice,
                localTransaction,
                webhookRawBody).ConfigureAwait(false);
            if (achCompletion != null)
            {
                return achCompletion;
            }

            throw new InvalidOperationException(
                $"No Helcim payment transaction could be found for invoiceId {invoice.InvoiceId} (invoiceNumber: {invoice.InvoiceNumber}).");
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
                    var existingTransactions = await GetStoredTransactionsByIdAsync(transactionId.Value).ConfigureAwait(false);
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

        private async Task<bool> ProcessPaidInvoiceAsync(
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

        private async Task<bool> ProcessPaidInvoiceAsync(
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
                return false;
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
                return true;
            }

            return true;
        }

        public async Task<List<HelcimTransactionResponse>> GetByFamilyId(Guid familyId)
        {
            var transactions = await _repository.GetByFamilyId(familyId).ConfigureAwait(false);
            await _cardBinLookupService.EnrichAsync(transactions).ConfigureAwait(false);
            return transactions;
        }

        public async Task<List<HelcimTransactionResponse>> GetByPaymentCode(string paymentCode)
        {
            var transactions = await _repository.GetByPaymentCode(paymentCode).ConfigureAwait(false);
            await _cardBinLookupService.EnrichAsync(transactions).ConfigureAwait(false);
            return transactions;
        }

        public async Task<List<HelcimTransactionResponseDetailed>> GetDetailedByFamilyId(Guid familyId)
        {
            var transactions = await _repository.GetDetailedByFamilyId(familyId).ConfigureAwait(false);
            await _cardBinLookupService.EnrichDetailedAsync(transactions).ConfigureAwait(false);
            return transactions;
        }

        public async Task<List<HelcimTransactionResponseDetailed>> GetDetailedByPaymentCode(string paymentCode)
        {
            var transactions = await _repository.GetDetailedByPaymentCode(paymentCode).ConfigureAwait(false);
            await _cardBinLookupService.EnrichDetailedAsync(transactions).ConfigureAwait(false);
            return transactions;
        }

        public async Task<List<HelcimTransactionResponse>> GetByMaktabTransactionId(Guid maktabTransactionId)
        {
            var transactions = await _repository.GetByMaktabTransactionId(maktabTransactionId).ConfigureAwait(false);
            await _cardBinLookupService.EnrichAsync(transactions).ConfigureAwait(false);
            return transactions;
        }

        public async Task<List<HelcimTransactionResponseDetailed>> GetDetailedByMaktabTransactionId(Guid maktabTransactionId)
        {
            var transactions = await _repository.GetDetailedByMaktabTransactionId(maktabTransactionId).ConfigureAwait(false);
            await _cardBinLookupService.EnrichDetailedAsync(transactions).ConfigureAwait(false);
            return transactions;
        }

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

        private static string BuildSavedCardVerificationInvoiceNumber()
            => $"{SavedCardVerificationInvoicePrefix}{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}";

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

        private static JObject BuildSavedCardPurchasePayload(
            InitiatePaymentRequest request,
            string invoiceNumber,
            decimal amount,
            string cardToken,
            int? terminalId)
        {
            var invoice = new JObject
            {
                ["invoiceNumber"] = invoiceNumber,
                ["notes"] = request.PaymentCode,
                ["type"] = "INVOICE",
                ["lineItems"] = new JArray(new JObject
                {
                    ["sku"] = request.TransactionId.ToString(),
                    ["description"] = request.UserIp,
                    ["quantity"] = 1,
                    ["price"] = amount,
                    ["total"] = amount
                })
            };
            var payload = new JObject
            {
                ["ipAddress"] = request.UserIp,
                ["ecommerce"] = true,
                ["amount"] = amount,
                ["currency"] = "CAD",
                ["cardData"] = new JObject { ["cardToken"] = cardToken },
                ["invoice"] = invoice
            };
            if (terminalId.HasValue)
            {
                payload["terminalId"] = terminalId.Value;
            }

            return payload;
        }

        private static SavedCardPaymentAttemptResponse MapAttempt(HelcimPaymentAttemptRecord attempt)
            => new()
            {
                PaymentAttemptId = attempt.PaymentAttemptId,
                InvoiceNumber = attempt.InvoiceNumber,
                HelcimTransactionId = attempt.HelcimTransactionId,
                Status = attempt.Status switch
                {
                    HelcimPaymentAttemptStatus.Confirmed => "confirmed",
                    HelcimPaymentAttemptStatus.Declined => "declined",
                    HelcimPaymentAttemptStatus.Failed => "failed",
                    HelcimPaymentAttemptStatus.ApprovedAwaitingConfirmation => "awaiting_confirmation",
                    HelcimPaymentAttemptStatus.Submitted => "submitted",
                    _ => "created"
                },
                AcceptedByHelcim = attempt.Status is HelcimPaymentAttemptStatus.ApprovedAwaitingConfirmation or HelcimPaymentAttemptStatus.Confirmed,
                AwaitingConfirmation = attempt.Status == HelcimPaymentAttemptStatus.ApprovedAwaitingConfirmation,
                Error = attempt.FailureReason
            };

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
            => IsAchTerminalAuthorizationFailure(statusAuth)
                ? HelcimCardTransactionStatus.Declined
                : HelcimCardTransactionStatus.Approved;

        private static bool IsAchRefundTransaction(HelcimAchTransactionResponse achTransaction)
            => achTransaction.OriginalTransactionId > 0
                || string.Equals(achTransaction.LegacyType, "REFUND", StringComparison.OrdinalIgnoreCase);

        private static bool IsAchSettled(HelcimAchTransactionResponse achTransaction)
            => (int)achTransaction.StatusAuth == AchStatusAuthApproved
                && (int)achTransaction.StatusClearing == AchStatusClearingApproved
                && achTransaction.DateClosed.HasValue;

        private static bool IsAchEligibleForStorage(HelcimAchTransactionResponse achTransaction)
            => IsAchSettled(achTransaction)
                || (achTransaction.DateClosed.HasValue
                    && (IsAchTerminalAuthorizationFailure(achTransaction.StatusAuth)
                        || (int)achTransaction.StatusClearing == AchStatusClearingDeclined));

        private static bool IsAchRefundable(HelcimAchTransactionResponse achTransaction)
            => (int)achTransaction.StatusBatch == AchStatusBatchClosed
                && (int)achTransaction.StatusAuth == AchStatusAuthApproved;

        private static bool CanAdjustAchTransaction(HelcimAchTransactionResponse achTransaction)
        {
            var statusAuth = (int)achTransaction.StatusAuth;
            var statusBatch = (int)achTransaction.StatusBatch;

            return (statusBatch == AchStatusBatchClosed && statusAuth == AchStatusAuthApproved)
                || (statusBatch == AchStatusBatchOpen && statusAuth == AchStatusAuthApproved)
                || (statusBatch == AchStatusBatchOpen && statusAuth == AchStatusAuthPending);
        }

        private static bool IsAchTerminalAuthorizationFailure(HelcimAchAuthorizationStatus statusAuth)
        {
            var value = (int)statusAuth;
            return value == AchStatusAuthDeclined || value == AchStatusAuthCancelled;
        }

        private static bool IsAchTerminalReversalState(HelcimAchTransactionResponse achTransaction)
            => IsAchTerminalAuthorizationFailure(achTransaction.StatusAuth)
                || (int)achTransaction.StatusClearing == AchStatusClearingDeclined;

        private static string DetermineAchAdjustmentOperation(HelcimAchTransactionResponse achTransaction, int transactionId)
        {
            if (IsAchRefundTransaction(achTransaction))
            {
                throw new InvalidOperationException(
                    $"Helcim ACH transaction {transactionId} is already a refund transaction and cannot be adjusted again.");
            }

            var statusAuth = (int)achTransaction.StatusAuth;
            var statusBatch = (int)achTransaction.StatusBatch;

            if (statusAuth == AchStatusAuthDeclined)
            {
                throw new InvalidOperationException(
                    $"Helcim ACH transaction {transactionId} was declined and cannot be refunded, voided, or cancelled.");
            }

            if (statusAuth == AchStatusAuthCancelled)
            {
                throw new InvalidOperationException(
                    $"Helcim ACH transaction {transactionId} is already voided/cancelled and cannot be adjusted again.");
            }

            if (statusBatch == AchStatusBatchClosed && statusAuth == AchStatusAuthApproved)
            {
                return "refund";
            }

            if (statusBatch == AchStatusBatchOpen && statusAuth == AchStatusAuthApproved)
            {
                return "void";
            }

            if (statusBatch == AchStatusBatchOpen && statusAuth == AchStatusAuthPending)
            {
                return "cancel";
            }

            throw new InvalidOperationException(
                $"Helcim ACH transaction {transactionId} is not in an adjustable state. statusAuth={achTransaction.StatusAuth}, statusClearing={achTransaction.StatusClearing}, statusBatch={achTransaction.StatusBatch}.");
        }

        private static bool ShouldApplyCardPayment(HelcimInvoiceResponse invoice, HelcimCardTransactionResponse cardTransaction)
            => cardTransaction.CardTransactionStatus == HelcimCardTransactionStatus.Approved
                && ShouldApplyInvoicePayment(invoice, MapPaymentType(cardTransaction.Type));

        private static bool ShouldApplyAchPayment(HelcimInvoiceResponse invoice, HelcimAchTransactionResponse achTransaction)
            => IsAchSettled(achTransaction);

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

            var storedTransactions = await GetStoredTransactionsByIdAsync(helcimTransactionId).ConfigureAwait(false);
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
            var invoice = await ResolveInvoiceByReferenceAsync(invoiceNumber).ConfigureAwait(false);
            return await SyncInvoicePaymentInternal(invoice, webhookRawBody).ConfigureAwait(false);
        }

        private async Task<HelcimInvoiceResponse> ResolveInvoiceByReferenceAsync(string invoiceReference)
        {
            var directInvoice = await TryGetInvoiceByInvoiceNumber(invoiceReference).ConfigureAwait(false);
            if (directInvoice != null)
            {
                return directInvoice;
            }

            var invoiceFromPaymentCode = await TryResolveInvoiceByPaymentCodeAsync(invoiceReference).ConfigureAwait(false);
            if (invoiceFromPaymentCode != null)
            {
                return invoiceFromPaymentCode;
            }

            throw new InvalidOperationException(
                $"No Helcim invoice could be found for reference {invoiceReference}. The value must be a valid invoice id, invoice number, or payment code.");
        }

        private async Task<HelcimInvoiceResponse?> TryGetInvoiceByInvoiceNumber(string invoiceNumber)
        {
            try
            {
                return await GetInvoiceByInvoiceNumber(invoiceNumber).ConfigureAwait(false);
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        private async Task<HelcimInvoiceResponse?> TryResolveInvoiceByPaymentCodeAsync(string paymentCode)
        {
            if (string.IsNullOrWhiteSpace(paymentCode))
            {
                return null;
            }

            var normalizedPaymentCode = paymentCode.Trim();
            var storedInvoiceNumber = await TryResolveStoredInvoiceNumberByPaymentCodeAsync(normalizedPaymentCode).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(storedInvoiceNumber))
            {
                return await GetInvoiceByInvoiceNumber(storedInvoiceNumber).ConfigureAwait(false);
            }

            var localTransaction = await _studentCourseTransactionService
                .GetTransactionByPaymentCode(normalizedPaymentCode)
                .ConfigureAwait(false);
            if (localTransaction == null)
            {
                return null;
            }

            var matchingInvoiceNumber = await TryResolveLiveInvoiceNumberByPaymentCodeAsync(
                normalizedPaymentCode,
                localTransaction.CreatedAt).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(matchingInvoiceNumber))
            {
                return null;
            }

            return await GetInvoiceByInvoiceNumber(matchingInvoiceNumber).ConfigureAwait(false);
        }

        private async Task<string?> TryResolveStoredInvoiceNumberByPaymentCodeAsync(string paymentCode)
        {
            var storedTransactions = await _repository.GetByPaymentCode(paymentCode).ConfigureAwait(false)
                ?? new List<HelcimTransactionResponse>();

            return storedTransactions
                .Where(transaction => MatchesPaymentCodeInvoiceNumber(transaction.InvoiceNumber, paymentCode))
                .OrderByDescending(transaction => transaction.TransactionId)
                .ThenByDescending(transaction => transaction.CreatedAt ?? DateTime.MinValue)
                .Select(transaction => transaction.InvoiceNumber)
                .FirstOrDefault(invoiceNumber => !string.IsNullOrWhiteSpace(invoiceNumber));
        }

        private async Task<string?> TryResolveLiveInvoiceNumberByPaymentCodeAsync(string paymentCode, DateTime transactionCreatedAt)
        {
            var startDate = ResolvePaymentCodeSearchStartDate(transactionCreatedAt);
            var endDate = DateTime.UtcNow.Date;

            var cardTransactions = await GetCardTransactionsForReconciliation(startDate, endDate).ConfigureAwait(false);
            var latestCardTransaction = cardTransactions
                .Where(transaction => MatchesPaymentCodeInvoiceNumber(transaction.InvoiceNumber, paymentCode))
                .OrderByDescending(transaction => transaction.TransactionId)
                .ThenByDescending(transaction => transaction.DateCreated ?? DateTime.MinValue)
                .FirstOrDefault();

            var achTransactions = await GetAchTransactionsForReconciliation(startDate, endDate).ConfigureAwait(false);
            var latestAchTransaction = achTransactions
                .Where(transaction => MatchesPaymentCodeInvoiceNumber(
                    transaction.Transaction.InvoiceNumber ?? transaction.Transaction.OrderNumber,
                    paymentCode))
                .OrderByDescending(transaction => transaction.Transaction.TransactionId)
                .ThenByDescending(transaction => transaction.Transaction.DateCreated ?? DateTime.MinValue)
                .FirstOrDefault();

            if (latestCardTransaction == null && latestAchTransaction == null)
            {
                return null;
            }

            if (latestCardTransaction != null
                && (latestAchTransaction == null
                    || latestCardTransaction.TransactionId >= latestAchTransaction.Transaction.TransactionId))
            {
                return latestCardTransaction.InvoiceNumber;
            }

            return latestAchTransaction?.Transaction.InvoiceNumber
                ?? latestAchTransaction?.Transaction.OrderNumber;
        }

        private static DateTime ResolvePaymentCodeSearchStartDate(DateTime transactionCreatedAt)
        {
            if (transactionCreatedAt == default)
            {
                return DateTime.UtcNow.Date.AddDays(-90);
            }

            return transactionCreatedAt.Kind == DateTimeKind.Utc
                ? transactionCreatedAt.Date.AddDays(-1)
                : transactionCreatedAt.ToUniversalTime().Date.AddDays(-1);
        }

        private static bool MatchesPaymentCodeInvoiceNumber(string? invoiceNumber, string paymentCode)
        {
            if (string.IsNullOrWhiteSpace(invoiceNumber) || string.IsNullOrWhiteSpace(paymentCode))
            {
                return false;
            }

            var normalizedPaymentCode = paymentCode.Trim();
            return invoiceNumber.StartsWith($"INV-{normalizedPaymentCode}-", StringComparison.OrdinalIgnoreCase)
                || string.Equals(invoiceNumber, normalizedPaymentCode, StringComparison.OrdinalIgnoreCase);
        }

        private async Task<CardTransactionLookup?> TryGetCardTransactionById(int transactionId, bool requireInvoiceNumber = true)
        {
            try
            {
                var cardTransactionRaw = await SendGetRequest(
                    BuildVersionedEndpoint($"/card-transactions/{transactionId}"),
                    allowEmptyResponse: true).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(cardTransactionRaw))
                {
                    return null;
                }

                var cardTransactionToken = JToken.Parse(cardTransactionRaw);
                var cardTransactionData = cardTransactionToken["transaction"] as JObject ?? cardTransactionToken as JObject;
                if (cardTransactionData == null || IsAchTransactionResponse(cardTransactionData))
                {
                    return null;
                }

                var cardTransaction = cardTransactionData.ToObject<HelcimCardTransactionResponse>();
                if (cardTransaction == null
                    || (requireInvoiceNumber && string.IsNullOrWhiteSpace(cardTransaction.InvoiceNumber)))
                {
                    return null;
                }

                return new CardTransactionLookup
                {
                    Transaction = cardTransaction,
                    TransactionRaw = cardTransactionRaw
                };
            }
            catch (HelcimRequestException ex) when (IsLookupMiss(ex))
            {
                return null;
            }
            catch (JsonException)
            {
                return null;
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        private async Task<CardTransactionLookup?> TryGetCardTransactionByInvoiceNumber(string invoiceNumber)
        {
            var cardTransactions = await GetCardTransactionsByInvoiceNumber(invoiceNumber).ConfigureAwait(false);
            var cardTransaction = cardTransactions
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

        private async Task<List<HelcimCardTransactionResponse>> GetCardTransactionsByInvoiceNumber(string invoiceNumber)
        {
            var cardTransactionEndpoint = BuildVersionedEndpoint($"/card-transactions?invoiceNumber={Uri.EscapeDataString(invoiceNumber)}");
            var cardTransactionRaw = await SendGetRequest(cardTransactionEndpoint).ConfigureAwait(false);
            return JsonConvert.DeserializeObject<List<HelcimCardTransactionResponse>>(cardTransactionRaw)?
                .Where(transaction => string.Equals(transaction.InvoiceNumber, invoiceNumber, StringComparison.OrdinalIgnoreCase))
                .ToList()
                ?? new List<HelcimCardTransactionResponse>();
        }

        private async Task<HelcimInvoiceResponse> ResolveInvoiceForCardTransactionAsync(HelcimCardTransactionResponse cardTransaction)
        {
            if (!string.IsNullOrWhiteSpace(cardTransaction.InvoiceNumber))
            {
                return await GetInvoiceByInvoiceNumber(cardTransaction.InvoiceNumber).ConfigureAwait(false);
            }

            var existingDetailedTransaction = await GetExistingDetailedTransactionAsync(cardTransaction.TransactionId).ConfigureAwait(false);
            if (existingDetailedTransaction?.InvoiceId > 0)
            {
                return await GetInvoiceByInvoiceId(existingDetailedTransaction.InvoiceId).ConfigureAwait(false);
            }

            if (!string.IsNullOrWhiteSpace(existingDetailedTransaction?.InvoiceNumber))
            {
                return await GetInvoiceByInvoiceNumber(existingDetailedTransaction.InvoiceNumber).ConfigureAwait(false);
            }

            throw new InvalidOperationException(
                $"Unable to resolve a Helcim invoice for card transaction {cardTransaction.TransactionId}.");
        }

        private async Task<CardBatchLookup?> TryGetCardBatchById(int cardBatchId)
        {
            try
            {
                var cardBatchRaw = await SendGetRequest(BuildVersionedEndpoint($"/card-batches/{cardBatchId}")).ConfigureAwait(false);
                var token = JToken.Parse(cardBatchRaw);
                var batchToken = token["cardBatch"] ?? token["batch"] ?? token;
                var closed = batchToken.Value<bool?>("closed");
                if (!closed.HasValue)
                {
                    return null;
                }

                return new CardBatchLookup
                {
                    CardBatchId = batchToken.Value<int?>("id")
                        ?? batchToken.Value<int?>("cardBatchId")
                        ?? cardBatchId,
                    Closed = closed.Value,
                    Raw = cardBatchRaw
                };
            }
            catch
            {
                return null;
            }
        }

        private async Task<CardTransactionLookup?> TryResolveCardAdjustmentTransactionAsync(
            HelcimInvoiceResponse invoice,
            int originalTransactionId,
            HelcimCardTransactionType expectedType,
            int? adjustmentTransactionId)
        {
            if (adjustmentTransactionId.HasValue && adjustmentTransactionId.Value > 0)
            {
                var byId = await TryGetCardTransactionById(adjustmentTransactionId.Value, requireInvoiceNumber: false).ConfigureAwait(false);
                if (byId != null && byId.Transaction.Type == expectedType)
                {
                    return byId;
                }
            }

            if (string.IsNullOrWhiteSpace(invoice.InvoiceNumber))
            {
                return null;
            }

            var transactions = await GetCardTransactionsByInvoiceNumber(invoice.InvoiceNumber).ConfigureAwait(false);
            var adjustment = transactions
                .Where(transaction => transaction.TransactionId != originalTransactionId)
                .Where(transaction => transaction.Type == expectedType)
                .OrderByDescending(transaction => transaction.TransactionId)
                .FirstOrDefault();

            if (adjustment == null)
            {
                return null;
            }

            return new CardTransactionLookup
            {
                Transaction = adjustment,
                TransactionRaw = JsonConvert.SerializeObject(adjustment)
            };
        }

        private async Task<AchTransactionLookup?> TryGetAchTransactionByTransactionId(int transactionId)
        {
            try
            {
                var achTransactionRaw = await SendGetRequest(
                    BuildVersionedEndpoint($"/ach/transactions/{transactionId}"),
                    allowEmptyResponse: true).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(achTransactionRaw))
                {
                    return null;
                }

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
            catch (HelcimRequestException ex) when (IsLookupMiss(ex))
            {
                return null;
            }
            catch (JsonException)
            {
                return null;
            }
            catch (InvalidOperationException)
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

        private async Task<HelcimPaymentCompletionResponse?> TrySyncAchTransactionsByInvoiceAsync(
            HelcimInvoiceResponse invoice,
            StudentCourseTransactionResponse? localTransaction,
            string? webhookRawBody)
        {
            var matchedTransactions = await GetAchTransactionsByInvoiceAsync(invoice).ConfigureAwait(false);
            if (matchedTransactions.Count == 0)
            {
                return null;
            }

            var orderedTransactions = matchedTransactions
                .OrderBy(lookup => lookup.Transaction.DateCreated ?? lookup.Transaction.DateClosed ?? DateTime.MinValue)
                .ThenBy(lookup => IsAchRefundTransaction(lookup.Transaction) ? 1 : 0)
                .ThenBy(lookup => lookup.Transaction.TransactionId)
                .ToList();

            HelcimPaymentCompletionResponse? lastCompletion = null;
            var anyStored = false;

            foreach (var transactionLookup in orderedTransactions)
            {
                var detailedLookup = await TryGetAchTransactionByTransactionId(transactionLookup.Transaction.TransactionId).ConfigureAwait(false)
                    ?? transactionLookup;

                var completion = await SaveAchTransactionDetailsAsync(
                    invoice,
                    localTransaction,
                    detailedLookup.TransactionData,
                    detailedLookup.TransactionRaw,
                    detailedLookup.Transaction,
                    webhookRawBody).ConfigureAwait(false);

                anyStored |= !completion.Duplicate;
                lastCompletion = completion;
            }

            if (lastCompletion == null)
            {
                return null;
            }

            if (anyStored || !lastCompletion.Duplicate)
            {
                return lastCompletion;
            }

            return new HelcimPaymentCompletionResponse
            {
                Success = lastCompletion.Success,
                Duplicate = true,
                InvoiceId = lastCompletion.InvoiceId,
                TransactionId = lastCompletion.TransactionId,
                InvoiceNumber = lastCompletion.InvoiceNumber,
                PaymentFlow = lastCompletion.PaymentFlow
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
            => TryExtractTransactionId(responseJson);

        private static int? TryExtractTransactionId(string responseJson)
        {
            try
            {
                var token = JToken.Parse(responseJson);
                return token["transaction"]?.Value<int?>("id")
                    ?? token.Value<int?>("id")
                    ?? token["transaction"]?.Value<int?>("transactionId")
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
            var existingTransactions = await GetStoredTransactionsByIdAsync(cardTransaction.TransactionId).ConfigureAwait(false);

            // A transaction detail may have been stored before its local payment was applied
            // (for example, a legacy reversal). Course-payment insertion is idempotent by
            // Helcim external transaction id, so safely reconcile the ledger first.
            var localPaymentApplied = false;
            if (!IsProfileSavedCardVerification(invoice.InvoiceNumber)
                && ShouldApplyCardPayment(invoice, cardTransaction))
            {
                localPaymentApplied = await ProcessPaidInvoiceAsync(invoice, cardTransaction, localTransaction).ConfigureAwait(false);
            }

            if (localPaymentApplied && _paymentAttempts != null && !string.IsNullOrWhiteSpace(invoice.InvoiceNumber))
            {
                var attempt = await _paymentAttempts.GetByInvoiceNumber(invoice.InvoiceNumber).ConfigureAwait(false);
                if (attempt != null && attempt.Status == HelcimPaymentAttemptStatus.ApprovedAwaitingConfirmation)
                {
                    await _paymentAttempts.UpdateResult(
                        attempt.PaymentAttemptId,
                        HelcimPaymentAttemptStatus.Confirmed,
                        cardTransaction.TransactionId,
                        null).ConfigureAwait(false);
                }
            }

            await SaveCardWhenCheckoutOptedInAsync(invoice, cardTransaction).ConfigureAwait(false);

            if (HasCompletedTransactionDetails(existingTransactions))
            {
                return CreatePaymentCompletionResponse(invoice, cardTransaction.TransactionId, "card", duplicate: true);
            }

            var existingDetailedTransaction = await GetExistingDetailedTransactionAsync(cardTransaction.TransactionId).ConfigureAwait(false);
            var transactionDetails = MapCardTransactionDetails(
                invoice,
                localTransaction,
                cardTransactionRaw,
                cardTransaction,
                webhookRawBody,
                existingDetailedTransaction);

            var duplicateDetected = await SaveTransactionDetailsAsync(transactionDetails, existingTransactions.Any()).ConfigureAwait(false);
            if (duplicateDetected)
            {
                return CreatePaymentCompletionResponse(invoice, cardTransaction.TransactionId, "card", duplicate: true);
            }

            return CreatePaymentCompletionResponse(invoice, cardTransaction.TransactionId, "card", duplicate: false);
        }

        private async Task SaveCardWhenCheckoutOptedInAsync(
            HelcimInvoiceResponse invoice,
            HelcimCardTransactionResponse cardTransaction)
        {
            if (string.IsNullOrWhiteSpace(invoice.InvoiceNumber)
                || string.IsNullOrWhiteSpace(cardTransaction.CardToken))
            {
                return;
            }

            var context = _checkoutContexts == null
                ? null
                : await _checkoutContexts.Get(invoice.InvoiceNumber).ConfigureAwait(false);
            if (context is not { SaveCardInfo: true } || context.UserId == Guid.Empty)
            {
                return;
            }

            var isProfileVerification = IsProfileSavedCardVerification(invoice.InvoiceNumber);
            if (isProfileVerification)
            {
                if (cardTransaction.CardTransactionStatus != HelcimCardTransactionStatus.Approved)
                {
                    return;
                }
            }
            else if (!ShouldApplyCardPayment(invoice, cardTransaction))
            {
                return;
            }

            if (_cardVault == null || _cardTokenProtector == null)
            {
                throw new InvalidOperationException("Saved-card storage is not configured.");
            }

            var token = _cardTokenProtector.Protect(cardTransaction.CardToken);
            var details = await _cardBinLookupService
                .GetDisplayDetailsAsync(cardTransaction.CardType, cardTransaction.CardNumber)
                .ConfigureAwait(false);
            var cardNumberDigits = new string((cardTransaction.CardNumber ?? string.Empty).Where(char.IsDigit).ToArray());

            await _cardVault.AddIfMissing(new HelcimSavedCardRecord
            {
                CardId = Guid.NewGuid(),
                UserId = context.UserId,
                FamilyId = context.FamilyId,
                TokenCiphertext = token.Ciphertext,
                TokenNonce = token.Nonce,
                TokenTag = token.Tag,
                TokenHash = token.Hash,
                CardCompany = details.CardCompany,
                CardFundingType = details.CardFundingType,
                LastFourDigits = cardNumberDigits.Length >= 4 ? cardNumberDigits[^4..] : cardNumberDigits,
                CardHolderName = cardTransaction.CardHolderName ?? string.Empty,
                SourceHelcimTransactionId = cardTransaction.TransactionId
            }).ConfigureAwait(false);
        }

        private static bool IsProfileSavedCardVerification(string? invoiceNumber)
            => !string.IsNullOrWhiteSpace(invoiceNumber)
                && invoiceNumber.StartsWith(SavedCardVerificationInvoicePrefix, StringComparison.OrdinalIgnoreCase);

        private async Task<HelcimPaymentCompletionResponse> SaveAchTransactionDetailsAsync(
            HelcimInvoiceResponse invoice,
            StudentCourseTransactionResponse? localTransaction,
            JObject transactionData,
            string achTransactionRaw,
            HelcimAchTransactionResponse achTransaction,
            string? webhookRawBody)
        {
            var existingTransactions = await GetStoredTransactionsByIdAsync(achTransaction.TransactionId).ConfigureAwait(false);

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

            var duplicateDetected = await SaveTransactionDetailsAsync(transactionDetails, existingTransactions.Any()).ConfigureAwait(false);
            if (duplicateDetected)
            {
                return CreatePaymentCompletionResponse(invoice, achTransaction.TransactionId, "ach", duplicate: true);
            }

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
            var existingTransactions = await GetStoredTransactionsByIdAsync(transactionId).ConfigureAwait(false);
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

        private async Task<List<HelcimTransactionResponse>> GetStoredTransactionsByIdAsync(int transactionId)
        {
            var getTransactionsTask = _repository.GetByTransactionId(transactionId);
            if (getTransactionsTask == null)
            {
                return new List<HelcimTransactionResponse>();
            }

            return await getTransactionsTask.ConfigureAwait(false) ?? new List<HelcimTransactionResponse>();
        }

        private async Task<HelcimTransactionResponseDetailed?> GetExistingDetailedTransactionAsync(int transactionId)
        {
            var getDetailedTask = _repository.GetDetailedByTransactionId(transactionId);
            if (getDetailedTask == null)
            {
                return null;
            }

            var transactions = await getDetailedTask.ConfigureAwait(false);
            return transactions?.FirstOrDefault();
        }

        private async Task<bool> SaveTransactionDetailsAsync(AddHelcimTransactionDetails transactionDetails, bool updateExisting)
        {
            if (updateExisting)
            {
                await _repository.Update(transactionDetails).ConfigureAwait(false);
                return false;
            }

            try
            {
                await _repository.Add(transactionDetails).ConfigureAwait(false);
                return false;
            }
            catch (Exception ex) when (LooksLikeDuplicateTransactionInsert(ex))
            {
                var existingTransactions = await GetStoredTransactionsByIdAsync(transactionDetails.TransactionId).ConfigureAwait(false);
                if (HasCompletedTransactionDetails(existingTransactions))
                {
                    return true;
                }

                throw;
            }
        }

        private static bool HasCompletedTransactionDetails(IEnumerable<HelcimTransactionResponse> existingTransactions)
            => existingTransactions.Any(transaction => !IsPlaceholderTransaction(transaction));

        private static bool LooksLikeDuplicateTransactionInsert(Exception exception)
        {
            var current = exception;
            while (current != null)
            {
                var message = current.Message ?? string.Empty;
                if (message.Contains("Duplicate", StringComparison.OrdinalIgnoreCase)
                    || message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase)
                    || message.Contains("PRIMARY", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                current = current.InnerException!;
            }

            return false;
        }

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
                || cardTransactionType == HelcimCardTransactionType.Reverse
                ? PaymentType.Refund
                : PaymentType.Credit;

        private static bool IsDebitCardType(string? cardType)
            => string.Equals(cardType?.Trim(), "DB", StringComparison.OrdinalIgnoreCase);

        private static HelcimTransactionAdjustmentResponse MapAdjustmentResponse(HelcimCardRefundResponse response)
            => new()
            {
                Success = response.Success,
                PaymentFlow = "card",
                TransactionId = response.TransactionId,
                InvoiceId = response.InvoiceId,
                InvoiceNumber = response.InvoiceNumber,
                Amount = response.Amount,
                Operation = response.Operation,
                AdjustmentTransactionId = response.AdjustmentTransactionId,
                BatchClosed = response.BatchClosed,
                LocalAdjustmentRecorded = response.LocalRefundRecorded,
                RequiresReconciliation = response.RequiresReconciliation,
                IdempotencyKey = response.IdempotencyKey,
                RawResponse = response.RawResponse
            };

        private static HelcimTransactionAdjustmentResponse MapAdjustmentResponse(HelcimAchRefundResponse response)
            => new()
            {
                Success = response.Success,
                PaymentFlow = "ach",
                TransactionId = response.TransactionId,
                InvoiceId = response.InvoiceId,
                InvoiceNumber = response.InvoiceNumber,
                Amount = response.Amount,
                Operation = response.Operation,
                AdjustmentTransactionId = response.RefundTransactionId,
                LocalAdjustmentRecorded = response.LocalRefundRecorded,
                RequiresReconciliation = response.RequiresReconciliation,
                IdempotencyKey = response.IdempotencyKey,
                RawResponse = response.RawResponse,
                StatusAuth = response.StatusAuth,
                StatusClearing = response.StatusClearing,
                StatusBatch = response.StatusBatch
            };

        private static string ResolveCardAdjustmentIpAddress(
            string? requestIpAddress,
            HelcimInvoiceResponse invoice,
            StudentCourseTransactionResponse? localTransaction)
        {
            if (!string.IsNullOrWhiteSpace(requestIpAddress))
            {
                return requestIpAddress.Trim();
            }

            var invoiceIpAddress = invoice.LineItems?.FirstOrDefault()?.UserIp;
            if (!string.IsNullOrWhiteSpace(invoiceIpAddress))
            {
                return invoiceIpAddress.Trim();
            }

            throw new InvalidOperationException(
                $"A customer IP address is required to refund or reverse Helcim card transaction {localTransaction?.PaymentCode ?? invoice.InvoiceNumber ?? "unknown"}.");
        }

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

        private static bool IsLookupMiss(HelcimRequestException ex)
            => !ex.IsUpstreamFailure
                && (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase)
                    || ex.Message.Contains("returned no results", StringComparison.OrdinalIgnoreCase));

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

                throw new HelcimRequestException(
                    $"Helcim {method} request failed or returned an empty response for endpoint {endpoint}.",
                    isUpstreamFailure: true);
            }

            ThrowIfHelcimErrorResponse(responseJson, method, endpoint);
            return responseJson;
        }

        private static void ThrowIfHelcimErrorResponse(string responseJson, HttpMethod method, string endpoint)
        {
            try
            {
                var token = JToken.Parse(responseJson);
                if (token.Type != JTokenType.Object)
                {
                    return;
                }

                var obj = (JObject)token;
                var status = obj.Value<string>("status");
                if (!string.Equals(status, "error", StringComparison.OrdinalIgnoreCase) && obj["errors"] == null)
                {
                    return;
                }

                throw new HelcimRequestException(
                    BuildHelcimErrorMessage(obj, method, endpoint),
                    isUpstreamFailure: false,
                    rawResponse: responseJson);
            }
            catch (JsonReaderException)
            {
                // Non-JSON responses are handled by the caller as opaque success payloads.
            }
        }

        private static string BuildHelcimErrorMessage(JObject errorObject, HttpMethod method, string endpoint)
        {
            var messages = new List<string>();

            var topLevelMessage = errorObject.Value<string>("message");
            if (!string.IsNullOrWhiteSpace(topLevelMessage))
            {
                messages.Add(topLevelMessage.Trim());
            }

            if (errorObject["errors"] is JObject errorsObject)
            {
                foreach (var errorGroup in errorsObject.Properties())
                {
                    if (errorGroup.Value is not JArray items)
                    {
                        continue;
                    }

                    foreach (var item in items.OfType<JObject>())
                    {
                        var message = item.Value<string>("message");
                        var source = item.Value<string>("source");
                        var data = item.Value<string>("data");

                        var parts = new List<string>();
                        if (!string.IsNullOrWhiteSpace(message))
                        {
                            parts.Add(message.Trim());
                        }

                        if (!string.IsNullOrWhiteSpace(source))
                        {
                            parts.Add($"source: {source.Trim()}");
                        }

                        if (!string.IsNullOrWhiteSpace(data))
                        {
                            parts.Add($"value: {data.Trim()}");
                        }

                        if (parts.Count > 0)
                        {
                            messages.Add(string.Join(", ", parts));
                        }
                    }
                }
            }

            if (messages.Count == 0)
            {
                messages.Add("Helcim reported that the request failed.");
            }

            return $"Helcim {method} request failed for endpoint {endpoint}. {string.Join(" | ", messages.Distinct())}";
        }

        private sealed class CardTransactionLookup
        {
            public HelcimCardTransactionResponse Transaction { get; set; } = null!;
            public string TransactionRaw { get; set; } = string.Empty;
        }

        private sealed class CardBatchLookup
        {
            public int CardBatchId { get; set; }
            public bool Closed { get; set; }
            public string Raw { get; set; } = string.Empty;
        }

        private sealed class AchTransactionLookup
        {
            public HelcimAchTransactionResponse Transaction { get; set; } = null!;
            public JObject TransactionData { get; set; } = null!;
            public string TransactionRaw { get; set; } = string.Empty;
        }
    }
}
