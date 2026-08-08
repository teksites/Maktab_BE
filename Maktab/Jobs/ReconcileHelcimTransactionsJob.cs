using Helcim.Services;
using Microsoft.Extensions.Logging;
using Quartz;
using System.Threading.Tasks;

namespace Maktab.Jobs
{
    [DisallowConcurrentExecution]
    public class ReconcileHelcimTransactionsJob : IJob
    {
        private readonly IHelcimTransactionService _helcimTransactionService;
        private readonly ILogger<ReconcileHelcimTransactionsJob> _logger;

        public ReconcileHelcimTransactionsJob(
            IHelcimTransactionService helcimTransactionService,
            ILogger<ReconcileHelcimTransactionsJob> logger)
        {
            _helcimTransactionService = helcimTransactionService;
            _logger = logger;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            var result = await _helcimTransactionService.ReconcileTransactions().ConfigureAwait(false);

            _logger.LogInformation(
                "Helcim reconciliation completed for {StartDate:yyyy-MM-dd} to {EndDate:yyyy-MM-dd}. Cards fetched: {CardTransactionsFetched}, ACH fetched: {AchTransactionsFetched}, stored: {StoredTransactions}, applied payments: {AppliedPayments}, applied refunds: {AppliedRefunds}, skipped duplicates: {SkippedDuplicates}, skipped pending ACH: {SkippedPendingAchTransactions}, stored non-settled ACH: {StoredNonSettledAchTransactions}, unmatched: {UnmatchedTransactions}.",
                result.StartDate,
                result.EndDate,
                result.CardTransactionsFetched,
                result.AchTransactionsFetched,
                result.StoredTransactions,
                result.AppliedPayments,
                result.AppliedRefunds,
                result.SkippedDuplicates,
                result.SkippedPendingAchTransactions,
                result.StoredNonSettledAchTransactions,
                result.UnmatchedTransactions);
        }
    }
}
