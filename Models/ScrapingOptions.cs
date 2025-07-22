namespace WebScrapperApi.Models
{
    public class ScrapingOptions
    {
        public string? Email { get; set; }
        public string? Password { get; set; }
        public bool Headless { get; set; } = false;
        public bool DownloadImages { get; set; } = false;
        public bool StoreInMongoDB { get; set; } = true;
        public string OutputFile { get; set; } = "products.json";
    }
} 