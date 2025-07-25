namespace WebScrapperApi.utils;

class MapModels
{
    public static ScrapingOptions ScrapingOptions(ScrapingOptions options)
    {
        return new ScrapingOptions()
        {
            Email = options.Email,
            Password = options.Password,
            Headless = options.Headless,
            UseCredentials = options.UseCredentials,
            OutputFile =  options.OutputFile,
            DownloadImages = options.DownloadImages,
            StoreInMongoDB = options.StoreInMongoDB
        };
    }
}