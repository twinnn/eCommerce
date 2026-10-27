namespace Expando.Contract.Configuration
{
    public interface IExpandoOrderServiceConfiguration
    {
        public string BaseUrl { get; set; }
        public int NumberOfDays { get; set; }
    }
}