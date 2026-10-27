// See https://aka.ms/new-console-template for more information


using Common.Readers.BaseXmlReader;
using Import.Hrackys.POCO;

var hrackysFeedUrl = @"https://www.hrackys.cz/XmlFeed/SKShopHrackysXMLPartners.xml";

var myReader = new BaseXmlReader<SHOPITEM>(hrackysFeedUrl);
await foreach (var item in myReader.GetNodes()) Console.WriteLine(item?.PRODUCT);

Task.WaitAll();
Console.ReadKey();