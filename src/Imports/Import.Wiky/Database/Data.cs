namespace Import.Wiky.Database;

public class Data
{
    public int Id { get; set; }
    public string PohodaOrderId { get; set; }
    public string AmazonOrderId { get; set; }
    public string InternalPackageId { get; set; }
    public string PackageTrackingNumber { get; set; }
    public string PackageBarCode { get; set; }
    public string CarrierInfo { get; set; }
    public DateTime DateCreated { get; set; }
    public string Status { get; set; }
    public string LabelId { get; set; }

    public string Manifacturer { get; set; }
    public decimal hmotnost { get; set; }
    public string Dĺžka { get; set; }
    public string Šírka { get; set; }
    public string Výška { get; set; }
}