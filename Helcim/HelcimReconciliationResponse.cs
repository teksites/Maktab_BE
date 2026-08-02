namespace Helcim
{
    public class HelcimReconciliationResponse
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool AlreadyRunning { get; set; }
        public string Message { get; set; } = string.Empty;
        public int CardTransactionsFetched { get; set; }
        public int AchTransactionsFetched { get; set; }
        public int StoredTransactions { get; set; }
        public int AppliedPayments { get; set; }
        public int AppliedRefunds { get; set; }
        public int SkippedDuplicates { get; set; }
        public int SkippedPendingAchTransactions { get; set; }
        public int StoredNonSettledAchTransactions { get; set; }
        public int UnmatchedTransactions { get; set; }
    }
}
