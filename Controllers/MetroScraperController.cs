using Microsoft.AspNetCore.Mvc;


namespace WebScrapperApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MetroScraperController(MetroScraperService metroScraperService, ILogger<MetroScraperController> logger) : ControllerBase
    {
        private readonly MetroScraperService _metroScraperService = metroScraperService;
        private readonly ILogger<MetroScraperController> _logger = logger;

        [HttpPost("scrape-all")]
        public async Task<IActionResult> ScrapeAllCategories([FromBody] ScrapingOptions options)
        {
            try
            {
                _logger.LogInformation("Starting Metro scrape all categories request");
                var result = await _metroScraperService.ScrapeAllCategoriesAsync(options);
                _logger.LogInformation("Metro scrape all categories completed successfully. Total products: {TotalProducts}", result.TotalProducts);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during Metro scrape all categories request");
                return StatusCode(500, new
                {
                    status = "error",
                    message = "An error occurred during Metro scraping",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Scrape a specific category from Metro
        /// </summary>
        /// <param name="categoryName">Name of the category to scrape</param>
        /// <param name="options">Scraping configuration options</param>
        /// <returns>List of scraped products for the category</returns>
        [HttpPost("scrape-category/{categoryName}")]
        public async Task<IActionResult> ScrapeCategory(string categoryName, [FromBody] ScrapingOptions options)
        {
            try
            {
                _logger.LogInformation("Starting Metro scrape category request for: {CategoryName}", categoryName);

                var category = MetroConfig.METRO_CATEGORIES
                    .FirstOrDefault(c => string.Equals(c.Name, categoryName, StringComparison.OrdinalIgnoreCase));

                if (category == null)
                {
                    return BadRequest(new
                    {
                        status = "error",
                        message = $"Category '{categoryName}' not found",
                        availableCategories = MetroConfig.METRO_CATEGORIES.Select(c => c.Name).ToList()
                    });
                }

                var products = await _metroScraperService.ScrapeCategoryAsync(options, category);

                _logger.LogInformation("Metro scrape category completed successfully. Products found: {ProductCount}", products.Count);

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
                _logger.LogError(ex, "Error during Metro scrape category request for {CategoryName}", categoryName);
                return StatusCode(500, new
                {
                    status = "error",
                    message = "An error occurred during Metro scraping",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Get available categories for Metro scraping
        /// </summary>
        /// <returns>List of available categories</returns>
        [HttpGet("categories")]
        public IActionResult GetCategories()
        {
            try
            {
                var categories = MetroConfig.METRO_CATEGORIES.Select(c => new
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
                _logger.LogError(ex, "Error getting Metro categories");
                return StatusCode(500, new
                {
                    status = "error",
                    message = "An error occurred while retrieving Metro categories",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Get Metro scraping configuration and status
        /// </summary>
        /// <returns>Current Metro scraping configuration</returns>
        [HttpGet("config")]
        public IActionResult GetConfig()
        {
            try
            {
                return Ok(new
                {
                    status = "success",
                    base_url = MetroConfig.METRO_BASE_URL,
                    total_categories = MetroConfig.METRO_CATEGORIES.Count,
                    selectors = new
                    {
                        product_grid = MetroConfig.MetroSelectors.PRODUCT_GRID,
                        product_name = MetroConfig.MetroSelectors.PRODUCT_NAME,
                        product_price = MetroConfig.MetroSelectors.PRODUCT_PRICE,
                        product_price_alt = MetroConfig.MetroSelectors.PRODUCT_PRICE_ALT,
                        product_url = MetroConfig.MetroSelectors.PRODUCT_URL,
                        product_image_wrap = MetroConfig.MetroSelectors.PRODUCT_IMAGE_WRAP,
                        product_image = MetroConfig.MetroSelectors.PRODUCT_IMAGE,
                        next_page = MetroConfig.MetroSelectors.NEXT_PAGE
                    },
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting Metro configuration");
                return StatusCode(500, new
                {
                    status = "error",
                    message = "An error occurred while retrieving Metro configuration",
                    error = ex.Message
                });
            }
        }
    }
} 