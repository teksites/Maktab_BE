namespace Email
{
    public class EmailData
    {
        public string To { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public IEnumerable<string> Cc { get; set; } = new List<string>();
        public IEnumerable<string> Bcc { get; set; } = new List<string>();
        public string Body { get; set; } = string.Empty;
    }
}
