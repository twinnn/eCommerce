using Microsoft.Extensions.Logging;
using Nop.Contract.POCO;

namespace Jobs.Nop.Synch.Helpers;

public interface IImageHelper
{
    Image GetImage(string image);
    StockImage GetStockImage(string image);
}
 
public class ImageHelper : IImageHelper
{
    private readonly ILogger<ImageHelper> _logger;
    private readonly string SourceDirectory = @"C:\ProgramData\STORMWARE\POHODA SK E1\Dokumenty\ACecom\Obrázky\";
    private readonly string TargetDirectory = @"C:\inetpub\wwwroot\nopCommerce450\wwwroot\images\UploadImagesSynch\";
    public ImageHelper(ILogger<ImageHelper> logger)
    {
        _logger = logger;
    }

    public Image? GetImage(string image)
    {

        if (string.IsNullOrEmpty(image))
        {
            return null;
        }

        try
        {
            if (!string.IsNullOrEmpty(image))
            {
                var imageName = image.Split('\\').Last();
                var sourceImagePath = $"{SourceDirectory}{imageName}";
                var targetImagePath = Path.Combine(TargetDirectory, imageName);
                if (File.Exists(sourceImagePath) && !File.Exists(targetImagePath))
                {
                    File.Copy(sourceImagePath, targetImagePath);
                }

                var mapped = new Image
                {
                    src = @$"http://20.113.184.46:8088/images/UploadImagesSynch/{imageName}"
                };

                return mapped;
            }
            return null;

        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error while copying main image");
            return null;
        }
    }

    public StockImage? GetStockImage(string image)
    {
        if (string.IsNullOrEmpty(image))
        {
            return null;
        }

        try
        {
            if (!string.IsNullOrEmpty(image))
            {
                var imageName = image.Split('\\').Last();
                var sourceImagePath = $"{SourceDirectory}{imageName}";
                var targetImagePath = Path.Combine(TargetDirectory, imageName);
                if (File.Exists(sourceImagePath) && !File.Exists(targetImagePath))
                {
                    File.Copy(sourceImagePath, targetImagePath);
                }

                var mapped = new StockImage
                {
                    id = 0,
                    picture_id = 0,
                    position = 0,
                    src = @$"http://20.113.184.46:8088/images/UploadImagesSynch/{imageName}",
                };




                //try
                //{
                //    var counter = 1;
                //    foreach (var i in images.Skip(1))
                //    {
                //        File.Copy(i.src, $@"C:\inetpub\wwwroot\nopCommerce450\wwwroot\images\UploadImagesSynch\{i.src.Split('\\').Last()}");
                //        mapped.Add(new StockImage
                //        {
                //            id = counter,
                //            picture_id = 0,
                //            position = 0,
                //            src = $@"http://20.113.184.46:8088/images/UploadImagesSynch/{i.src.Split('\\').Last()}",
                //        });
                //        counter++;
                //    }
                //}
                //catch (Exception e)
                //{
                //    _logger.LogError(e, "Error while copying alternative images");
                //}

                return mapped;
            }
            return null;

        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error while copying main image");
            return null;
        }
    }
}