using MaktabDataContracts.Requests.Helcim;
using MaktabDataContracts.Responses.Helcim;

namespace Helcim.Services
{
    public interface IHelcimTransactionService
    {
        Task<HelcimPayInitializeResponse> InitializePayment(InitiatePaymentRequest request);
        Task<HelcimPayInitializeResponse> InitializePaymentForSession(InitiatePaymentRequest request, Guid userId, Guid familyId);
        Task<HelcimPayInitializeResponse> InitializeSavedCardVerification(Guid userId, Guid familyId);
        Task<SavedCardPaymentAttemptResponse> ChargeSavedCard(ChargeSavedCardRequest request, Guid userId, Guid familyId);
        Task<HelcimPaymentCompletionResponse> CompleteHelcimPayPayment(CompleteHelcimPayPaymentRequest request);
        Task<HelcimPaymentCompletionResponse> SyncInvoicePaymentByInvoiceId(int invoiceId);
        Task<HelcimPaymentCompletionResponse> SyncInvoicePayment(string invoiceReference);
        Task<HelcimPaymentCompletionResponse> SyncInvoicePaymentByInvoiceNumber(string invoiceNumber);
        Task<HelcimReconciliationResponse> ReconcileTransactions(HelcimReconciliationRequest? request = null);
        Task<IReadOnlyList<HelcimAchRefundInvoiceSummaryResponse>> GetAchRefundInvoices(GetAchRefundInvoicesRequest request);
        Task<HelcimTransactionAdjustmentResponse> RefundTransaction(RefundTransactionRequest request);
        Task<HelcimAchRefundResponse> RefundAchTransaction(RefundAchTransactionRequest request);
        Task<HelcimAchRefundResponse> RefundAchInvoice(RefundAchInvoiceRequest request);
        Task<HelcimCardRefundResponse> RefundCardTransaction(RefundCardTransactionRequest request);
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
