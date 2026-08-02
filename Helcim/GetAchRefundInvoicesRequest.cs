namespace Helcim
{
    public class GetAchRefundInvoicesRequest
    {
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string SearchText { get; set; } = string.Empty;
        public bool OnlyRefundable { get; set; } = true;
        public bool IncludeRefundTransactions { get; set; }
        public int Page { get; set; } = 1;
        public int Limit { get; set; } = 50;
    }
}
