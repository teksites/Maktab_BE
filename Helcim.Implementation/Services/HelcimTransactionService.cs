using System.Text;
using Helcim.Configuration;
using Helcim.Repository;
using Helcim.Services;
using InternalContracts;
using MaktabDataContracts.Enums.Helcim;
using MaktabDataContracts.Requests.Helcim;
using MaktabDataContracts.Responses.Helcim;
using Newtonsoft.Json;
using WebMsgSender;

namespace Helcim.Implementation.Services
{
    public class HelcimTransactionService : IHelcimTransactionService
    {
        private readonly IHelcimTransactionRepository _repository;
        private readonly IHelcimClientConfiguration _clientConfiguration;
        private readonly IWebMsgSenderService _senderService;

        public HelcimTransactionService(
            IHelcimTransactionRepository repository,
            IHelcimClientConfiguration clientConfiguration,
            IWebMsgSenderService senderService)
        {
            _repository = repository;
            _clientConfiguration = clientConfiguration;
            _senderService = senderService;
        }

        public async Task<HelcimPayInitializeResponse> InitializePayment(InitiatePaymentRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            var invoiceNumber = await BuildInvoiceNumber(request).ConfigureAwait(false);
            var amount = Convert.ToDecimal(request.Amount);

            var helcimRequest = new HelcimPayInitializeRequest
            {
                InvoiceRequest = new HelcimInvoiceRequest
                {
                    InvoiceNumber = invoiceNumber,
                    PaymentCode = request.PaymentCode,
                    Type = HelcimInvoiceType.Invoice,
                    LineItems = new List<HelcimInvoiceLineItemRequest>
                    {
                        new HelcimInvoiceLineItemRequest
                        {
                            MaktabTransactionId = request.TransactionId,
                            UserIp = "payment",
                            Quantity = 1m,
                            Price = amount,
                            Total = amount
                        }
                    }
                },
                PaymentType = HelcimPaymentType.Purchase,
                Amount = amount,
                Currency = HelcimCurrency.Cad,
                PaymentMethod = HelcimPaymentMethod.CreditCardOrAch
            };

            var payload = new JsonMessageData
            {
                ExternalEndpoint = _clientConfiguration.BaseUrl + _clientConfiguration.RelativeUrl,
                Payload = new StringContent(
                    JsonConvert.SerializeObject(helcimRequest),
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

            var response = JsonConvert.DeserializeObject<HelcimPayInitializeResponse>(responseJson);
            if (response == null)
            {
                throw new InvalidOperationException($"Unable to deserialize Helcim initialize payment response. Raw response: {responseJson}");
            }

            return response;
        }

        public Task AddTransactionDetails(AddHelcimTransactionDetails transactionDetails)
            => _repository.Add(transactionDetails);

        public async Task HandleWebhook(HelcimCardTransactionWebhookResponse webhook)
        {
            ArgumentNullException.ThrowIfNull(webhook);

            if (webhook.Type != HelcimWebhookEventType.CardTransaction)
            {
                return;
            }

            var existingTransactions = await _repository.GetByTransactionId(webhook.Id).ConfigureAwait(false);
            if (existingTransactions.Any())
            {
                return;
            }

            var cardTransactionEndpoint = $"{_clientConfiguration.BaseUrl}/v2/card-transactions/{webhook.Id}";
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

            var invoiceEndpoint = $"{_clientConfiguration.BaseUrl}/v2/invoices/?invoiceNumber={Uri.EscapeDataString(cardTransaction.InvoiceNumber)}";
            var invoiceRaw = await SendGetRequest(invoiceEndpoint).ConfigureAwait(false);
            var invoiceResponses = JsonConvert.DeserializeObject<List<HelcimInvoiceResponse>>(invoiceRaw);
            var invoice = invoiceResponses?.FirstOrDefault();

            if (invoice == null)
            {
                throw new InvalidOperationException($"Helcim invoice lookup for invoice number {cardTransaction.InvoiceNumber} returned no results.");
            }

            var lineItem = invoice.LineItems?.FirstOrDefault();
            var rawResponse = JsonConvert.SerializeObject(new
            {
                WebhookRequest = JsonConvert.SerializeObject(webhook),
                CardTransactionResponse = cardTransactionRaw,
                InvoiceResponse = invoiceRaw
            });

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
                RawResponse = rawResponse
            };

            await _repository.Add(transactionDetails).ConfigureAwait(false);
        }

        public Task<List<HelcimTransactionResponse>> GetByPaymentCode(string paymentCode)
            => _repository.GetByPaymentCode(paymentCode);

        public Task<List<HelcimTransactionResponse>> GetByMaktabTransactionId(Guid maktabTransactionId)
            => _repository.GetByMaktabTransactionId(maktabTransactionId);

        private async Task<string> BuildInvoiceNumber(InitiatePaymentRequest request)
        {
            var existingTransactions = new List<HelcimTransactionResponse>();
            var normalizedPaymentCode = string.IsNullOrWhiteSpace(request.PaymentCode)
                ? "NOPAYMENTCODE"
                : request.PaymentCode.Trim();
            var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");

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
