namespace Email
{
    public class EmailData
    {
        public string To { get; set; }
        public string Subject { get; set; }
        public IEnumerable<string>Cc { get; set; } = new List<string>();
        public string Body { get; set; }
    }
}