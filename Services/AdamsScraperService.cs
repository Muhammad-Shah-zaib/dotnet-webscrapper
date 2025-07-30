using Microsoft.Playwright;
using System.Linq;

namespace WebScrapperApi.Services
{
    public class AdamsScraperService(UtilityService utilityService, LoggerService loggerService, ScraperDbContext dbContext)
    {
        private readonly UtilityService _utilityService = utilityService;
        private readonly LoggerService _loggerService = loggerService;
        private readonly ScraperDbContext _dbContext = dbContext;

        public async Task<ScrapingResult> ScrapeAllCategoriesAsync(ScrapingOptions options)
        {
            var startTime = DateTime.UtcNow;
            var allProducts = new List<AdamsProduct>();
            var statistics = new ScrapingStatistics();

            // MongoDB setup
            var mongoEnabled = false;

            try
            {
                await _dbContext.ConnectAsync();
                mongoEnabled = true;
                _loggerService.Log("Adams", LogLevel.Information, "MongoDB connection established using ScraperDbContext");
            }
            catch (Exception ex)
            {
                _loggerService.Log("Adams", LogLevel.Error, $"Failed to connect to MongoDB. Proceeding without database integration. - Exception: {ex.Message}");
                mongoEnabled = false;
            }

            // Initialize Playwright
            var playwright = await Playwright.CreateAsync();
            var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = options.Headless
            });

            try
            {
                // Process each category
                foreach (var category in AdamsConfig.ADAMS_CATEGORIES)
                {
                    _loggerService.Log("Adams", LogLevel.Information, $"Processing Adams category: {category.Name}");

                    try
                    {
                        var categoryProducts = await ScrapeCategoryAsync(MapModels.ScrapingOptions(options), category, browser);

                        allProducts.AddRange(categoryProducts);
                        statistics.CategoriesProcessed.Add(category.Name);
                        statistics.TotalProcessed += categoryProducts.Count;

                        // Save to MongoDB if enabled
                        if (mongoEnabled)
                        {
                            var mongoStats = await _dbContext.SaveAdamsProductsAsync(categoryProducts);
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
                        _loggerService.Log("Adams", LogLevel.Error, $"Error scraping Adams category {category.Name} - Exception: {ex.Message}");
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
                    Scraper = "adams",
                    Message = "Adams scraping completed successfully",
                    Timestamp = DateTime.UtcNow,
                    TotalProducts = allProducts.Count,
                    DownloadImagesEnabled = options.DownloadImages,
                    OutputFile = options.OutputFile,
                    MongoDbEnabled = mongoEnabled,
                    Statistics = statistics,
                    OutputPath = outputPath,
                    AdamsProducts = allProducts
                };
            }
            catch (Exception ex)
            {
                _loggerService.Log("Adams", LogLevel.Critical, $"Error during Adams scraping process - Exception: {ex.Message}");
                throw;
            }
        }

        public async Task<List<AdamsProduct>> ScrapeCategoryAsync(ScrapingOptions options, Category category, IBrowser? existingBrowser = null)
        {
            _loggerService.Log("Adams", LogLevel.Information, $"Starting to scrape Adams category: {category.Name}");
            _loggerService.Log("Adams", LogLevel.Information, $"Category URL: {category.Url}");

            var startTime = DateTime.UtcNow;
            var products = new List<AdamsProduct>();

            // MongoDB setup
            var mongoEnabled = false;

            try
            {
                await _dbContext.ConnectAsync();
                mongoEnabled = true;
                _loggerService.Log("Adams", LogLevel.Information, "MongoDB connection established using ScraperDbContext");
            }
            catch (Exception ex)
            {
                _loggerService.Log("Adams", LogLevel.Error, $"Failed to connect to MongoDB. Proceeding without database integration. - Exception: {ex.Message}");
                mongoEnabled = false;
            }

            IBrowser? browser = null;
            IBrowserContext? context = null;
            IPage? page = null;

            try
            {
                // Initialize Playwright if not provided
                if (existingBrowser == null)
                {
                    var playwright = await Playwright.CreateAsync();
                    browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
                    {
                        Headless = options.Headless,
                        SlowMo = 50 // Add slowMo for debugging
                    });
                }
                else
                {
                    browser = existingBrowser;
                }

                // Create browser context
                context = await browser.NewContextAsync(new BrowserNewContextOptions
                {
                    ViewportSize = new ViewportSize { Width = 1280, Height = 720 }
                });

                // Create page
                page = await context.NewPageAsync();

                // Navigate to category page
                _loggerService.Log("Adams", LogLevel.Information, $"Navigating to category URL: {category.Url}");
                await page.GotoAsync(category.Url, new PageGotoOptions { Timeout = 60000 });

                // Wait for the page to fully load
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions { Timeout = 30000 });
                _loggerService.Log("Adams", LogLevel.Information, "Page fully loaded");

                // Take a screenshot of the category page for debugging
                var screenshotDir = Path.Combine("screenshots", "adams");
                Directory.CreateDirectory(screenshotDir);
                var screenshotPath = Path.Combine(screenshotDir, $"adams-{category.Name}-page.png");
                await page.ScreenshotAsync(new PageScreenshotOptions 
                {
                    Path = screenshotPath, 
                    FullPage = true 
                });
                _loggerService.Log("Adams", LogLevel.Information, $"Category page screenshot saved as {screenshotPath}");

                // Try multiple selectors to find products
                var productsFound = false;
                var productElements = new List<IElementHandle>();

                var selectors = new[]
                {
                    AdamsConfig.AdamsSelectors.PRODUCT_LIST,
                    "ul.wp-block-post-template",
                    ".wp-block-woocommerce-product-collection",
                    ".products",
                    ".woocommerce-loop-product",
                    "li.wc-block-product",
                };

                foreach (var selector in selectors)
                {
                    try
                    {
                        _loggerService.Log("Adams", LogLevel.Information, $"Trying selector: {selector}");
                        await page.WaitForSelectorAsync(selector, new PageWaitForSelectorOptions { Timeout = 10000 });

                        // Get product items within this container
                        var itemSelectors = new[]
                        {
                            AdamsConfig.AdamsSelectors.PRODUCT_ITEM,
                            "li.wc-block-product",
                            ".woocommerce-loop-product",
                            "li.product",
                        };

                        foreach (var itemSelector in itemSelectors)
                        {
                            var elements = await page.QuerySelectorAllAsync(itemSelector);
                            if (elements.Count > 0)
                            {
                                productElements = elements.ToList();
                                _loggerService.Log("Adams", LogLevel.Information, $"Found {productElements.Count} products using selector: {itemSelector}");
                                productsFound = true;
                                break;
                            }
                        }

                        if (productsFound) break;
                    }
                    catch (Exception ex)
                    {
                        _loggerService.Log("Adams", LogLevel.Warning, $"Selector {selector} failed: {ex.Message}");
                    }
                }

                if (!productsFound || productElements.Count == 0)
                {
                    _loggerService.Log("Adams", LogLevel.Warning, "No products found with any selector. Checking page content...");
                    var pageContent = await page.ContentAsync();
                    var htmlDebugDir = Path.Combine("screenshots", "adams");
                    Directory.CreateDirectory(htmlDebugDir);
                    var htmlDebugPath = Path.Combine(htmlDebugDir, $"adams-{category.Name}-content.html");
                    await File.WriteAllTextAsync(htmlDebugPath, pageContent);
                    _loggerService.Log("Adams", LogLevel.Information, $"Page content saved to {htmlDebugPath}");

                    // Return empty array instead of throwing error
                    return new List<AdamsProduct>();
                }

                _loggerService.Log("Adams", LogLevel.Information, $"Found {productElements.Count} product elements, extracting data...");

                // Extract products from current page
                var pageProducts = await ExtractProductsFromPageAsync(page, category.Name, category.Url, options.DownloadImages, productElements);
                products.AddRange(pageProducts);

                _loggerService.Log("Adams", LogLevel.Information, $"Extracted {pageProducts.Count} valid products from {category.Name}");

                // Try to find and click "Load More" button
                try
                {
                    var loadMoreButton = await page.QuerySelectorAsync(AdamsConfig.AdamsSelectors.LOAD_MORE_BUTTON);
                    var loadMoreCount = 0;
                    const int MAX_LOAD_MORE = 20; // Reduced limit

                    while (loadMoreButton != null && loadMoreCount < MAX_LOAD_MORE)
                    {
                        _loggerService.Log("Adams", LogLevel.Information, $"Clicking load more button (attempt {loadMoreCount + 1})");
                        await loadMoreButton.ClickAsync();
                        // wait for network idle state to ensure new products are loaded
                        await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions { Timeout = 30000 });

                        // Get new product elements
                        // wait for the new product elements to appear
                        _loggerService.Log("Adams", LogLevel.Information, "Checking for new products after load more button click...");
                        await page.WaitForSelectorAsync(AdamsConfig.AdamsSelectors.PRODUCT_ITEM, new PageWaitForSelectorOptions { Timeout = 10000 });
                        var newProductElements = await page.QuerySelectorAllAsync(AdamsConfig.AdamsSelectors.PRODUCT_ITEM);
                        if (newProductElements.Count > 0)
                        {
                            var newElements = newProductElements.ToList();
                            var newProducts = await ExtractProductsFromPageAsync(
                                page,
                                category.Name,
                                category.Url,
                                options.DownloadImages,
                                newElements);
                            products.AddRange(newProducts);
                            productElements = newProductElements.ToList();

                            _loggerService.Log("Adams", LogLevel.Information, $"Loaded {newProducts.Count} additional products");
                        }

                        // Check if there's still a "Load More" button
                        loadMoreButton = await page.QuerySelectorAsync(AdamsConfig.AdamsSelectors.LOAD_MORE_BUTTON);
                        loadMoreCount++;
                    }
                }
                catch (Exception ex)
                {
                    _loggerService.Log("Adams", LogLevel.Warning, $"Load more functionality failed: {ex.Message}");
                }

                // Save to MongoDB if enabled
                var mongoStats = new MongoStats();
                if (mongoEnabled && products.Count > 0)
                {
                    _loggerService.Log("Adams", LogLevel.Information, $"Saving {products.Count} products to MongoDB...");
                    mongoStats = await _dbContext.SaveAdamsProductsAsync(products);
                    _loggerService.Log("Adams", LogLevel.Information, $"MongoDB save stats: {System.Text.Json.JsonSerializer.Serialize(mongoStats)}");
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

                    _loggerService.Log("Adams", LogLevel.Information, $"Adams category scraping completed successfully for category: {category.Name}");
                }

                return products;
            }
            finally
            {
                if (page != null)
                {
                    await page.CloseAsync();
                }

                if (context != null)
                {
                    await context.CloseAsync();
                }

                if (existingBrowser == null && browser != null)
                {
                    await browser.DisposeAsync();
                }

                if (mongoEnabled && existingBrowser == null)
                {
                    _dbContext.Disconnect();
                }
            }
        }

        private async Task<List<AdamsProduct>> ExtractProductsFromPageAsync(IPage page, string categoryName, string categoryUrl, bool downloadImages, List<IElementHandle> productElements)
        {
            _loggerService.Log("Adams", LogLevel.Information, $"Extracting products from page for category: {categoryName}");
            var products = new List<AdamsProduct>();
            var seenProducts = new HashSet<string>();

            _loggerService.Log("Adams", LogLevel.Information, $"Processing {productElements.Count} product elements");

            for (int i = 0; i < productElements.Count; i++)
            {
                var productElement = productElements[i];

                try
                {
                    _loggerService.Log("Adams", LogLevel.Information, $"Processing product {i + 1} of {productElements.Count}");

                    // Try multiple selectors for product name
                    IElementHandle? nameElement = null;
                    var name = "Unknown Product";

                    var nameSelectors = new[]
                    {
                        AdamsConfig.AdamsSelectors.PRODUCT_NAME_RELATIVE,
                        "h6 a",
                        ".wc-block-components-product-name a",
                        "h3 a",
                        "a[href*=\"/product/\"]",
                    };

                    foreach (var selector in nameSelectors)
                    {
                        try
                        {
                            nameElement = await productElement.QuerySelectorAsync(selector);
                            if (nameElement != null)
                            {
                                name = await nameElement.InnerTextAsync();
                                if (!string.IsNullOrEmpty(name) && name.Trim() != "")
                                {
                                    _loggerService.Log("Adams", LogLevel.Information, $"Found product name using selector {selector}: {name}");
                                    break;
                                }
                            }
                        }
                        catch (Exception)
                        {
                            // Continue to next selector
                        }
                    }

                    // Skip if we couldn't get a valid name or if it's a duplicate
                    if (string.IsNullOrEmpty(name) || name == "Unknown Product" || seenProducts.Contains(name))
                    {
                        _loggerService.Log("Adams", LogLevel.Information, $"Skipping product {i + 1}: {(name == "Unknown Product" ? "no valid name" : "duplicate")}");
                        continue;
                    }

                    seenProducts.Add(name);

                    // Get product URL
                    var url = "";
                    if (nameElement != null)
                    {
                        try
                        {
                            url = await nameElement.GetAttributeAsync("href");
                            if (!string.IsNullOrEmpty(url) && !url.StartsWith("http"))
                            {
                                url = $"https://adamsfoodservice.com{url}";
                            }
                        }
                        catch (Exception)
                        {
                            _loggerService.Log("Adams", LogLevel.Warning, $"Could not get URL for product: {name}");
                        }
                    }

                    // Try multiple selectors for SKU
                    var sku = "";
                    var skuSelectors = new[]
                    {
                        AdamsConfig.AdamsSelectors.PRODUCT_SKU_RELATIVE,
                        ".wc-block-components-product-sku span.sku",
                        ".sku",
                        "[class*=\"sku\"]",
                    };

                    foreach (var selector in skuSelectors)
                    {
                        try
                        {
                            var skuElement = await productElement.QuerySelectorAsync(selector);
                            if (skuElement != null)
                            {
                                sku = await skuElement.InnerTextAsync();
                                if (!string.IsNullOrEmpty(sku) && sku.Trim() != "")
                                {
                                    _loggerService.Log("Adams", LogLevel.Information, $"Found SKU using selector {selector}: {sku}");
                                    break;
                                }
                            }
                        }
                        catch (Exception)
                        {
                            // Continue to next selector
                        }
                    }

                    // Try multiple selectors for image
                    var imageUrl = "";
                    var imageSelectors = new[]
                    {
                        AdamsConfig.AdamsSelectors.PRODUCT_IMAGE_RELATIVE,
                        "img",
                        ".wc-block-components-product-image img",
                    };

                    foreach (var selector in imageSelectors)
                    {
                        try
                        {
                            var imageElement = await productElement.QuerySelectorAsync(selector);
                            if (imageElement != null)
                            {
                                imageUrl = await imageElement.GetAttributeAsync("src");
                                if (!string.IsNullOrEmpty(imageUrl))
                                {
                                    _loggerService.Log("Adams", LogLevel.Information, $"Found image using selector {selector}");
                                    break;
                                }
                            }
                        }
                        catch (Exception)
                        {
                            // Continue to next selector
                        }
                    }

                    // Generate hash for the product
                    var productNameHash = _utilityService.GenerateHash(name);

                    // Download image if enabled
                    string? localImageFilename = null;
                    if (downloadImages && !string.IsNullOrEmpty(imageUrl))
                    {
                        _loggerService.Log("Adams", LogLevel.Information, $"Downloading image for product: {name}");
                        var imageResult = await _utilityService.DownloadImageAsync(imageUrl, productNameHash, "images/adams");
                        if (imageResult != null)
                        {
                            localImageFilename = imageResult.Filename;
                            _loggerService.Log("Adams", LogLevel.Information, $"Image downloaded: {localImageFilename}");
                        }
                    }

                    // Create product object
                    var product = new AdamsProduct
                    {
                        ProductId = _utilityService.GenerateUUID(),
                        Name = name,
                        Sku = sku,
                        ImageUrlScraped = imageUrl,
                        ImageFilenameLocal = localImageFilename,
                        Category = categoryName,
                        ProductPageUrl = url,
                        ScrapedFromCategoryPageUrl = categoryUrl,
                        Source = "AdamsFoodService_Standalone_Mongo",
                        ScrapedTimestamp = DateTime.UtcNow
                    };

                    products.Add(product);
                    _loggerService.Log("Adams", LogLevel.Information, $"Successfully extracted product: {name}");
                }
                catch (Exception ex)
                {
                    _loggerService.Log("Adams", LogLevel.Error, $"Error extracting product {i + 1} - Exception: {ex.Message}");
                }
            }

            _loggerService.Log("Adams", LogLevel.Information, $"Successfully extracted {products.Count} products from {categoryName}");
            return products;
        }
    }
}