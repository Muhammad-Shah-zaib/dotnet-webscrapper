using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace WebScrapperApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CaterChoiceScraperController(CaterChoiceScraperService scraperService, ILogger<CaterChoiceScraperController> logger, ScraperLockService scraperLockService) : ControllerBase
    {
        private readonly CaterChoiceScraperService _scraperService = scraperService;
        private readonly ScraperLockService _scraperLockService = scraperLockService;
        private readonly ILogger<CaterChoiceScraperController> _logger = logger;

        /// <summary>
        /// Scrape all categories from Cater Choice
        /// </summary>
        /// <param name="options">Scraping configuration options</param>
        /// <returns>Scraping result with products and statistics</returns>
        [HttpPost("scrape-all-categories")]
        public async Task<IActionResult> ScrapeAllCategories([FromBody] ScrapingOptions options)
        {
            var maskedPassword = string.IsNullOrWhiteSpace(options.Password) ? "<empty>" : "<provided>";

            _logger.LogInformation("ScrapingOptions received: {Options}", JsonConvert.SerializeObject(new
            {
                options.Email,
                Password = maskedPassword,
                options.UseCredentials,
                options.Headless,
                options.DownloadImages,
                options.StoreInMongoDB,
                options.OutputFile
            }));
            if (!_scraperLockService.TryStartScraping("CaterChoice"))
            {
                return Conflict(new
                {
                    status = "error",
                    message = $"Another scraper is already running: '{_scraperLockService.CurrentScraper}'"
                });
            }
            try
            {
                _logger.LogInformation("Starting scrape all categories request");

                var result = await _scraperService.ScrapeAllCategoriesAsync(options);

                _logger.LogInformation("Scrape all categories completed successfully. Total products: {TotalProducts}",
                    result.TotalProducts);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during scrape all categories request");
                return StatusCode(500, new
                {
                    status = "error",
                    message = "An error occurred during scraping",
                    error = ex.Message
                });
            }
            finally
            {
                _scraperLockService.StopScraping();
            }
        }

        /// <summary>
        /// Scrape a specific category from Cater Choice
        /// </summary>
        /// <param name="categoryName">Name of the category to scrape</param>
        /// <param name="options">Scraping configuration options</param>
        /// <returns>List of scraped products for the category</returns>
        [HttpPost("scrape-category/{categoryName}")]
        public async Task<IActionResult> ScrapeCategory(string categoryName, [FromBody] ScrapingOptions options)
        {
            var maskedPassword = string.IsNullOrWhiteSpace(options.Password) ? "<empty>" : "<provided>";

            _logger.LogInformation("ScrapingOptions received: {Options}", JsonConvert.SerializeObject(new
            {
                options.Email,
                Password = maskedPassword,
                options.UseCredentials,
                options.Headless,
                options.DownloadImages,
                options.StoreInMongoDB,
                options.OutputFile
            }));
            if (!_scraperLockService.TryStartScraping("CaterChoice"))
            {
                return Conflict(new
                {
                    status = "error",
                    message = $"Another scraper is already running: '{_scraperLockService.CurrentScraper}'"
                });
            }

            try
            {
                _logger.LogInformation("Starting scrape category request for: {CategoryName}", categoryName);

                // Find the category by name
                var category = CaterChoiceConfig.CATER_CHOICE_CATEGORIES
                    .FirstOrDefault(c => string.Equals(c.Name, categoryName, StringComparison.OrdinalIgnoreCase));

                if (category == null)
                {
                    return BadRequest(new
                    {
                        status = "error",
                        message = $"Category '{categoryName}' not found",
                        availableCategories = CaterChoiceConfig.CATER_CHOICE_CATEGORIES.Select(c => c.Name).ToList()
                    });
                }

                var products = await _scraperService.ScrapeCategoryAsync(options, category);

                _logger.LogInformation("Scrape category completed successfully. Products found: {ProductCount}",
                    products.Count);

                return Ok(new
                {
                    status = "success",
                    category = categoryName,
                    total_products = products.Count,
                    products = products,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during scrape category request for {CategoryName}", categoryName);
                return StatusCode(500, new
                {
                    status = "error",
                    message = "An error occurred during scraping",
                    error = ex.Message
                });
            }
            finally
            {
                _scraperLockService.StopScraping();
            }
        }

        /// <summary>
        /// Get available categories for scraping
        /// </summary>
        /// <returns>List of available categories</returns>
        [HttpGet("categories")]
        public IActionResult GetCategories()
        {
            try
            {
                var categories = CaterChoiceConfig.CATER_CHOICE_CATEGORIES.Select(c => new
                {
                    name = c.Name,
                    url = c.Url
                }).ToList();

                return Ok(new
                {
                    status = "success",
                    categories = categories,
                    total_categories = categories.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting categories");
                return StatusCode(500, new
                {
                    status = "error",
                    message = "An error occurred while retrieving categories",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Get scraping configuration and status
        /// </summary>
        /// <returns>Current scraping configuration</returns>
        [HttpGet("config")]
        public IActionResult GetConfig()
        {
            try
            {
                return Ok(new
                {
                    status = "success",
                    base_url = CaterChoiceConfig.CATER_CHOICE_BASE_URL,
                    total_categories = CaterChoiceConfig.CATER_CHOICE_CATEGORIES.Count,
                    selectors = new
                    {
                        product_grid = CaterChoiceConfig.CaterChoiceSelectors.PRODUCT_GRID,
                        product_container = CaterChoiceConfig.CaterChoiceSelectors.PRODUCT_CONTAINER,
                        product_name = CaterChoiceConfig.CaterChoiceSelectors.PRODUCT_NAME,
                        product_image = CaterChoiceConfig.CaterChoiceSelectors.PRODUCT_IMAGE,
                        pack_size = CaterChoiceConfig.CaterChoiceSelectors.PACK_SIZE,
                        case_price = CaterChoiceConfig.CaterChoiceSelectors.CASE_PRICE,
                        single_price = CaterChoiceConfig.CaterChoiceSelectors.SINGLE_PRICE,
                        product_code = CaterChoiceConfig.CaterChoiceSelectors.PRODUCT_CODE,
                        product_description = CaterChoiceConfig.CaterChoiceSelectors.PRODUCT_DESCRIPTION
                    },
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting configuration");
                return StatusCode(500, new
                {
                    status = "error",
                    message = "An error occurred while retrieving configuration",
                    error = ex.Message
                });
            }
        }
    }
} 