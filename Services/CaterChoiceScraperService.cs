using Microsoft.Extensions.Options;
using System.Text;

namespace WebScrapperApi.Services;

public class CaterChoiceScraperService(
    UtilityService utilityService,
    ScraperDbContext dbContext,
    IOptionsMonitor<ScraperCredentialsConfig> credentialsMonitor,
    LoggerService loggerService)
{
    public readonly UtilityService UtilityService = utilityService;
    public readonly ScraperDbContext DbContext = dbContext;
    private readonly ScraperCredentialsConfig _credentials = credentialsMonitor.CurrentValue;
    private readonly LoggerService _loggerService = loggerService;

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
                await DbContext.ConnectAsync("caterchoice");
                mongoEnabled = true;
                _loggerService.Log("CaterChoice", LogLevel.Information, "MongoDB connection established using ScraperDbContext");
            }
            catch (Exception ex)
            {
                var errorMessage = "Failed to connect to MongoDB. Proceeding without database integration.";
                _loggerService.Log("CaterChoice", LogLevel.Error, $"{errorMessage} - Exception: {ex.Message}");
                mongoEnabled = false;
            }
        }
        else
        {
            _loggerService.Log("CaterChoice", LogLevel.Information, "MongoDB storage disabled by user preference");
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
                _loggerService.Log("CaterChoice", LogLevel.Information, $"Processing category: {category.Name}");

                try
                {
                    var categoryProducts = await ScrapeCategoryAsync(
                        MapModels.ScrapingOptions(options),
                        category,
                        browser,
                        manageMongo: false,
                        manageJson: false);


                    allProducts.AddRange(categoryProducts);
                    statistics.CategoriesProcessed.Add(category.Name);
                    statistics.TotalProcessed += categoryProducts.Count;

                    // Save to MongoDB if enabled
                    if (mongoEnabled)
                    {
                        var mongoStats = await DbContext.SaveCaterChoiceProductsAsync(categoryProducts);
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
                    var errorMessage = $"Error scraping category {category.Name}";
                    _loggerService.Log("CaterChoice", LogLevel.Error, $"{errorMessage} - Exception: {ex.Message}");
                    statistics.Errors++;
                }
            }

            // Calculate processing time
            var endTime = DateTime.UtcNow;
            var processingTimeSeconds = (endTime - startTime).TotalSeconds;
            statistics.TotalProcessingTimeSeconds = processingTimeSeconds;
            statistics.ProcessingTimeFormatted = UtilityService.FormatProcessingTime(processingTimeSeconds);

            // Save results to file
            var outputPath = UtilityService.SaveToJson(new
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
                DbContext.Disconnect();
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
            var errorMessage = "Error during scraping process";
            _loggerService.Log("CaterChoice", LogLevel.Critical, $"{errorMessage} - Exception: {ex.Message}");
            throw;
        }
    }

    public async Task<List<CaterChoiceProduct>> ScrapeCategoryAsync(ScrapingOptions options, Category category,
        IBrowser? existingBrowser = null, bool manageMongo = true, bool manageJson = true)
    {
        _loggerService.Log("CaterChoice", LogLevel.Information, $"Starting to scrape Cater Choice category: {category.Name}");
        _loggerService.Log("CaterChoice", LogLevel.Information, $"Category URL: {category.Url}");

        var startTime = DateTime.UtcNow;
        var products = new List<CaterChoiceProduct>();

        var email = _credentials.Email;
        var password = _credentials.Password;

        if (options.Email != string.Empty)
        {
            email = options.Email;
        }

        if (options.Password != string.Empty)
        {
            password = options.Password;
        }


        // MongoDB setup
        var mongoEnabled = false;

        if (options.StoreInMongoDB && manageMongo)
        {
            try
            {
                await DbContext.ConnectAsync("caterchoice");
                mongoEnabled = true;
                _loggerService.Log("CaterChoice", LogLevel.Information, "MongoDB connection established using ScraperDbContext");
            }
            catch (Exception ex)
            {
                var errorMessage = "Failed to connect to MongoDB. Proceeding without database integration.";
                _loggerService.Log("CaterChoice", LogLevel.Error, $"{errorMessage} - Exception: {ex.Message}");
                mongoEnabled = false;
            }
        }
        else
        {
            _loggerService.Log("CaterChoice", LogLevel.Information, "MongoDB storage disabled by user preference");
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
                _loggerService.Log("CaterChoice", LogLevel.Critical, email);
                _loggerService.Log("CaterChoice", LogLevel.Critical, password);
                _loggerService.Log("CaterChoice", LogLevel.Information, "Using credentials, attempting login...");
                await LoginAsync(page, email, password);
            }
            else
            {
                _loggerService.Log("CaterChoice", LogLevel.Information, "No credentials provided, skipping login");
            }

            // Navigate to category page
            _loggerService.Log("CaterChoice", LogLevel.Information, $"Navigating to category URL: {category.Url}");
            await page.GotoAsync(category.Url, new PageGotoOptions { Timeout = 60000 });

            // Wait for the page to fully load
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle,
                new PageWaitForLoadStateOptions { Timeout = 30000 });
            _loggerService.Log("CaterChoice", LogLevel.Information, "Page fully loaded");

            // Take a screenshot of the category page for debugging
            var screenshotDir = Path.Combine("screenshots", "caterchoice");
            Directory.CreateDirectory(screenshotDir);
            var screenshotPath = Path.Combine(screenshotDir, $"caterchoice-{category.Name}-page.png");
            await page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = screenshotPath,
                FullPage = true
            });
            _loggerService.Log("CaterChoice", LogLevel.Information, $"Category page screenshot saved as {screenshotPath}");

            // Extract products from current page
            _loggerService.Log("CaterChoice", LogLevel.Information, "Extracting products from current page...");
            var pageProducts = await ExtractProductsFromPageAsync(page, category.Name, options.DownloadImages);

            // Filter out "Unknown Product" entries
            var validProducts = pageProducts.Where(p => p.ProductName != "Unknown Product").ToList();
            _loggerService.Log("CaterChoice", LogLevel.Information, $"Found {pageProducts.Count} products, {validProducts.Count} valid products after filtering");

            products.AddRange(validProducts);

            // Pagination logic: click next page and scrape recursively
            int currentPage = 2;
            int maxPages = 30;

            while (currentPage <= maxPages)
            {
                var pageUrl = $"{category.Url}?page={currentPage}";
                _loggerService.Log("CaterChoice", LogLevel.Information, $"Navigating to page {currentPage}: {pageUrl}");

                await page.GotoAsync(pageUrl, new PageGotoOptions { Timeout = 60000 });
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle,
                    new PageWaitForLoadStateOptions { Timeout = 30000 });

                var productsOnPage =
                    await ExtractProductsFromPageAsync(page, category.Name, options.DownloadImages);
                var nextValidProducts = productsOnPage.Where(p => p.ProductName != "Unknown Product").ToList();

                if (nextValidProducts.Count == 0)
                {
                    _loggerService.Log("CaterChoice", LogLevel.Information, $"No products found on page {currentPage}, stopping pagination.");
                    break;
                }

                products.AddRange(nextValidProducts);
                _loggerService.Log("CaterChoice", LogLevel.Information, $"Page {currentPage} processed: {validProducts.Count} valid products");

                currentPage++;
            }


            _loggerService.Log("CaterChoice", LogLevel.Information, $"Finished scraping category {category.Name}, found {products.Count} valid products total");

            // Save to MongoDB if enabled
            MongoStats mongoStats = new();
            if (mongoEnabled && products.Count > 0)
            {
                _loggerService.Log("CaterChoice", LogLevel.Information, $"Saving {products.Count} products to MongoDB...");
                mongoStats = await DbContext.SaveCaterChoiceProductsAsync(products);
                _loggerService.Log("CaterChoice", LogLevel.Information, $"MongoDB stats: {System.Text.Json.JsonSerializer.Serialize(mongoStats)}");
            }

            // If this is a standalone category scrape (not part of scrapeAllCategories)
            if (manageJson && !string.IsNullOrEmpty(options.OutputFile))
            {
                var endTime = DateTime.UtcNow;
                var processingTimeSeconds = (endTime - startTime).TotalSeconds;
                _loggerService.Log("CaterChoice", LogLevel.Information, $"Processing completed in {processingTimeSeconds} seconds");

                var statistics = new ScrapingStatistics
                {
                    TotalProcessed = products.Count,
                    NewRecordsAdded = mongoEnabled ? mongoStats.NewRecordsAdded : products.Count,
                    ExistingRecordsUpdated = mongoStats.RecordsUnchanged,
                    RecordsUnchanged = mongoStats.RecordsUnchanged,
                    Errors = mongoStats.Errors,
                    CategoriesProcessed = new List<string> { category.Name },
                    TotalProcessingTimeSeconds = processingTimeSeconds,
                    ProcessingTimeFormatted = UtilityService.FormatProcessingTime(processingTimeSeconds),
                };

                // Save results to file
                _loggerService.Log("CaterChoice", LogLevel.Information, $"Saving results to file: {options.OutputFile}");
                var outputPath = UtilityService.SaveToJson(new
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
                _loggerService.Log("CaterChoice", LogLevel.Information, $"Results saved to: {outputPath}");

                // Disconnect from MongoDB if connected
                if (mongoEnabled)
                {
                    _loggerService.Log("CaterChoice", LogLevel.Information, "Disconnecting from MongoDB...");
                    DbContext.Disconnect();
                    _loggerService.Log("CaterChoice", LogLevel.Information, "MongoDB disconnected");
                }
            }

            return products;
        }
        catch (Exception ex)
        {
            var errorMessage = $"Error scraping category {category.Name}";
            _loggerService.Log("CaterChoice", LogLevel.Critical, $"{errorMessage} - Exception: {ex.Message}");
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
        }
    }

    private async Task<List<CaterChoiceProduct>> ExtractProductsFromPageAsync(IPage page, string categoryName,
        bool downloadImages)
    {
        _loggerService.Log("CaterChoice", LogLevel.Information, $"Extracting products from page for category: {categoryName}");
        var products = new List<CaterChoiceProduct>();

        // wait for main product grid to load
        try
        {
            await page.WaitForSelectorAsync(CaterChoiceConfig.CaterChoiceSelectors.PRODUCT_GRID,
                new PageWaitForSelectorOptions { Timeout = 10000 });
            _loggerService.Log("CaterChoice", LogLevel.Information, "Main product Grid found");
        }
        catch (Exception ex)
        {
            var errorMessage = "Error waiting for main product grid to load, continuing with product extraction";
            _loggerService.Log("CaterChoice", LogLevel.Warning, $"{errorMessage} - Exception: {ex.Message}");
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
                _loggerService.Log("CaterChoice", LogLevel.Information, $"Trying product container selector: {selector}");
                await page.WaitForSelectorAsync(selector, new PageWaitForSelectorOptions { Timeout = 10000 });
                productElements = (await page.QuerySelectorAllAsync(selector)).ToArray();
                if (productElements.Length > 0)
                {
                    _loggerService.Log("CaterChoice", LogLevel.Information, $"Found {productElements.Length} product containers using selector: {selector}");
                    break;
                }
            }
            catch (Exception ex)
            {
                var warningMessage = $"Selector {selector} failed: {ex.Message}";
                _loggerService.Log("CaterChoice", LogLevel.Warning, warningMessage);
            }
        }

        // If still no products found, try XPath as last resort
        if (productElements == null || productElements.Length == 0)
        {
            _loggerService.Log("CaterChoice", LogLevel.Information, "Trying XPath selector as last resort...");
            try
            {
                var xpathElements = await page.Locator(CaterChoiceConfig.CaterChoiceSelectors.PRODUCT_ITEM_XPATH)
                    .AllAsync();
                if (xpathElements.Count > 0)
                {
                    productElements = (await Task.WhenAll(xpathElements.Select(e => e.ElementHandleAsync())))
                        .Where(e => e != null)
                        .ToArray();
                    _loggerService.Log("CaterChoice", LogLevel.Information, $"Found {productElements.Length} products using XPath");
                }
            }
            catch (Exception ex)
            {
                var warningMessage = $"XPath selector failed: {ex.Message}";
                _loggerService.Log("CaterChoice", LogLevel.Warning, warningMessage);
            }
        }

        if (productElements == null || productElements.Length == 0)
        {
            var warningMessage = "No product elements found, saving page content for debugging...";
            _loggerService.Log("CaterChoice", LogLevel.Warning, warningMessage);
            var pageContent = await page.ContentAsync();
            var debugDir = Path.Combine("screenshots", "caterchoice");
            Directory.CreateDirectory(debugDir);
            var debugFilePath = Path.Combine(debugDir, $"caterchoice-{categoryName}-debug.html");
            await File.WriteAllTextAsync(debugFilePath, pageContent);
            _loggerService.Log("CaterChoice", LogLevel.Information, $"Debug HTML saved to {debugFilePath}");
            return products;
        }

        _loggerService.Log("CaterChoice", LogLevel.Information, $"Processing {productElements.Length} product elements");

        for (int i = 0; i < productElements.Length; i++)
        {
            var productElement = productElements[i];
            try
            {
                _loggerService.Log("CaterChoice", LogLevel.Information, $"Processing product {i + 1} of {productElements.Length}");

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
                                _loggerService.Log("CaterChoice", LogLevel.Information, $"Found name: {name}");
                                if (!string.IsNullOrEmpty(url) && !url.StartsWith("http"))
                                {
                                    url = $"{CaterChoiceConfig.CATER_CHOICE_BASE_URL.TrimEnd('/')}{url}";
                                }

                                _loggerService.Log("CaterChoice", LogLevel.Information, $"Found URL: {url}");
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
                    _loggerService.Log("CaterChoice", LogLevel.Information, $"Skipping product {i + 1}: no valid name found.");
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
                                _loggerService.Log("CaterChoice", LogLevel.Information, $"Found image URL: {(imageUrl.Length > 50 ? imageUrl.Substring(0, 50) : imageUrl)}...");
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
                                _loggerService.Log("CaterChoice", LogLevel.Information, $"Found pack size: {packSize}");
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
                                _loggerService.Log("CaterChoice", LogLevel.Information, $"Found case price: {casePrice}");
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
                                _loggerService.Log("CaterChoice", LogLevel.Information, $"Found single price: {singlePrice}");
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
                var productId = UtilityService.GenerateUUID();
                var productNameHash = UtilityService.GenerateHash(name);

                // Download image if enabled
                string? localImageFilename = null;
                string? localImageFilepath = null;

                if (downloadImages && !string.IsNullOrEmpty(imageUrl))
                {
                    _loggerService.Log("CaterChoice", LogLevel.Information, $"Downloading image for product: {name}");
                    var imageResult =
                        await UtilityService.DownloadImageAsync(imageUrl, productNameHash, "images/cater-choice");
                    if (imageResult != null)
                    {
                        localImageFilename = imageResult.Filename;
                        localImageFilepath = imageResult.FilePath;
                        _loggerService.Log("CaterChoice", LogLevel.Information, $"Image downloaded: {localImageFilename}");
                    }
                    else
                    {
                        _loggerService.Log("CaterChoice", LogLevel.Warning, "Failed to download image");
                    }
                }

                // Get product code and description by visiting product page
                var productCode = "";
                var productDescription = "";

                if (!string.IsNullOrEmpty(url))
                {
                    IPage? productPage = null;
                    const int maxReloadAttempts = 2;

                    try
                    {
                        productPage = await page.Context.NewPageAsync();
                        _loggerService.Log("CaterChoice", LogLevel.Information, $"Visiting product page for code/description: {url}");

                        try
                        {
                            await productPage.GotoAsync(url, new PageGotoOptions { Timeout = 30000 });
                            await productPage.WaitForLoadStateAsync(LoadState.NetworkIdle,
                                new PageWaitForLoadStateOptions { Timeout = 15000 });
                            _loggerService.Log("CaterChoice", LogLevel.Information, "Product page loaded successfully on the first attempt.");
                        }
                        catch (TimeoutException ex)
                        {
                            var warningMessage = $"Initial page load for {url} timed out. Attempting recovery strategies.";
                            _loggerService.Log("CaterChoice", LogLevel.Warning, $"{warningMessage} - Exception: {ex.Message}");
                            bool isPageLoaded = false;

                            // Strategy 1: Close and open a new page
                            try
                            {
                                const string strategyMessage = "Attempting to close the current page and open a new one.";
                                _loggerService.Log("CaterChoice", LogLevel.Warning, strategyMessage);
                                await productPage.CloseAsync();
                                productPage = await page.Context.NewPageAsync();
                                await productPage.GotoAsync(url, new PageGotoOptions { Timeout = 30000 });
                                await productPage.WaitForLoadStateAsync(LoadState.NetworkIdle,
                                    new PageWaitForLoadStateOptions { Timeout = 15000 });
                                _loggerService.Log("CaterChoice", LogLevel.Information, "Successfully loaded page on the second attempt (new page).");
                                isPageLoaded = true;
                            }
                            catch (TimeoutException timeoutEx)
                            {
                                const string strategyMessage = "Opening a new page also timed out. Proceeding to reload attempts.";
                                _loggerService.Log("CaterChoice", LogLevel.Warning, $"{strategyMessage} - Exception: {timeoutEx.Message}");
                            }

                            // Strategy 2: Reload the page
                            if (!isPageLoaded)
                            {
                                for (int j = 0; j < maxReloadAttempts; j++)
                                {
                                    try
                                    {
                                        var strategyMessage = $"Reloading page, attempt {j + 1}/{maxReloadAttempts}";
                                        _loggerService.Log("CaterChoice", LogLevel.Warning, strategyMessage);
                                        await productPage.ReloadAsync(new PageReloadOptions { Timeout = 30000 });
                                        await productPage.WaitForLoadStateAsync(LoadState.NetworkIdle,
                                            new PageWaitForLoadStateOptions { Timeout = 15000 });
                                        _loggerService.Log("CaterChoice", LogLevel.Information, $"Successfully loaded page after reload attempt {j + 1}.");
                                        isPageLoaded = true;
                                        break;
                                    }
                                    catch (TimeoutException timeoutEx)
                                    {
                                        var strategyMessage = $"Reload attempt {i + 1} failed.";
                                        _loggerService.Log("CaterChoice", LogLevel.Warning, $"{strategyMessage} - Exception: {timeoutEx.Message}");
                                    }
                                }
                            }

                            if (!isPageLoaded)
                            {
                                var warningMessage2 = $"All retry attempts failed for {url}. Proceeding with extraction on the potentially incomplete page.";
                                _loggerService.Log("CaterChoice", LogLevel.Warning, warningMessage2);
                            }
                        }

                        // Extraction logic starts here
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
                                        _loggerService.Log("CaterChoice", LogLevel.Information, $"Found product code: {productCode}");
                                        break;
                                    }
                                }
                            }
                            catch (Exception)
                            {
                                /* Continue */
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
                                        _loggerService.Log("CaterChoice", LogLevel.Information, $"Found product description: {(productDescription.Length > 50 ? productDescription.Substring(0, 50) : productDescription)}...");
                                        break;
                                    }
                                }
                            }
                            catch (Exception)
                            {
                                /* Continue */
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        var errorMessage = $"An unexpected error occurred while trying to load product page {url}";
                        _loggerService.Log("CaterChoice", LogLevel.Error, $"{errorMessage} - Exception: {ex.Message}");
                    }
                    finally
                    {
                        if (productPage != null)
                        {
                            await productPage.CloseAsync();
                            _loggerService.Log("CaterChoice", LogLevel.Information, "Product page closed");
                        }
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
                _loggerService.Log("CaterChoice", LogLevel.Information, $"Product {name} added to results");
            }
            catch (Exception ex)
            {
                var errorMessage = $"Error extracting product {i + 1}";
                _loggerService.Log("CaterChoice", LogLevel.Error, $"{errorMessage} - Exception: {ex.Message}");
            }
        }

        _loggerService.Log("CaterChoice", LogLevel.Information, $"Extracted {products.Count} products from page");
        return products;
    }

    private async Task LoginAsync(IPage page, string email, string password)
    {
        _loggerService.Log("CaterChoice", LogLevel.Information, "Starting login process for Cater Choice...");

        try
        {
            var loginUrl = $"{CaterChoiceConfig.CATER_CHOICE_BASE_URL.TrimEnd('/')}/customer/login";
            _loggerService.Log("CaterChoice", LogLevel.Information, $"Navigating to {loginUrl}");
            await page.GotoAsync(loginUrl);

            // Wait for page to fully load
            _loggerService.Log("CaterChoice", LogLevel.Information, "Waiting for page to fully load...");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            _loggerService.Log("CaterChoice", LogLevel.Information, "Waiting for login form to load...");
            await page.WaitForSelectorAsync("input[type=\"email\"]",
                new PageWaitForSelectorOptions { Timeout = 30000 });

            _loggerService.Log("CaterChoice", LogLevel.Information, $"Filling email: {email.Substring(0, Math.Min(3, email.Length))}...");
            await page.FillAsync("input[type=\"email\"]", email);

            _loggerService.Log("CaterChoice", LogLevel.Information, "Filling password: ********");
            await page.FillAsync("input[type=\"password\"]", password);

            // Take a screenshot of the login form for debugging
            var loginScreenshotDir = Path.Combine("screenshots", "caterchoice");
            Directory.CreateDirectory(loginScreenshotDir);
            var loginScreenshotPath = Path.Combine(loginScreenshotDir, "login-form.png");
            await page.ScreenshotAsync(new PageScreenshotOptions { Path = loginScreenshotPath, FullPage = true });
            _loggerService.Log("CaterChoice", LogLevel.Information, $"Login form screenshot saved as {loginScreenshotPath}");

            // Try multiple strategies to click the login button
            _loggerService.Log("CaterChoice", LogLevel.Information, "Attempting to click login button using multiple strategies...");

            // Strategy 1: Use locator with XPath
            _loggerService.Log("CaterChoice", LogLevel.Information, "Strategy 1: Using XPath with locator");
            try
            {
                var buttonByXPath = page.Locator("xpath=/html/body/main/div[2]/div/div/form/button");
                var isVisible = await buttonByXPath.IsVisibleAsync();

                if (isVisible)
                {
                    _loggerService.Log("CaterChoice", LogLevel.Information, "Button found by XPath, attempting to click...");
                    await buttonByXPath.ClickAsync(new LocatorClickOptions { Force = true, Timeout = 5000 });
                    _loggerService.Log("CaterChoice", LogLevel.Information, "Button clicked using XPath");

                    // Check if we've navigated away from the login page
                    await page.WaitForTimeoutAsync(2000);
                    var currentUrl = page.Url;
                    if (!currentUrl.Contains("login"))
                    {
                        _loggerService.Log("CaterChoice", LogLevel.Information, $"Login successful, URL changed to: {currentUrl}");
                        return; // Exit early if login was successful
                    }
                }
                else
                {
                    _loggerService.Log("CaterChoice", LogLevel.Information, "Button not visible or not found by XPath");
                }
            }
            catch (Exception ex)
            {
                var warningMessage = $"XPath strategy failed: {ex.Message}";
                _loggerService.Log("CaterChoice", LogLevel.Warning, warningMessage);
            }

            // Strategy 2: Try submitting the form directly
            _loggerService.Log("CaterChoice", LogLevel.Information, "Strategy 2: Submitting form directly");
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
                    _loggerService.Log("CaterChoice", LogLevel.Information, "Form submitted directly");

                    // Check if we've navigated away from the login page
                    await page.WaitForTimeoutAsync(2000);
                    var currentUrl = page.Url;
                    if (!currentUrl.Contains("login"))
                    {
                        _loggerService.Log("CaterChoice", LogLevel.Information, $"Login successful, URL changed to: {currentUrl}");
                        return; // Exit early if login was successful
                    }
                }
                else
                {
                    _loggerService.Log("CaterChoice", LogLevel.Information, "No form found to submit");
                }
            }
            catch (Exception ex)
            {
                var warningMessage = $"Form submission failed: {ex.Message}";
                _loggerService.Log("CaterChoice", LogLevel.Warning, warningMessage);
            }

            // Strategy 3: Try various CSS selectors
            _loggerService.Log("CaterChoice", LogLevel.Information, "Strategy 3: Trying various CSS selectors");
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
                    _loggerService.Log("CaterChoice", LogLevel.Information, $"Trying selector: {selector}");
                    var button = page.Locator(selector);
                    var isVisible = await button.IsVisibleAsync();

                    if (isVisible)
                    {
                        _loggerService.Log("CaterChoice", LogLevel.Information, $"Button found with selector: {selector}");
                        await button.ClickAsync(new LocatorClickOptions { Force = true, Timeout = 5000 });
                        _loggerService.Log("CaterChoice", LogLevel.Information, $"Button clicked using selector: {selector}");

                        // Check if we've navigated away from the login page
                        await page.WaitForTimeoutAsync(2000);
                        var currentUrl = page.Url;
                        if (!currentUrl.Contains("login"))
                        {
                            _loggerService.Log("CaterChoice", LogLevel.Information, $"Login successful, URL changed to: {currentUrl}");
                            return; // Exit early if login was successful
                        }

                        break;
                    }
                }
                catch (Exception ex)
                {
                    var warningMessage = $"Selector {selector} failed: {ex.Message}";
                    _loggerService.Log("CaterChoice", LogLevel.Warning, warningMessage);
                }
            }

            // Strategy 4: Try pressing Enter in the password field
            _loggerService.Log("CaterChoice", LogLevel.Information, "Strategy 4: Pressing Enter in password field");
            try
            {
                await page.FocusAsync("input[type=\"password\"]");
                await page.Keyboard.PressAsync("Enter");
                _loggerService.Log("CaterChoice", LogLevel.Information, "Enter key pressed in password field");

                // Check if we've navigated away from the login page
                await page.WaitForTimeoutAsync(2000);
                var currentUrl = page.Url;
                if (!currentUrl.Contains("login"))
                {
                    _loggerService.Log("CaterChoice", LogLevel.Information, $"Login successful, URL changed to: {currentUrl}");
                    return; // Exit early if login was successful
                }
            }
            catch (Exception ex)
            {
                var warningMessage = $"Enter key press failed: {ex.Message}";
                _loggerService.Log("CaterChoice", LogLevel.Warning, warningMessage);
            }

            // Check if login was successful without waiting for navigation
            var finalUrl = page.Url;
            _loggerService.Log("CaterChoice", LogLevel.Information, $"Current URL after login attempts: {finalUrl}");

            if (finalUrl.Contains("login"))
            {
                const string warningMessage = "Login failed - still on login page";
                _loggerService.Log("CaterChoice", LogLevel.Warning, warningMessage);
                throw new Exception("Login failed");
            }
            else
            {
                _loggerService.Log("CaterChoice", LogLevel.Information, "Login successful");
            }
        }
        catch (Exception ex)
        {
            const string errorMessage = "Login error";
            _loggerService.Log("CaterChoice", LogLevel.Critical, $"{errorMessage} - Exception: {ex.Message}");
            // Take a screenshot of the error state
            var errorScreenshotDir = Path.Combine("screenshots", "caterchoice");
            Directory.CreateDirectory(errorScreenshotDir);
            var errorScreenshotPath = Path.Combine(errorScreenshotDir, "login-error.png");
            await page.ScreenshotAsync(new PageScreenshotOptions { Path = errorScreenshotPath, FullPage = true });
            _loggerService.Log("CaterChoice", LogLevel.Information, $"Error state screenshot saved as {errorScreenshotPath}");
            throw;
        }
    }
}