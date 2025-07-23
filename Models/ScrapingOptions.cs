namespace WebScrapperApi.Models
{
    public class ScrapingOptions
    {
        public bool UseCredentials { get; set; } = false;
        public bool Headless { get; set; } = false;
        public bool DownloadImages { get; set; } = false;
        public bool StoreInMongoDB { get; set; } = false;
        public string OutputFile { get; set; } = "products.json";
    }
} 