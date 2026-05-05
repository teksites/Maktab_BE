namespace InternalContracts
{
    public class JsonMessageData
    {
        public string ExternalEndpoint { get; set; }
        public StringContent? Payload { get; set; }
        public Dictionary<string, string>? Headers { get; set; }
    }
}
