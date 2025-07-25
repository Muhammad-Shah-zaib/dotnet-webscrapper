using Newtonsoft.Json;

namespace WebScrapperApi.utils;

class LogModels()
{
    public static void ScrapingOptions(ScrapingOptions options, ILogger logger)
    {
        var maskedPassword = string.IsNullOrWhiteSpace(options.Password) ? "<empty>" : "<provided>";
        logger.LogInformation("ScrapingOptions received: {Options}", JsonConvert.SerializeObject(new
         {
             options.Email,
             Password = maskedPassword,
             options.UseCredentials,
             options.Headless,
             options.DownloadImages,
             options.StoreInMongoDB,
             options.OutputFile
         }));
    }
}