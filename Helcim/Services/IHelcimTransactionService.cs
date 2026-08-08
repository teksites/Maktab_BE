using MaktabDataContracts.Requests.Helcim;
using MaktabDataContracts.Responses.Helcim;

namespace Helcim.Services
{
    public interface IHelcimTransactionService
    {
        Task<HelcimPayInitializeResponse> InitializePayment(InitiatePaymentRequest request);
        Task<HelcimPaymentCompletionResponse> CompleteHelcimPayPayment(CompleteHelcimPayPaymentRequest request);
        Task<HelcimPaymentCompletionResponse> SyncInvoicePaymentByInvoiceId(int invoiceId);
        Task<HelcimReconciliationResponse> ReconcileTransactions(HelcimReconciliationRequest? request = null);
        Task<IReadOnlyList<HelcimAchRefundInvoiceSummaryResponse>> GetAchRefundInvoices(GetAchRefundInvoicesRequest request);
        Task<HelcimAchRefundResponse> RefundAchTransaction(RefundAchTransactionRequest request);
        Task<HelcimAchRefundResponse> RefundAchInvoice(RefundAchInvoiceRequest request);
        Task<HelcimWebhookHandlingStatus> HandleWebhook(
            string rawBody,
            string? webhookId,
            string? webhookTimestamp,
            string? signatureHeader);
        Task<HelcimWebhookHandlingStatus> HandleWebhook(
            HelcimCardTransactionWebhookResponse webhook,
            string rawBody,
            string? webhookId,
            string? webhookTimestamp,
            string? signatureHeader);
        Task AddTransactionDetails(AddHelcimTransactionDetails transactionDetails);
        Task<List<HelcimTransactionResponse>> GetByFamilyId(Guid familyId);
        Task<List<HelcimTransactionResponse>> GetByPaymentCode(string paymentCode);
        Task<List<HelcimTransactionResponseDetailed>> GetDetailedByFamilyId(Guid familyId);
        Task<List<HelcimTransactionResponseDetailed>> GetDetailedByPaymentCode(string paymentCode);
        Task<List<HelcimTransactionResponse>> GetByMaktabTransactionId(Guid maktabTransactionId);
        Task<List<HelcimTransactionResponseDetailed>> GetDetailedByMaktabTransactionId(Guid maktabTransactionId);
    }
}
