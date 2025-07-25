using Microsoft.AspNetCore.Mvc;

namespace WebScrapperApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AdamsController(AdamsScraperService adamsScraperService, ILogger<AdamsController> logger,  ScraperLockService scraperLockService ) : ControllerBase
    {
        private readonly AdamsScraperService _adamsScraperService = adamsScraperService;
        private readonly ScraperLockService _scraperLockService = scraperLockService;

        /// <summary>
        /// Scrape all categories from Adams Food Service
        /// </summary>
        /// <param name="options">Scraping configuration options</param>
        /// <returns>Scraping result with products and statistics</returns>
        [HttpPost("scrape-all-categories")]
        public async Task<IActionResult> ScrapeAllCategories([FromBody] ScrapingOptions options)
        {
            LogModels.ScrapingOptions(options, logger);

            if (!_scraperLockService.TryStartScraping("Adams"))
            {
                return Conflict(new
                {
                    status = "error",
                    message = $"Another scraper is already running: '{_scraperLockService.CurrentScraper}'"
                });
            }

            try
            {
                logger.LogInformation("Starting Adams scrape all categories request");

                var result = await _adamsScraperService.ScrapeAllCategoriesAsync(options);

                logger.LogInformation("Adams scrape all categories completed successfully. Total products: {TotalProducts}",
                    result.TotalProducts);

                return Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during Adams scrape all categories request");
                return StatusCode(500, new
                {
                    status = "error",
                    message = "An error occurred during Adams scraping",
                    error = ex.Message
                });
            }
            finally
            {
                _scraperLockService.StopScraping();
            }
        }


        /// <summary>
        /// Scrape a specific category from Adams Food Service
        /// </summary>
        /// <param name="categoryName">Name of the category to scrape</param>
        /// <param name="options">Scraping configuration options</param>
        /// <returns>List of scraped products for the category</returns>
        [HttpPost("scrape-category/{categoryName}")]
        public async Task<IActionResult> ScrapeCategory(string categoryName, [FromBody] ScrapingOptions options)
        {
            LogModels.ScrapingOptions(options, logger);

            if (!_scraperLockService.TryStartScraping("Adams"))
            {
                return Conflict(new
                {
                    status = "error",
                    message = $"Another scraper is already running: '{_scraperLockService.CurrentScraper}'"
                });
            }

            try
            {
                logger.LogInformation("Starting Adams scrape category request for: {CategoryName}", categoryName);

                var category = AdamsConfig.ADAMS_CATEGORIES
                    .FirstOrDefault(c => string.Equals(c.Name, categoryName, StringComparison.OrdinalIgnoreCase));

                if (category == null)
                {
                    return BadRequest(new
                    {
                        status = "error",
                        message = $"Category '{categoryName}' not found",
                        availableCategories = AdamsConfig.ADAMS_CATEGORIES.Select(c => c.Name).ToList()
                    });
                }

                var products = await _adamsScraperService.ScrapeCategoryAsync(options, category);

                logger.LogInformation("Adams scrape category completed successfully. Products found: {ProductCount}",
                    products.Count);

                return Ok(new
                {
                    status = "success",
                    category = categoryName,
                    total_products = products.Count,
                    products,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during Adams scrape category request for {CategoryName}", categoryName);
                return StatusCode(500, new
                {
                    status = "error",
                    message = "An error occurred during Adams scraping",
                    error = ex.Message
                });
            }
            finally
            {
                _scraperLockService.StopScraping();
            }
        }


        /// <summary>
        /// Get available categories for Adams scraping
        /// </summary>
        /// <returns>List of available categories</returns>
        [HttpGet("categories")]
        public IActionResult GetCategories()
        {
            try
            {
                var categories = AdamsConfig.ADAMS_CATEGORIES.Select(c => new
                {
                    name = c.Name,
                    url = c.Url
                }).ToList();

                return Ok(new
                {
                    status = "success",
                    categories,
                    total_categories = categories.Count
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error getting Adams categories");
                return StatusCode(500, new
                {
                    status = "error",
                    message = "An error occurred while retrieving Adams categories",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Get Adams scraping configuration and status
        /// </summary>
        /// <returns>Current Adams scraping configuration</returns>
        [HttpGet("config")]
        public IActionResult GetConfig()
        {
            try
            {
                return Ok(new
                {
                    status = "success",
                    base_url = AdamsConfig.ADAMS_BASE_URL,
                    total_categories = AdamsConfig.ADAMS_CATEGORIES.Count,
                    selectors = new
                    {
                        product_list = AdamsConfig.AdamsSelectors.PRODUCT_LIST,
                        product_item = AdamsConfig.AdamsSelectors.PRODUCT_ITEM,
                        product_name_relative = AdamsConfig.AdamsSelectors.PRODUCT_NAME_RELATIVE,
                        product_sku_relative = AdamsConfig.AdamsSelectors.PRODUCT_SKU_RELATIVE,
                        product_image_relative = AdamsConfig.AdamsSelectors.PRODUCT_IMAGE_RELATIVE,
                        load_more_button = AdamsConfig.AdamsSelectors.LOAD_MORE_BUTTON,
                        product_grid = AdamsConfig.AdamsSelectors.PRODUCT_GRID,
                        product_container = AdamsConfig.AdamsSelectors.PRODUCT_CONTAINER
                    },
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error getting Adams configuration");
                return StatusCode(500, new
                {
                    status = "error",
                    message = "An error occurred while retrieving Adams configuration",
                    error = ex.Message
                });
            }
        }
    }
} 