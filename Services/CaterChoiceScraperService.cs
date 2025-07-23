
using Microsoft.Extensions.Options;

namespace WebScrapperApi.Services
{
    public class CaterChoiceScraperService(UtilityService utilityService, ILogger<CaterChoiceScraperService> logger, ScraperDbContext dbContext, IOptionsMonitor<ScraperCredentialsConfig> credentialsMonitor)
    {
        private readonly UtilityService _utilityService = utilityService;
        private readonly ILogger<CaterChoiceScraperService> _logger = logger;
        private readonly ScraperDbContext _dbContext = dbContext;
        private readonly ScraperCredentialsConfig _credentials = credentialsMonitor.CurrentValue;

        public async Task<ScrapingResult> ScrapeAllCategoriesAsync(ScrapingOptions options)
        {
            var startTime = DateTime.UtcNow;
            var allProducts = new List<CaterChoiceProduct>();
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
                Headless = options.Headless,
                Args = new[] { "--ignore-certificate-errors" }
            });

            try
            {
                // Process each category
                foreach (var category in CaterChoiceConfig.CATER_CHOICE_CATEGORIES)
                {
                    _logger.LogInformation("Processing category: {CategoryName}", category.Name);

                    try
                    {
                        var categoryProducts = await ScrapeCategoryAsync(new ScrapingOptions
                        {
                            Headless = options.Headless,
                            DownloadImages = options.DownloadImages,
                            StoreInMongoDB = options.StoreInMongoDB
                        }, category, browser);

                        allProducts.AddRange(categoryProducts);
                        statistics.CategoriesProcessed.Add(category.Name);
                        statistics.TotalProcessed += categoryProducts.Count;

                        // Save to MongoDB if enabled
                        if (mongoEnabled)
                        {
                            var mongoStats = await _dbContext.SaveCaterChoiceProductsAsync(categoryProducts);
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
                    catch ( Exception ex)
                    {
                        _logger.LogError(ex, "Error scraping category {CategoryName}", category.Name);
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
                    Scraper = "cater-choice",
                    Message = "Scraping completed successfully",
                    Timestamp = DateTime.UtcNow,
                    TotalProducts = allProducts.Count,
                    DownloadImagesEnabled = options.DownloadImages,
                    OutputFile = options.OutputFile,
                    MongoDbEnabled = mongoEnabled,
                    Statistics = statistics,
                    OutputPath = outputPath,
                    Products = allProducts
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during scraping process");
                throw;
            }
        }

        public async Task<List<CaterChoiceProduct>> ScrapeCategoryAsync(ScrapingOptions options, Category category, IBrowser? existingBrowser = null)
        {
            _logger.LogInformation("Starting to scrape Cater Choice category: {CategoryName}", category.Name);
            _logger.LogInformation("Category URL: {CategoryUrl}", category.Url);

            var startTime = DateTime.UtcNow;
            var products = new List<CaterChoiceProduct>();

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
                        SlowMo = 50,
                        Args = new[] { "--ignore-certificate-errors" }
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

                // Login if credentials provided
                if (options.UseCredentials)
                {
                    _logger.LogInformation("Using credentials, attempting login...");
                    await LoginAsync(page, _credentials.Email, _credentials.Password);
                }
                else
                {
                    _logger.LogInformation("No credentials provided, skipping login");
                }

                // Navigate to category page
                _logger.LogInformation("Navigating to category URL: {CategoryUrl}", category.Url);
                await page.GotoAsync(category.Url, new PageGotoOptions { Timeout = 60000 });

                // Wait for the page to fully load
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions { Timeout = 30000 });
                _logger.LogInformation("Page fully loaded");

                // Take a screenshot of the category page for debugging
                var screenshotDir = Path.Combine("screenshots", "caterchoice");
                Directory.CreateDirectory(screenshotDir);
                var screenshotPath = Path.Combine(screenshotDir, $"caterchoice-{category.Name}-page.png");
                await page.ScreenshotAsync(new PageScreenshotOptions 
                { 
                    Path = screenshotPath, 
                    FullPage = true 
                });
                _logger.LogInformation("Category page screenshot saved as {ScreenshotPath}", screenshotPath);

                // Extract products from current page
                _logger.LogInformation("Extracting products from current page...");
                var pageProducts = await ExtractProductsFromPageAsync(page, category.Name, options.DownloadImages);

                // Filter out "Unknown Product" entries
                var validProducts = pageProducts.Where(p => p.ProductName != "Unknown Product").ToList();
                _logger.LogInformation("Found {TotalProducts} products, {ValidProducts} valid products after filtering", 
                    pageProducts.Count, validProducts.Count);

                products.AddRange(validProducts);

                // Pagination logic: click next page and scrape recursively
                while (true)
                {
                    _logger.LogInformation("Checking for next page...");
                    var nextPageButton = await page.QuerySelectorAsync("ul.pagination li.page-item:not(.disabled) > a.page-link:has-text(\"›\")");
                    if (nextPageButton != null)
                    {
                        _logger.LogInformation("Next page button found, clicking to go to next page...");
                        await nextPageButton.ClickAsync(new ElementHandleClickOptions { Force = true });
                        await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions { Timeout = 30000 });
                        _logger.LogInformation("Next page loaded, extracting products...");
                        var nextPageProducts = await ExtractProductsFromPageAsync(page, category.Name, options.DownloadImages);
                        var nextValidProducts = nextPageProducts.Where(p => p.ProductName != "Unknown Product").ToList();
                        _logger.LogInformation("Found {TotalProducts} products, {ValidProducts} valid products after filtering on next page", 
                            nextPageProducts.Count, nextValidProducts.Count);
                        products.AddRange(nextValidProducts);
                    }
                    else
                    {
                        _logger.LogInformation("No next page button found, finished pagination.");
                        break;
                    }
                }

                _logger.LogInformation("Finished scraping category {CategoryName}, found {ProductCount} valid products total", 
                    category.Name, products.Count);

                // Save to MongoDB if enabled
                MongoStats mongoStats = new();
                if (mongoEnabled && products.Count > 0)
                {
                    _logger.LogInformation("Saving {ProductCount} products to MongoDB...", products.Count);
                    mongoStats = await _dbContext.SaveCaterChoiceProductsAsync(products);
                    _logger.LogInformation("MongoDB stats: {Stats}", System.Text.Json.JsonSerializer.Serialize(mongoStats));
                }

                // If this is a standalone category scrape (not part of scrapeAllCategories)
                if (!string.IsNullOrEmpty(options.OutputFile))
                {
                    var endTime = DateTime.UtcNow;
                    var processingTimeSeconds = (endTime - startTime).TotalSeconds;
                    _logger.LogInformation("Processing completed in {ProcessingTime} seconds", processingTimeSeconds);

                    var statistics = new ScrapingStatistics
                    {
                        TotalProcessed = products.Count,
                        NewRecordsAdded = mongoEnabled ? mongoStats.NewRecordsAdded : products.Count,
                        ExistingRecordsUpdated = mongoStats.ExistingRecordsUpdated,
                        RecordsUnchanged = mongoStats.RecordsUnchanged,
                        Errors = mongoStats.Errors,
                        CategoriesProcessed = new List<string> { category.Name },
                        TotalProcessingTimeSeconds = processingTimeSeconds,
                        ProcessingTimeFormatted = _utilityService.FormatProcessingTime(processingTimeSeconds),
                    };

                    // Save results to file
                    _logger.LogInformation("Saving results to file: {OutputFile}", options.OutputFile);
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
                    _logger.LogInformation("Results saved to: {OutputPath}", outputPath);

                    // Disconnect from MongoDB if connected
                    if (mongoEnabled)
                    {
                        _logger.LogInformation("Disconnecting from MongoDB...");
                        _dbContext.Disconnect();
                        _logger.LogInformation("MongoDB disconnected");
                    }
                }

                return products;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scraping category {CategoryName}", category.Name);
                throw;
            }
            finally
            {
                // Clean up resources
                page?.CloseAsync();
                context?.CloseAsync();
                
                // Only close browser if we created it (not if it was passed in)
                if (browser != null && existingBrowser == null)
                {
                    await browser.CloseAsync();
                }

                if (mongoEnabled && !string.IsNullOrEmpty(options.OutputFile))
                {
                    _logger.LogInformation("Disconnecting from MongoDB...");
                    _dbContext.Disconnect();
                    _logger.LogInformation("MongoDB disconnected");
                }
            }
        }

        private async Task<List<CaterChoiceProduct>> ExtractProductsFromPageAsync(IPage page, string categoryName, bool downloadImages)
        {
            _logger.LogInformation("Extracting products from page for category: {CategoryName}", categoryName);
            var products = new List<CaterChoiceProduct>();

            // wait for main product grid to load
            try
            {
                await page.WaitForSelectorAsync(CaterChoiceConfig.CaterChoiceSelectors.PRODUCT_GRID, new PageWaitForSelectorOptions { Timeout = 10000 });
                _logger.LogInformation("Main product Grid found");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error waiting for main product grid to load, continuing with product extraction");
            }            

            // Try to find product containers using multiple selectors
            var productContainerSelectors = new[]
            {
                CaterChoiceConfig.CaterChoiceSelectors.PRODUCT_CONTAINER,
                "div.gridcontroll",
                "[class*='gridcontroll']",
                ".product-item",
                ".product",
            };

            IElementHandle[]? productElements = null;
            foreach (var selector in productContainerSelectors)
            {
                try
                {
                    _logger.LogInformation("Trying product container selector: {Selector}", selector);
                    await page.WaitForSelectorAsync(selector, new PageWaitForSelectorOptions { Timeout = 10000 });
                    productElements = (await page.QuerySelectorAllAsync(selector)).ToArray();
                    if (productElements.Length > 0)
                    {
                        _logger.LogInformation("Found {Count} product containers using selector: {Selector}", 
                            productElements.Length, selector);
                        break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Selector {Selector} failed: {Error}", selector, ex.Message);
                }
            }

            // If still no products found, try XPath as last resort
            if (productElements == null || productElements.Length == 0)
            {
                _logger.LogInformation("Trying XPath selector as last resort...");
                try
                {
                    var xpathElements = await page.Locator(CaterChoiceConfig.CaterChoiceSelectors.PRODUCT_ITEM_XPATH).AllAsync();
                    if (xpathElements.Count > 0)
                    {
                        productElements = (await Task.WhenAll(xpathElements.Select(e => e.ElementHandleAsync())))
                        .Where(e => e != null)
                        .ToArray();
                        _logger.LogInformation("Found {Count} products using XPath", productElements.Length);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("XPath selector failed: {Error}", ex.Message);
                }
            }

            if (productElements == null || productElements.Length == 0)
            {
                _logger.LogWarning("No product elements found, saving page content for debugging...");
                var pageContent = await page.ContentAsync();
                var debugDir = Path.Combine("screenshots", "caterchoice");
                Directory.CreateDirectory(debugDir);
                var debugFilePath = Path.Combine(debugDir, $"caterchoice-{categoryName}-debug.html");
                await File.WriteAllTextAsync(debugFilePath, pageContent);
                _logger.LogInformation("Debug HTML saved to {FilePath}", debugFilePath);
                return products;
            }

            _logger.LogInformation("Processing {Count} product elements", productElements.Length);

            for (int i = 0; i < productElements.Length; i++)
            {
                var productElement = productElements[i];
                try
                {
                    _logger.LogInformation("Processing product {Index} of {Total}", i + 1, productElements.Length);

                    // Extract product details using updated selectors
                    var name = "Unknown Product";
                    var url = "";
                    var imageUrl = "";
                    var packSize = "";
                    var singlePrice = "";
                    var casePrice = "";

                    // Product Name and URL - try multiple approaches
                    var nameSelectors = new[]
                    {
                        CaterChoiceConfig.CaterChoiceSelectors.PRODUCT_NAME, // main product name selector
                        "h3 a",
                        ".text-center a",
                        "a[href*='/product/']",
                    };

                    foreach (var selector in nameSelectors)
                    {
                        try
                        {
                            var nameElement = await productElement.QuerySelectorAsync(selector);
                            if (nameElement != null)
                            {
                                name = await nameElement.TextContentAsync() ?? "Unknown Product";
                                url = await nameElement.GetAttributeAsync("href") ?? "";
                                if (!string.IsNullOrEmpty(name) && name != "Unknown Product")
                                {
                                    _logger.LogInformation("Found name: {Name}", name);
                                    if (!string.IsNullOrEmpty(url) && !url.StartsWith("http"))
                                    {
                                        url = $"{CaterChoiceConfig.CATER_CHOICE_BASE_URL.TrimEnd('/')}{url}";
                                    }
                                    _logger.LogInformation("Found URL: {Url}", url);
                                    break;
                                }
                            }
                        }
                        catch (Exception)
                        {
                            // Continue to next selector
                        }
                    }

                    if (string.IsNullOrEmpty(name) || name == "Unknown Product")
                    {
                        _logger.LogInformation("Skipping product {Index}: no valid name found.", i + 1);
                        continue;
                    }

                    // Image URL - try multiple approaches
                    var imageSelectors = new[]
                    {
                        CaterChoiceConfig.CaterChoiceSelectors.PRODUCT_IMAGE,
                        "img",
                        ".product-image img",
                        "[class*='mb-'] img",
                    };

                    foreach (var selector in imageSelectors)
                    {
                        try
                        {
                            var imageElement = await productElement.QuerySelectorAsync(selector);
                            if (imageElement != null)
                            {
                                imageUrl = await imageElement.GetAttributeAsync("src") ?? "";
                                if (!string.IsNullOrEmpty(imageUrl))
                                {
                                    _logger.LogInformation("Found image URL: {ImageUrl}...", 
                                        imageUrl.Length > 50 ? imageUrl.Substring(0, 50) : imageUrl);
                                    break;
                                }
                            }
                        }
                        catch (Exception)
                        {
                            // Continue to next selector
                        }
                    }

                    // Pack Size
                    var packSizeSelectors = new[]
                    {
                        CaterChoiceConfig.CaterChoiceSelectors.PACK_SIZE,
                        "div.truncate strong",
                        ".truncate strong",
                        ".pack-size",
                    };

                    foreach (var selector in packSizeSelectors)
                    {
                        try
                        {
                            var packSizeElement = await productElement.QuerySelectorAsync(selector);
                            if (packSizeElement != null)
                            {
                                packSize = await packSizeElement.TextContentAsync() ?? "";
                                if (!string.IsNullOrEmpty(packSize))
                                {
                                    _logger.LogInformation("Found pack size: {PackSize}", packSize);
                                    break;
                                }
                            }
                        }
                        catch (Exception)
                        {
                            // Continue to next selector
                        }
                    }

                    // Case Price
                    var casePriceSelectors = new[]
                    {
                        CaterChoiceConfig.CaterChoiceSelectors.CASE_PRICE,
                        ".custom_design_hm div:first-child strong",
                        ".price-case strong",
                        ".case-price",
                    };

                    foreach (var selector in casePriceSelectors)
                    {
                        try
                        {
                            var casePriceElement = await productElement.QuerySelectorAsync(selector);
                            if (casePriceElement != null)
                            {
                                casePrice = await casePriceElement.TextContentAsync() ?? "";
                                if (!string.IsNullOrEmpty(casePrice))
                                {
                                    _logger.LogInformation("Found case price: {CasePrice}", casePrice);
                                    break;
                                }
                            }
                        }
                        catch (Exception)
                        {
                            // Continue to next selector
                        }
                    }

                    // Single Price
                    var singlePriceSelectors = new[]
                    {
                        CaterChoiceConfig.CaterChoiceSelectors.SINGLE_PRICE,
                        ".custom_design_hm div:nth-child(2) strong",
                        ".price-single strong",
                        ".single-price",
                    };

                    foreach (var selector in singlePriceSelectors)
                    {
                        try
                        {
                            var singlePriceElement = await productElement.QuerySelectorAsync(selector);
                            if (singlePriceElement != null)
                            {
                                singlePrice = await singlePriceElement.TextContentAsync() ?? "";
                                if (!string.IsNullOrEmpty(singlePrice))
                                {
                                    _logger.LogInformation("Found single price: {SinglePrice}", singlePrice);
                                    break;
                                }
                            }
                        }
                        catch (Exception)
                        {
                            // Continue to next selector
                        }
                    }

                    // Generate a unique ID for the product
                    var productId = _utilityService.GenerateUUID();
                    var productNameHash = _utilityService.GenerateHash(name);

                    // Download image if enabled
                    string? localImageFilename = null;
                    string? localImageFilepath = null;

                    if (downloadImages && !string.IsNullOrEmpty(imageUrl))
                    {
                        _logger.LogInformation("Downloading image for product: {Name}", name);
                        var imageResult = await _utilityService.DownloadImageAsync(imageUrl, productNameHash, "images/cater-choice");
                        if (imageResult != null)
                        {
                            localImageFilename = imageResult.Filename;
                            localImageFilepath = imageResult.FilePath;
                            _logger.LogInformation("Image downloaded: {Filename}", localImageFilename);
                        }
                        else
                        {
                            _logger.LogWarning("Failed to download image");
                        }
                    }

                    // Get product code and description by visiting product page
                    var productCode = "";
                    var productDescription = "";

                    if (!string.IsNullOrEmpty(url))
                    {
                        try
                        {
                            _logger.LogInformation("Visiting product page for code/description: {Url}", url);
                            var productPage = await page.Context.NewPageAsync();
                            await productPage.GotoAsync(url, new PageGotoOptions { Timeout = 30000 });
                            await productPage.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions { Timeout = 15000 });
                            _logger.LogInformation("Product page loaded");

                            // Product Code - try multiple selectors
                            var codeSelectors = new[]
                            {
                                CaterChoiceConfig.CaterChoiceSelectors.PRODUCT_CODE,
                                ".product-code",
                                ".sku_wrapper .sku",
                                ".product_meta .sku",
                                "span.sku",
                            };

                            foreach (var selector in codeSelectors)
                            {
                                try
                                {
                                    var codeElement = await productPage.QuerySelectorAsync(selector);
                                    if (codeElement != null)
                                    {
                                        var fullText = await codeElement.TextContentAsync() ?? "";
                                        productCode = fullText.Replace("Product Code:", "").Replace(" ", "").Trim();
                                        if (!string.IsNullOrEmpty(productCode))
                                        {
                                            _logger.LogInformation("Found product code: {ProductCode}", productCode);
                                            break;
                                        }
                                    }
                                }
                                catch (Exception)
                                {
                                    // Continue to next selector
                                }
                            }

                            // Product Description
                            var descSelectors = new[]
                            {
                                CaterChoiceConfig.CaterChoiceSelectors.PRODUCT_DESCRIPTION,
                                ".product-description",
                                "div[itemprop='description']",
                                ".entry-summary .summary",
                            };

                            foreach (var selector in descSelectors)
                            {
                                try
                                {
                                    var descElement = await productPage.QuerySelectorAsync(selector);
                                    if (descElement != null)
                                    {
                                        productDescription = await descElement.TextContentAsync() ?? "";
                                        if (!string.IsNullOrEmpty(productDescription))
                                        {
                                            _logger.LogInformation("Found product description: {Description}...", 
                                                productDescription.Length > 50 ? productDescription.Substring(0, 50) : productDescription);
                                            break;
                                        }
                                    }
                                }
                                catch (Exception)
                                {
                                    // Continue to next selector
                                }
                            }

                            await productPage.CloseAsync();
                            _logger.LogInformation("Product page closed");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error visiting product page {Url}", url);
                        }
                    }

                    // Create product object matching the expected MongoDB schema for CaterChoice
                    var product = new CaterChoiceProduct
                    {
                        ProductId = productId,
                        ProductCode = string.IsNullOrEmpty(productCode) ? null : productCode,
                        ProductName = string.IsNullOrEmpty(name) ? null : name,
                        ProductDescription = string.IsNullOrEmpty(productDescription) ? null : productDescription,
                        ProductSize = string.IsNullOrEmpty(packSize) ? null : packSize,
                        ProductSinglePrice = string.IsNullOrEmpty(singlePrice) ? null : singlePrice,
                        ProductCasePrice = string.IsNullOrEmpty(casePrice) ? null : casePrice,
                        ProductUrl = string.IsNullOrEmpty(url) ? null : url,
                        OriginalImageUrl = string.IsNullOrEmpty(imageUrl) ? null : imageUrl,
                        LocalImageFilename = localImageFilename,
                        LocalImageFilepath = localImageFilepath,
                        Category = categoryName,
                        ScrapedTimestamp = DateTime.UtcNow,
                        Source = "CaterChoice_Standalone_Mongo",
                    };

                    products.Add(product);
                    _logger.LogInformation("Product {Name} added to results", name);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error extracting product {Index}", i + 1);
                }
            }

            _logger.LogInformation("Extracted {Count} products from page", products.Count);
            return products;
        }

        private async Task LoginAsync(IPage page, string email, string password)
        {
            _logger.LogInformation("Starting login process for Cater Choice...");

            try
            {
                var loginUrl = $"{CaterChoiceConfig.CATER_CHOICE_BASE_URL.TrimEnd('/')}/customer/login";
                _logger.LogInformation("Navigating to {LoginUrl}", loginUrl);
                await page.GotoAsync(loginUrl);

                // Wait for page to fully load
                _logger.LogInformation("Waiting for page to fully load...");
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

                _logger.LogInformation("Waiting for login form to load...");
                await page.WaitForSelectorAsync("input[type=\"email\"]", new PageWaitForSelectorOptions { Timeout = 30000 });

                _logger.LogInformation("Filling email: {Email}...", email.Substring(0, Math.Min(3, email.Length)));
                await page.FillAsync("input[type=\"email\"]", email);

                _logger.LogInformation("Filling password: ********");
                await page.FillAsync("input[type=\"password\"]", password);

                // Take a screenshot of the login form for debugging
                var loginScreenshotDir = Path.Combine("screenshots", "caterchoice");
                Directory.CreateDirectory(loginScreenshotDir);
                var loginScreenshotPath = Path.Combine(loginScreenshotDir, "login-form.png");
                await page.ScreenshotAsync(new PageScreenshotOptions { Path = loginScreenshotPath, FullPage = true });
                _logger.LogInformation("Login form screenshot saved as {LoginScreenshotPath}", loginScreenshotPath);

                // Try multiple strategies to click the login button
                _logger.LogInformation("Attempting to click login button using multiple strategies...");

                // Strategy 1: Use locator with XPath
                _logger.LogInformation("Strategy 1: Using XPath with locator");
                try
                {
                    var buttonByXPath = page.Locator("xpath=/html/body/main/div[2]/div/div/form/button");
                    var isVisible = await buttonByXPath.IsVisibleAsync();

                    if (isVisible)
                    {
                        _logger.LogInformation("Button found by XPath, attempting to click...");
                        await buttonByXPath.ClickAsync(new LocatorClickOptions { Force = true, Timeout = 5000 });
                        _logger.LogInformation("Button clicked using XPath");

                        // Check if we've navigated away from the login page
                        await page.WaitForTimeoutAsync(2000);
                        var currentUrl = page.Url;
                        if (!currentUrl.Contains("login"))
                        {
                            _logger.LogInformation("Login successful, URL changed to: {Url}", currentUrl);
                            return; // Exit early if login was successful
                        }
                    }
                    else
                    {
                        _logger.LogInformation("Button not visible or not found by XPath");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("XPath strategy failed: {Error}", ex.Message);
                }

                // Strategy 2: Try submitting the form directly
                _logger.LogInformation("Strategy 2: Submitting form directly");
                try
                {
                    var submitted = await page.EvaluateAsync<bool>(@"
                        () => {
                            const form = document.querySelector('form');
                            if (form) {
                                form.submit();
                                return true;
                            }
                            return false;
                        }
                    ");
                    if (submitted)
                    {
                        _logger.LogInformation("Form submitted directly");

                        // Check if we've navigated away from the login page
                        await page.WaitForTimeoutAsync(2000);
                        var currentUrl = page.Url;
                        if (!currentUrl.Contains("login"))
                        {
                            _logger.LogInformation("Login successful, URL changed to: {Url}", currentUrl);
                            return; // Exit early if login was successful
                        }
                    }
                    else
                    {
                        _logger.LogInformation("No form found to submit");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Form submission failed: {Error}", ex.Message);
                }

                // Strategy 3: Try various CSS selectors
                _logger.LogInformation("Strategy 3: Trying various CSS selectors");
                var selectors = new[]
                {
                    "button[type=\"submit\"]",
                    "form button",
                    ".login",
                    "button.login",
                    "button.btn-primary",
                    "button:text(\"Login\")",
                    "button:text(\"Sign In\")",
                    "input[type=\"submit\"]",
                };

                foreach (var selector in selectors)
                {
                    try
                    {
                        _logger.LogInformation("Trying selector: {Selector}", selector);
                        var button = page.Locator(selector);
                        var isVisible = await button.IsVisibleAsync();

                        if (isVisible)
                        {
                            _logger.LogInformation("Button found with selector: {Selector}", selector);
                            await button.ClickAsync(new LocatorClickOptions { Force = true, Timeout = 5000 });
                            _logger.LogInformation("Button clicked using selector: {Selector}", selector);

                            // Check if we've navigated away from the login page
                            await page.WaitForTimeoutAsync(2000);
                            var currentUrl = page.Url;
                            if (!currentUrl.Contains("login"))
                            {
                                _logger.LogInformation("Login successful, URL changed to: {Url}", currentUrl);
                                return; // Exit early if login was successful
                            }

                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("Selector {Selector} failed: {Error}", selector, ex.Message);
                    }
                }

                // Strategy 4: Try pressing Enter in the password field
                _logger.LogInformation("Strategy 4: Pressing Enter in password field");
                try
                {
                    await page.FocusAsync("input[type=\"password\"]");
                    await page.Keyboard.PressAsync("Enter");
                    _logger.LogInformation("Enter key pressed in password field");

                    // Check if we've navigated away from the login page
                    await page.WaitForTimeoutAsync(2000);
                    var currentUrl = page.Url;
                    if (!currentUrl.Contains("login"))
                    {
                        _logger.LogInformation("Login successful, URL changed to: {Url}", currentUrl);
                        return; // Exit early if login was successful
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Enter key press failed: {Error}", ex.Message);
                }

                // Check if login was successful without waiting for navigation
                var finalUrl = page.Url;
                _logger.LogInformation("Current URL after login attempts: {Url}", finalUrl);

                if (finalUrl.Contains("login"))
                {
                    _logger.LogWarning("Login failed - still on login page");
                    throw new Exception("Login failed");
                }
                else
                {
                    _logger.LogInformation("Login successful");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Login error");
                // Take a screenshot of the error state
                var errorScreenshotDir = Path.Combine("screenshots", "caterchoice");
                Directory.CreateDirectory(errorScreenshotDir);
                var errorScreenshotPath = Path.Combine(errorScreenshotDir, "login-error.png");
                await page.ScreenshotAsync(new PageScreenshotOptions { Path = errorScreenshotPath, FullPage = true });
                _logger.LogInformation("Error state screenshot saved as {ErrorScreenshotPath}", errorScreenshotPath);
                throw;
            }
        }
    }
} 