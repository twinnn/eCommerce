using Common.Contract.DTO.Parameter;
using System;
using System.Collections.Generic;

namespace Common.Contract.DTO.Stock
{
    public class StockDTO
    {
        public string Name { get; set; }
        public string SKU { get; set; }
        public UInt64 EAN { get; set; }
        public string ImgUrl { get; set; }
        public decimal Price { get; set; }
        public decimal Count { get; set; }
        public string Manufacturer { get; set; }
        public string ShortDescription { get; set; }
        public string LongDescription { get; set; }
        public string ImgFilePath { get; set; }
        public string RelatedLink { get; set; }
        public float Weight { get; set; }
        public float Height { get; set; }
        public float Width { get; set; }
        public float Lenght { get; set; }
        public IEnumerable<StockParameterDto> Parameters { get; set; }
        public IEnumerable<string> AlternativeImagesUrl { get; set; }
        public ushort[] Categories { get; set; }
        public string Variant {get;set;}
        public uint Id { get; set;}
        public IEnumerable<uint> RelatedStocks {get;set;}
    }
}
