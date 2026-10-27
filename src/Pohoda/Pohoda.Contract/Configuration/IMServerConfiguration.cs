namespace Pohoda.Contract.Configuration
{
    public interface IMServerConfiguration
    {
        public string ServerName { get; set; }
        public string HttpClientName { get; set; }
        public string PathToServer { get; set; }
        public short RetryDelay { get; set; }
    }
}