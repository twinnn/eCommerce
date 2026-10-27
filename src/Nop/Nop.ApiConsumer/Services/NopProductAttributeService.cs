using Microsoft.Extensions.Logging;
using Nop.Contract.Poco;
using Nop.Contract.Services;

namespace Nop.ApiConsumer.Services
{
    public class NopProductAttributeService : INopProductAttributeService
    {
        public static Dictionary<string, IEnumerable<string>> productAttributes =
            new Dictionary<string, IEnumerable<string>>

            {
                ["Length"] = new List<string> { "50", "60", "70", "80", "90", "100", "105", "110", "115", "120", "125", "130", "135", "140", "145", "150", "155", "160", "165", "170", "175", "180" },
                ["Size"] = new List<string> { "XS", "S", "M", "L", "XL" }
            };


        private readonly ILogger<NopProductAttributeService> _logger;
    }
}
