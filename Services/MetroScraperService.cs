using Microsoft.Playwright;
using Microsoft.Extensions.Logging;
using WebScrapperApi.Models;
using WebScrapperApi.Configuration;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace WebScrapperApi.Services
{
    public class MetroScraperService(UtilityService utilityService, ILogger<MetroScraperService> logger, ScraperDbContext dbContext)
    {
        private readonly UtilityService _utilityService = utilityService;
        private readonly ILogger<MetroScraperService> _logger = logger;
        private readonly ScraperDbContext _dbContext = dbContext;

        public async Task<ScrapingResult> ScrapeAllCategoriesAsync(ScrapingOptions options)
        {
            var startTime = DateTime.UtcNow;
            var allProducts = new List<MetroProduct>();
            var statistics = new ScrapingStatistics();

            // MongoDB setup
            var mongoEnabled = false;

            if (options.StoreInMongoDB)
            {
                try
                {
                    await _dbContext.ConnectAsync();
                    mongoEnabled = true;
                    _logger.LogInformation("MongoDB connection established using ScraperDbContext");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to connect to MongoDB. Proceeding without database integration.");
                    mongoEnabled = false;
                }
            }
            else
            {
                _logger.LogInformation("MongoDB storage disabled by user preference");
            }

            // Initialize Playwright
            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = options.Headless
            });

            try
            {
                // Process each category
                foreach (var category in MetroConfig.METRO_CATEGORIES)
                {
                    _logger.LogInformation("Processing Metro category: {CategoryName}", category.Name);

                    try
                    {
                        var categoryProducts = await ScrapeCategoryAsync(MapModels.ScrapingOptions(options), category, browser);
                        allProducts.AddRange(categoryProducts);
                        statistics.CategoriesProcessed.Add(category.Name);
                        statistics.TotalProcessed += categoryProducts.Count;

                        // Save to MongoDB if enabled
                        if (mongoEnabled)
                        {
                            var mongoStats = await _dbContext.SaveMetroProductsAsync(categoryProducts);
                            statistics.NewRecordsAdded += mongoStats.NewRecordsAdded;
                            statistics.ExistingRecordsUpdated += mongoStats.ExistingRecordsUpdated;
                            statistics.RecordsUnchanged += mongoStats.RecordsUnchanged;
                            statistics.Errors += mongoStats.Errors;
                        }
                        else
                        {
                            statistics.NewRecordsAdded += categoryProducts.Count;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error scraping Metro category {CategoryName}", category.Name);
                        statistics.Errors++;
                    }
                }

                // Calculate processing time
                var endTime = DateTime.UtcNow;
                var processingTimeSeconds = (endTime - startTime).TotalSeconds;
                statistics.TotalProcessingTimeSeconds = processingTimeSeconds;
                statistics.ProcessingTimeFormatted = _utilityService.FormatProcessingTime(processingTimeSeconds);

                // Save results to file
                var outputPath = _utilityService.SaveToJson(new
                {
                    products = allProducts,
                    metadata = new
                    {
                        total_products = allProducts.Count,
                        timestamp = DateTime.UtcNow.ToString("O"),
                        download_images_enabled = options.DownloadImages,
                        statistics,
                        mongodb_enabled = mongoEnabled,
                    }
                }, options.OutputFile);

                // Disconnect from MongoDB if connected
                if (mongoEnabled)
                {
                    _dbContext.Disconnect();
                }

                return new ScrapingResult
                {
                    Status = "success",
                    Scraper = "metro",
                    Message = "Metro scraping completed successfully",
                    Timestamp = DateTime.UtcNow,
                    TotalProducts = allProducts.Count,
                    DownloadImagesEnabled = options.DownloadImages,
                    OutputFile = options.OutputFile,
                    MongoDbEnabled = mongoEnabled,
                    Statistics = statistics,
                    OutputPath = outputPath,
                    MetroProducts = allProducts
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during Metro scraping process");
                throw;
            }
        }

        public async Task<List<MetroProduct>> ScrapeCategoryAsync(ScrapingOptions options, Category category, IBrowser? existingBrowser = null)
        {
            _logger.LogInformation("Starting to scrape Metro category: {CategoryName}", category.Name);
            _logger.LogInformation("Category URL: {CategoryUrl}", category.Url);

            var startTime = DateTime.UtcNow;
            var products = new List<MetroProduct>();
            IBrowser? browser = null;
            IBrowserContext? context = null;
            IPage? page = null;

            // MongoDB setup
            var mongoEnabled = false;
            MongoStats mongoStats = new();
            if (options.StoreInMongoDB)
            {
                try
                {
                    await _dbContext.ConnectAsync();
                    mongoEnabled = true;
                    _logger.LogInformation("MongoDB connection established using ScraperDbContext");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to connect to MongoDB. Proceeding without database integration.");
                    mongoEnabled = false;
                }
            }
            else
            {
                _logger.LogInformation("MongoDB storage disabled by user preference");
            }

            try
            {
                // Initialize Playwright if not provided
                if (existingBrowser == null)
                {
                    var playwright = await Playwright.CreateAsync();
                    browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
                    {
                        Headless = options.Headless
                    });
                }
                else
                {
                    browser = existingBrowser;
                }

                // Create browser context
                context = await browser.NewContextAsync(new BrowserNewContextOptions
                {
                    ViewportSize = new ViewportSize { Width = 1920, Height = 1080 }
                });

                // Create page
                page = await context.NewPageAsync();

                // Scrape category with pagination
                products = await ScrapeCategoryWithPaginationAsync(page, category, options.DownloadImages, 0);

                _logger.LogInformation("Finished scraping category {CategoryName}, found {ProductCount} products total", category.Name, products.Count);

                // Save to MongoDB if enabled
                if (mongoEnabled && products.Count > 0)
                {
                    _logger.LogInformation("Saving {Count} products to MongoDB...", products.Count);
                    mongoStats = await _dbContext.SaveMetroProductsAsync(products);
                    _logger.LogInformation("MongoDB save stats: {Stats}", System.Text.Json.JsonSerializer.Serialize(mongoStats));
                }

                // If this is a standalone category scrape
                if (existingBrowser == null && !string.IsNullOrEmpty(options.OutputFile))
                {
                    var endTime = DateTime.UtcNow;
                    var processingTimeSeconds = (endTime - startTime).TotalSeconds;

                    var statistics = new ScrapingStatistics
                    {
                        TotalProcessed = products.Count,
                        NewRecordsAdded = mongoEnabled ? mongoStats.NewRecordsAdded : products.Count,
                        ExistingRecordsUpdated = mongoStats.ExistingRecordsUpdated,
                        RecordsUnchanged = mongoStats.RecordsUnchanged,
                        Errors = mongoStats.Errors,
                        CategoriesProcessed = new List<string> { category.Name },
                        TotalProcessingTimeSeconds = processingTimeSeconds,
                        ProcessingTimeFormatted = _utilityService.FormatProcessingTime(processingTimeSeconds)
                    };

                    // Save results to file
                    var outputPath = _utilityService.SaveToJson(new
                    {
                        products,
                        metadata = new
                        {
                            category = category.Name,
                            total_products = products.Count,
                            timestamp = DateTime.UtcNow.ToString("O"),
                            download_images_enabled = options.DownloadImages,
                            mongodb_enabled = mongoEnabled,
                            statistics,
                        }
                    }, options.OutputFile);

                    // Disconnect from MongoDB if connected
                    if (mongoEnabled)
                    {
                        _dbContext.Disconnect();
                    }

                    _logger.LogInformation("Metro category scraping completed successfully for category: {CategoryName}", category.Name);

                    // Return ScrapingResult for standalone
                    throw new StandaloneScrapingResultException(new ScrapingResult
                    {
                        Status = "success",
                        Scraper = "metro",
                        Message = $"Metro scraping completed successfully for category: {category.Name}",
                        Timestamp = DateTime.UtcNow,
                        TotalProducts = products.Count,
                        DownloadImagesEnabled = options.DownloadImages,
                        OutputFile = options.OutputFile,
                        MongoDbEnabled = mongoEnabled,
                        Statistics = statistics,
                        OutputPath = outputPath,
                        MetroProducts = products
                    });
                }

                return products;
            }
            catch (StandaloneScrapingResultException ex)
            {
                // Special case: return ScrapingResult for standalone scrape
                  return ex.Result.MetroProducts ?? [];
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scraping Metro category {CategoryName}", category.Name);
                throw;
            }
            finally
            {
                if (page != null) await page.CloseAsync();
                if (context != null) await context.CloseAsync();
                if (browser != null && existingBrowser == null) await browser.CloseAsync();
                if (mongoEnabled && existingBrowser == null)
                {
                    _dbContext.Disconnect();
                }
            }
        }

        private async Task<List<MetroProduct>> ScrapeCategoryWithPaginationAsync(IPage page, Category category, bool downloadImages, int offset)
        {
            var allProducts = new List<MetroProduct>();
            try
            {
                // Construct paginated URL
                var paginatedUrl = category.Url;
                if (offset > 0)
                {
                    var uriBuilder = new UriBuilder(category.Url);
                    var query = System.Web.HttpUtility.ParseQueryString(uriBuilder.Query);
                    query["offset"] = offset.ToString();
                    uriBuilder.Query = query.ToString();
                    paginatedUrl = uriBuilder.ToString();
                }

                _logger.LogInformation("Navigating to: {PaginatedUrl}", paginatedUrl);
                await page.GotoAsync(paginatedUrl, new PageGotoOptions { Timeout = 60000 });
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions { Timeout = 30000 });

                // Wait for product grid
                try
                {
                    await page.WaitForSelectorAsync(MetroConfig.MetroSelectors.PRODUCT_GRID, new PageWaitForSelectorOptions { Timeout = 20000 });
                    _logger.LogInformation("Products found on page");
                }
                catch (Exception)
                {
                    _logger.LogInformation("No product grid found on this page, might be end of pagination");
                    return allProducts;
                }

                // Extract products from current page
                var pageProducts = await ExtractProductsFromPageAsync(page, category.Name, downloadImages);
                _logger.LogInformation("Found {Count} products on current page", pageProducts.Count);
                allProducts.AddRange(pageProducts);

                // Check for next page
                var nextButton = await page.QuerySelectorAsync(MetroConfig.MetroSelectors.NEXT_PAGE);
                if (nextButton != null)
                {
                    _logger.LogInformation("Found next page button, continuing pagination...");
                    var nextOffset = offset + 60; // Metro uses 60 items per page
                    var nextPageProducts = await ScrapeCategoryWithPaginationAsync(page, category, downloadImages, nextOffset);
                    allProducts.AddRange(nextPageProducts);
                }
                else
                {
                    _logger.LogInformation("No more pages found, pagination complete");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scraping page with offset {Offset}", offset);
            }
            return allProducts;
        }

        private async Task<List<MetroProduct>> ExtractProductsFromPageAsync(IPage page, string categoryName, bool downloadImages)
        {
            _logger.LogInformation("Extracting products from page for category: {CategoryName}", categoryName);
            var products = new List<MetroProduct>();

            // Take a screenshot of the category page for debugging
            var screenshotDir = Path.Combine("screenshots", "metro");
            Directory.CreateDirectory(screenshotDir);
            var screenshotPath = Path.Combine(screenshotDir, $"metro-{categoryName}-page.png");
            await page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = screenshotPath,
                FullPage = true
            });
            _logger.LogInformation("Category page screenshot saved as {ScreenshotPath}", screenshotPath);

            // Get all product elements
            // TODO: CHECK IF THIS SELECTOR IS CORRECT
            var productElements = await page.QuerySelectorAllAsync(MetroConfig.MetroSelectors.PRODUCT_ITEM);
            _logger.LogInformation("Processing {Count} product elements", productElements.Count);

            for (int i = 0; i < productElements.Count; i++)
            {
                var productElement = productElements[i];
                try
                {
                    _logger.LogInformation("Processing product {Index} of {Total}", i + 1, productElements.Count);

                    // Extract product name
                    string productName = "Unknown Product";
                    var nameSelectors = new[]
                    {
                        MetroConfig.MetroSelectors.PRODUCT_NAME,
                        "a.grid-product__title",
                        ".grid-product__title"
                    };
                    foreach (var selector in nameSelectors)
                    {
                        try
                        {
                            var nameElement = await productElement.QuerySelectorAsync(selector);
                            if (nameElement != null)
                            {
                                productName = await nameElement.InnerTextAsync();
                                if (!string.IsNullOrWhiteSpace(productName))
                                {
                                    _logger.LogInformation("Found product name: {ProductName}", productName);
                                    break;
                                }
                            }
                        }
                        catch { }
                    }
                    if (string.IsNullOrWhiteSpace(productName) || productName == "Unknown Product")
                    {
                        _logger.LogInformation("Skipping product {Index}: no valid name found", i + 1);
                        continue;
                    }

                    // Extract product price
                    string productPrice = "Price not available";
                    var priceSelectors = new[]
                    {
                        MetroConfig.MetroSelectors.PRODUCT_PRICE,
                        MetroConfig.MetroSelectors.PRODUCT_PRICE_ALT,
                        ".grid-product__price",
                        ".price"
                    };
                    foreach (var selector in priceSelectors)
                    {
                        try
                        {
                            var priceElement = await productElement.QuerySelectorAsync(selector);
                            if (priceElement != null)
                            {
                                productPrice = await priceElement.InnerTextAsync();
                                if (!string.IsNullOrWhiteSpace(productPrice))
                                {
                                    _logger.LogInformation("Found product price: {ProductPrice}", productPrice);
                                    break;
                                }
                            }
                        }
                        catch { }
                    }

                    // Extract product URL
                    string? productUrl = null;
                    try
                    {
                        var urlElement = await productElement.QuerySelectorAsync(MetroConfig.MetroSelectors.PRODUCT_URL);
                        if (urlElement != null)
                        {
                            productUrl = await urlElement.GetAttributeAsync("href");
                            if (!string.IsNullOrEmpty(productUrl) && !productUrl.StartsWith("http"))
                            {
                                productUrl = MetroConfig.METRO_BASE_URL.TrimEnd('/') + "/" + productUrl.TrimStart('/');
                            }
                            _logger.LogInformation("Found product URL: {ProductUrl}", productUrl);
                        }
                    }
                    catch { _logger.LogInformation("Could not extract product URL"); }

                    // Extract product image
                    string? imageUrl = null;
                    string? imageFilename = null;
                    try
                    {
                        var imageWrapElement = await productElement.QuerySelectorAsync(MetroConfig.MetroSelectors.PRODUCT_IMAGE_WRAP);
                        if (imageWrapElement != null)
                        {
                            // Try to find <img> tag first
                            var imgElement = await imageWrapElement.QuerySelectorAsync(MetroConfig.MetroSelectors.PRODUCT_IMAGE);
                            if (imgElement != null)
                            {
                                imageUrl = await imgElement.GetAttributeAsync("src");
                                if (!string.IsNullOrEmpty(imageUrl))
                                {
                                    _logger.LogInformation("Found img src URL: {ImageUrl}", imageUrl);
                                }
                            }

                            // If no <img> or empty src, try to get background image from style
                            if (string.IsNullOrEmpty(imageUrl))
                            {
                                var style = await imageWrapElement.GetAttributeAsync("style");
                                if (!string.IsNullOrEmpty(style) && style.Contains("background-image"))
                                {
                                    var match = System.Text.RegularExpressions.Regex.Match(style, "url\\(\\\"(?<url>[^\\\"]+)\\\"\\)");
                                    if (match.Success)
                                    {
                                        imageUrl = match.Groups["url"].Value;
                                        _logger.LogInformation("Found background image URL: {ImageUrl}", imageUrl);
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogInformation("Error extracting image: {Error}", ex.Message);
                    }

                    // Download image if enabled and URL found
                    string? imageLocation = null;
                    if (downloadImages && !string.IsNullOrEmpty(imageUrl))
                    {
                        _logger.LogInformation("Downloading image for product: {ProductName}", productName);
                        var productNameHash = _utilityService.GenerateHash(productName);
                        var imageResult = await _utilityService.DownloadImageAsync(imageUrl, productNameHash, "images/metro");
                        if (imageResult != null)
                        {
                            imageFilename = imageResult.Filename;
                            imageLocation = $"images/metro/{imageFilename}";
                            _logger.LogInformation("Image downloaded: {ImageFilename}", imageFilename);
                        }
                    }

                    // Generate unique product ID
                    var productId = Guid.NewGuid().ToString();

                    // Create product object
                    var product = new MetroProduct
                    {
                        ProductId = productId,
                        ProductCode = null,
                        ProductName = productName,
                        ProductDescription = productName,
                        ProductSize = null,
                        ProductPrice = productPrice,
                        ProductUrl = productUrl,
                        ImageName = imageFilename,
                        ImageUrl = imageUrl,
                        ImageLocation = imageLocation,
                        Category = categoryName,
                        Source = "metro",
                        NeedsGeminiProcessing = true,
                        ScrapedTimestamp = DateTime.UtcNow
                    };
                    products.Add(product);
                    _logger.LogInformation("Successfully extracted product: {ProductName}", productName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error extracting product {Index}", i + 1);
                }
            }
            _logger.LogInformation("Successfully extracted {Count} products from {CategoryName}", products.Count, categoryName);
            return products;
        }

        // Helper exception for returning ScrapingResult from a List-returning method
        private class StandaloneScrapingResultException : Exception
        {
            public ScrapingResult Result { get; }
            public StandaloneScrapingResultException(ScrapingResult result) => Result = result;
        }
    }
} 