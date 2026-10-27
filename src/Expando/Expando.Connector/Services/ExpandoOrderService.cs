using System;
using System.Collections.Generic;
using Common.Readers.BaseXmlReader;
using Expando.Contract.Configuration;
using Expando.Contract.POCO;
using Expando.Contract.Services;
using Microsoft.Extensions.Logging;

namespace Expando.Connector.Services
{
    public class ExpandoOrderService : IExpandoOrderService
    {
        private readonly IExpandoOrderServiceConfiguration _config;

        private readonly ILogger<ExpandoOrderService> _logger;

        public ExpandoOrderService(ILogger<ExpandoOrderService> logger, IExpandoOrderServiceConfiguration config)
        {
            _logger = logger;
            _config = config;
        }

        public async IAsyncEnumerable<ExpandoOrder> GetExpandoOrders()
        {
            if (string.IsNullOrEmpty(_config.BaseUrl) || _config.NumberOfDays == 0) throw new ArgumentException();

            var xmlReader = new BaseXmlReader<ExpandoOrder>(_config.BaseUrl + _config.NumberOfDays);

            await foreach (var item in xmlReader.GetNodes()) yield return item;
        }
    }
}