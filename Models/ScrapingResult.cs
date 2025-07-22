namespace WebScrapperApi.Models
{
    public class ScrapingResult
    {
        public string Status { get; set; } = string.Empty;
        public string Scraper { get; set; } = "cater-choice";
        public string Message { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public int TotalProducts { get; set; }
        public bool DownloadImagesEnabled { get; set; }
        public string OutputFile { get; set; } = string.Empty;
        public bool MongoDbEnabled { get; set; }
        public ScrapingStatistics Statistics { get; set; } = new();
        public string OutputPath { get; set; } = string.Empty;
        public List<CaterChoiceProduct> Products { get; set; } = new();
        public List<AdamsProduct> AdamsProducts { get; set; } = new();
        public List<MetroProduct> MetroProducts { get; set; } = new();
    }

    public class ScrapingStatistics
    {
        public int TotalProcessed { get; set; }
        public int NewRecordsAdded { get; set; }
        public int ExistingRecordsUpdated { get; set; }
        public int RecordsUnchanged { get; set; }
        public int Errors { get; set; }
        public List<string> CategoriesProcessed { get; set; } = new();
        public double TotalProcessingTimeSeconds { get; set; }
        public string ProcessingTimeFormatted { get; set; } = string.Empty;
    }
} 