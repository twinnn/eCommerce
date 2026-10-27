using System.Collections.Generic;

namespace Common.Contract.DTO.Category
{
    public class PohodaCategoryDto
    {
        public string Name { get; set; }
        public string Description { get; set; }        
        public List<PohodaCategoryDto>? Subcategories { get; set; }        
        public string Image { get; set; }
        public string PohodaID { get; set; }
    }
}