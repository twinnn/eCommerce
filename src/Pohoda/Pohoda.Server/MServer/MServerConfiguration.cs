using Pohoda.Contract.Configuration;

namespace Pohoda.Server.MServer
{
    public class MServerConfiguration : IMServerConfiguration
    {
        public string ServerName { get; set; }
        public string HttpClientName { get; set; }
        public string PathToServer { get; set; }
        public short RetryDelay { get; set; }
    }
}