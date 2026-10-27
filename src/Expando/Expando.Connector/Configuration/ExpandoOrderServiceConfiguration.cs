using Expando.Contract.Configuration;

namespace Expando.Connector.Configuration
{
    public class ExpandoOrderServiceConfiguration : IExpandoOrderServiceConfiguration
    {
        public string BaseUrl { get; set; }
        public int NumberOfDays { get; set; }
    }
}