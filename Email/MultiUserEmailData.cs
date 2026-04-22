namespace Email
{
    public class MultiUserEmailData
    {
        public IEnumerable<string> To { get; set; } = new List<string>();
        public string Subject { get; set; } = string.Empty;
        public IEnumerable<string> Cc { get; set; } = new List<string>();
        public string Body { get; set; } = string.Empty;
    }
}
