using System.Collections.Generic;

namespace Common.Contract.DTO.Parameter
{
    public class PohodaParameterDto
    {
        public string Name { get; set; }
        public string? Type { get; set; }

        public List<PohodaParameterDto>? SubParameters { get; set; }

        public string? Value { get; set; }
    }
}