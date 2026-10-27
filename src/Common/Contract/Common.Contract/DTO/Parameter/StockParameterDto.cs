using System.Collections.Generic;

namespace Common.Contract.DTO.Parameter
{
    public class StockParameterDto
    {
        public ushort Id { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public IEnumerable<StockParameterValueDto> Values { get; set; }
    }

    public class StockParameterValueDto
    {
        public ushort ValueId { get; set; }
        public string Value { get; set; }
    }
}
