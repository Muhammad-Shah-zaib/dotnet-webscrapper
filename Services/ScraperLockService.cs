namespace WebScrapperApi.Services;

public class ScraperLockService
{
    private readonly object _lock = new();

    public bool TryStartScraping(string scraperName)
    {
        lock (_lock)
        {
            if (IsScrapingInProgress)
                return false;

            IsScrapingInProgress = true;
            CurrentScraper = scraperName;
            return true;
        }
    }

    public void StopScraping()
    {
        lock (_lock)
        {
            IsScrapingInProgress = false;
            CurrentScraper = null;
        }
    }

    public bool IsScrapingInProgress { get; private set; } = false;

    public string? CurrentScraper { get; private set; } = null;
}
