
using Microsoft.Extensions.Options;

namespace WebScrapperApi.Services;

public class CaterChoiceScraperService(
    UtilityService utilityService,
    ILogger<CaterChoiceScraperService> logger,
    ScraperDbContext dbContext,
    IOptionsMonitor<ScraperCredentialsConfig> credentialsMonitor)
{
    public readonly UtilityService UtilityService = utilityService;
    public readonly ScraperDbContext DbContext = dbContext;
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
                await DbContext.ConnectAsync();
                mongoEnabled = true;
                logger.LogInformation("MongoDB connection established using ScraperDbContext");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to connect to MongoDB. Proceeding without database integration.");
                mongoEnabled = false;
            }
        }
        else
        {
            logger.LogInformation("MongoDB storage disabled by user preference");
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
                logger.LogInformation("Processing category: {CategoryName}", category.Name);

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
                    logger.LogError(ex, "Error scraping category {CategoryName}", category.Name);
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
            logger.LogError(ex, "Error during scraping process");
            throw;
        }
    }

    public async Task<List<CaterChoiceProduct>> ScrapeCategoryAsync(ScrapingOptions options, Category category,
        IBrowser? existingBrowser = null, bool manageMongo = true, bool manageJson = true)
    {
        logger.LogInformation("Starting to scrape Cater Choice category: {CategoryName}", category.Name);
        logger.LogInformation("Category URL: {CategoryUrl}", category.Url);

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
                await DbContext.ConnectAsync();
                mongoEnabled = true;
                logger.LogInformation("MongoDB connection established using ScraperDbContext");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to connect to MongoDB. Proceeding without database integration.");
                mongoEnabled = false;
            }
        }
        else
        {
            logger.LogInformation("MongoDB storage disabled by user preference");
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
                logger.LogCritical(email);
                logger.LogCritical(password);
                logger.LogInformation("Using credentials, attempting login...");
                await LoginAsync(page, email, password);
            }
            else
            {
                logger.LogInformation("No credentials provided, skipping login");
            }

            // Navigate to category page
            logger.LogInformation("Navigating to category URL: {CategoryUrl}", category.Url);
            await page.GotoAsync(category.Url, new PageGotoOptions { Timeout = 60000 });

            // Wait for the page to fully load
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle,
                new PageWaitForLoadStateOptions { Timeout = 30000 });
            logger.LogInformation("Page fully loaded");

            // Take a screenshot of the category page for debugging
            var screenshotDir = Path.Combine("screenshots", "caterchoice");
            Directory.CreateDirectory(screenshotDir);
            var screenshotPath = Path.Combine(screenshotDir, $"caterchoice-{category.Name}-page.png");
            await page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = screenshotPath,
                FullPage = true
            });
            logger.LogInformation("Category page screenshot saved as {ScreenshotPath}", screenshotPath);

            // Extract products from current page
            logger.LogInformation("Extracting products from current page...");
            var pageProducts = await ExtractProductsFromPageAsync(page, category.Name, options.DownloadImages);

            // Filter out "Unknown Product" entries
            var validProducts = pageProducts.Where(p => p.ProductName != "Unknown Product").ToList();
            logger.LogInformation("Found {TotalProducts} products, {ValidProducts} valid products after filtering",
                pageProducts.Count, validProducts.Count);

            products.AddRange(validProducts);

            // Pagination logic: click next page and scrape recursively
            int currentPage = 2;
            int maxPages = 30;

            while (currentPage <= maxPages)
            {
                var pageUrl = $"{category.Url}?page={currentPage}";
                logger.LogInformation("Navigating to page {PageNumber}: {Url}", currentPage, pageUrl);

                await page.GotoAsync(pageUrl, new PageGotoOptions { Timeout = 60000 });
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle,
                    new PageWaitForLoadStateOptions { Timeout = 30000 });

                var productsOnPage =
                    await ExtractProductsFromPageAsync(page, category.Name, options.DownloadImages);
                var nextValidProducts = productsOnPage.Where(p => p.ProductName != "Unknown Product").ToList();

                if (nextValidProducts.Count == 0)
                {
                    logger.LogInformation("No products found on page {PageNumber}, stopping pagination.",
                        currentPage);
                    break;
                }

                products.AddRange(nextValidProducts);
                logger.LogInformation("Page {PageNumber} processed: {Count} valid products", currentPage,
                    validProducts.Count);

                currentPage++;
            }


            logger.LogInformation(
                "Finished scraping category {CategoryName}, found {ProductCount} valid products total",
                category.Name, products.Count);

            // Save to MongoDB if enabled
            MongoStats mongoStats = new();
            if (mongoEnabled && products.Count > 0)
            {
                logger.LogInformation("Saving {ProductCount} products to MongoDB...", products.Count);
                mongoStats = await DbContext.SaveCaterChoiceProductsAsync(products);
                logger.LogInformation("MongoDB stats: {Stats}",
                    System.Text.Json.JsonSerializer.Serialize(mongoStats));
            }

            // If this is a standalone category scrape (not part of scrapeAllCategories)
            if (manageJson && !string.IsNullOrEmpty(options.OutputFile))
            {
                var endTime = DateTime.UtcNow;
                var processingTimeSeconds = (endTime - startTime).TotalSeconds;
                logger.LogInformation("Processing completed in {ProcessingTime} seconds", processingTimeSeconds);

                var statistics = new ScrapingStatistics
                {
                    TotalProcessed = products.Count,
                    NewRecordsAdded = mongoEnabled ? mongoStats.NewRecordsAdded : products.Count,
                    ExistingRecordsUpdated = mongoStats.ExistingRecordsUpdated,
                    RecordsUnchanged = mongoStats.RecordsUnchanged,
                    Errors = mongoStats.Errors,
                    CategoriesProcessed = new List<string> { category.Name },
                    TotalProcessingTimeSeconds = processingTimeSeconds,
                    ProcessingTimeFormatted = UtilityService.FormatProcessingTime(processingTimeSeconds),
                };

                // Save results to file
                logger.LogInformation("Saving results to file: {OutputFile}", options.OutputFile);
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
                logger.LogInformation("Results saved to: {OutputPath}", outputPath);

                // Disconnect from MongoDB if connected
                if (mongoEnabled)
                {
                    logger.LogInformation("Disconnecting from MongoDB...");
                    DbContext.Disconnect();
                    logger.LogInformation("MongoDB disconnected");
                }
            }

            return products;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error scraping category {CategoryName}", category.Name);
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
        logger.LogInformation("Extracting products from page for category: {CategoryName}", categoryName);
        var products = new List<CaterChoiceProduct>();

        // wait for main product grid to load
        try
        {
            await page.WaitForSelectorAsync(CaterChoiceConfig.CaterChoiceSelectors.PRODUCT_GRID,
                new PageWaitForSelectorOptions { Timeout = 10000 });
            logger.LogInformation("Main product Grid found");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error waiting for main product grid to load, continuing with product extraction");
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
                logger.LogInformation("Trying product container selector: {Selector}", selector);
                await page.WaitForSelectorAsync(selector, new PageWaitForSelectorOptions { Timeout = 10000 });
                productElements = (await page.QuerySelectorAllAsync(selector)).ToArray();
                if (productElements.Length > 0)
                {
                    logger.LogInformation("Found {Count} product containers using selector: {Selector}",
                        productElements.Length, selector);
                    break;
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning("Selector {Selector} failed: {Error}", selector, ex.Message);
            }
        }

        // If still no products found, try XPath as last resort
        if (productElements == null || productElements.Length == 0)
        {
            logger.LogInformation("Trying XPath selector as last resort...");
            try
            {
                var xpathElements = await page.Locator(CaterChoiceConfig.CaterChoiceSelectors.PRODUCT_ITEM_XPATH)
                    .AllAsync();
                if (xpathElements.Count > 0)
                {
                    productElements = (await Task.WhenAll(xpathElements.Select(e => e.ElementHandleAsync())))
                        .Where(e => e != null)
                        .ToArray();
                    logger.LogInformation("Found {Count} products using XPath", productElements.Length);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning("XPath selector failed: {Error}", ex.Message);
            }
        }

        if (productElements == null || productElements.Length == 0)
        {
            logger.LogWarning("No product elements found, saving page content for debugging...");
            var pageContent = await page.ContentAsync();
            var debugDir = Path.Combine("screenshots", "caterchoice");
            Directory.CreateDirectory(debugDir);
            var debugFilePath = Path.Combine(debugDir, $"caterchoice-{categoryName}-debug.html");
            await File.WriteAllTextAsync(debugFilePath, pageContent);
            logger.LogInformation("Debug HTML saved to {FilePath}", debugFilePath);
            return products;
        }

        logger.LogInformation("Processing {Count} product elements", productElements.Length);

        for (int i = 0; i < productElements.Length; i++)
        {
            var productElement = productElements[i];
            try
            {
                logger.LogInformation("Processing product {Index} of {Total}", i + 1, productElements.Length);

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
                                logger.LogInformation("Found name: {Name}", name);
                                if (!string.IsNullOrEmpty(url) && !url.StartsWith("http"))
                                {
                                    url = $"{CaterChoiceConfig.CATER_CHOICE_BASE_URL.TrimEnd('/')}{url}";
                                }

                                logger.LogInformation("Found URL: {Url}", url);
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
                    logger.LogInformation("Skipping product {Index}: no valid name found.", i + 1);
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
                                logger.LogInformation("Found image URL: {ImageUrl}...",
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
                                logger.LogInformation("Found pack size: {PackSize}", packSize);
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
                                logger.LogInformation("Found case price: {CasePrice}", casePrice);
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
                                logger.LogInformation("Found single price: {SinglePrice}", singlePrice);
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
                    logger.LogInformation("Downloading image for product: {Name}", name);
                    var imageResult =
                        await UtilityService.DownloadImageAsync(imageUrl, productNameHash, "images/cater-choice");
                    if (imageResult != null)
                    {
                        localImageFilename = imageResult.Filename;
                        localImageFilepath = imageResult.FilePath;
                        logger.LogInformation("Image downloaded: {Filename}", localImageFilename);
                    }
                    else
                    {
                        logger.LogWarning("Failed to download image");
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
                        logger.LogInformation("Visiting product page for code/description: {Url}", url);

                        try
                        {
                            await productPage.GotoAsync(url, new PageGotoOptions { Timeout = 30000 });
                            await productPage.WaitForLoadStateAsync(LoadState.NetworkIdle,
                                new PageWaitForLoadStateOptions { Timeout = 15000 });
                            logger.LogInformation("Product page loaded successfully on the first attempt.");
                        }
                        catch (TimeoutException)
                        {
                            logger.LogWarning(
                                "Initial page load for {Url} timed out. Attempting recovery strategies.", url);
                            bool isPageLoaded = false;

                            // Strategy 1: Close and open a new page
                            try
                            {
                                logger.LogWarning("Attempting to close the current page and open a new one.");
                                await productPage.CloseAsync();
                                productPage = await page.Context.NewPageAsync();
                                await productPage.GotoAsync(url, new PageGotoOptions { Timeout = 30000 });
                                await productPage.WaitForLoadStateAsync(LoadState.NetworkIdle,
                                    new PageWaitForLoadStateOptions { Timeout = 15000 });
                                logger.LogInformation("Successfully loaded page on the second attempt (new page).");
                                isPageLoaded = true;
                            }
                            catch (TimeoutException)
                            {
                                logger.LogWarning(
                                    "Opening a new page also timed out. Proceeding to reload attempts.");
                            }

                            // Strategy 2: Reload the page
                            if (!isPageLoaded)
                            {
                                for (int j = 0; j < maxReloadAttempts; j++)
                                {
                                    try
                                    {
                                        logger.LogWarning("Reloading page, attempt {Attempt}/{MaxAttempts}", j + 1,
                                            maxReloadAttempts);
                                        await productPage.ReloadAsync(new PageReloadOptions { Timeout = 30000 });
                                        await productPage.WaitForLoadStateAsync(LoadState.NetworkIdle,
                                            new PageWaitForLoadStateOptions { Timeout = 15000 });
                                        logger.LogInformation(
                                            "Successfully loaded page after reload attempt {Attempt}.", j + 1);
                                        isPageLoaded = true;
                                        break;
                                    }
                                    catch (TimeoutException)
                                    {
                                        logger.LogWarning("Reload attempt {Attempt} failed.", i + 1);
                                    }
                                }
                            }

                            if (!isPageLoaded)
                            {
                                logger.LogWarning(
                                    "All retry attempts failed for {Url}. Proceeding with extraction on the potentially incomplete page.",
                                    url);
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
                                        logger.LogInformation("Found product code: {ProductCode}", productCode);
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
                                        logger.LogInformation("Found product description: {Description}...",
                                            productDescription.Length > 50
                                                ? productDescription.Substring(0, 50)
                                                : productDescription);
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
                        logger.LogError(ex, "An unexpected error occurred while trying to load product page {Url}",
                            url);
                    }
                    finally
                    {
                        if (productPage != null)
                        {
                            await productPage.CloseAsync();
                            logger.LogInformation("Product page closed");
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
                logger.LogInformation("Product {Name} added to results", name);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error extracting product {Index}", i + 1);
            }
        }

        logger.LogInformation("Extracted {Count} products from page", products.Count);
        return products;
    }

    private async Task LoginAsync(IPage page, string email, string password)
    {
        logger.LogInformation("Starting login process for Cater Choice...");

        try
        {
            var loginUrl = $"{CaterChoiceConfig.CATER_CHOICE_BASE_URL.TrimEnd('/')}/customer/login";
            logger.LogInformation("Navigating to {LoginUrl}", loginUrl);
            await page.GotoAsync(loginUrl);

            // Wait for page to fully load
            logger.LogInformation("Waiting for page to fully load...");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            logger.LogInformation("Waiting for login form to load...");
            await page.WaitForSelectorAsync("input[type=\"email\"]",
                new PageWaitForSelectorOptions { Timeout = 30000 });

            logger.LogInformation("Filling email: {Email}...", email.Substring(0, Math.Min(3, email.Length)));
            await page.FillAsync("input[type=\"email\"]", email);

            logger.LogInformation("Filling password: ********");
            await page.FillAsync("input[type=\"password\"]", password);

            // Take a screenshot of the login form for debugging
            var loginScreenshotDir = Path.Combine("screenshots", "caterchoice");
            Directory.CreateDirectory(loginScreenshotDir);
            var loginScreenshotPath = Path.Combine(loginScreenshotDir, "login-form.png");
            await page.ScreenshotAsync(new PageScreenshotOptions { Path = loginScreenshotPath, FullPage = true });
            logger.LogInformation("Login form screenshot saved as {LoginScreenshotPath}", loginScreenshotPath);

            // Try multiple strategies to click the login button
            logger.LogInformation("Attempting to click login button using multiple strategies...");

            // Strategy 1: Use locator with XPath
            logger.LogInformation("Strategy 1: Using XPath with locator");
            try
            {
                var buttonByXPath = page.Locator("xpath=/html/body/main/div[2]/div/div/form/button");
                var isVisible = await buttonByXPath.IsVisibleAsync();

                if (isVisible)
                {
                    logger.LogInformation("Button found by XPath, attempting to click...");
                    await buttonByXPath.ClickAsync(new LocatorClickOptions { Force = true, Timeout = 5000 });
                    logger.LogInformation("Button clicked using XPath");

                    // Check if we've navigated away from the login page
                    await page.WaitForTimeoutAsync(2000);
                    var currentUrl = page.Url;
                    if (!currentUrl.Contains("login"))
                    {
                        logger.LogInformation("Login successful, URL changed to: {Url}", currentUrl);
                        return; // Exit early if login was successful
                    }
                }
                else
                {
                    logger.LogInformation("Button not visible or not found by XPath");
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning("XPath strategy failed: {Error}", ex.Message);
            }

            // Strategy 2: Try submitting the form directly
            logger.LogInformation("Strategy 2: Submitting form directly");
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
                    logger.LogInformation("Form submitted directly");

                    // Check if we've navigated away from the login page
                    await page.WaitForTimeoutAsync(2000);
                    var currentUrl = page.Url;
                    if (!currentUrl.Contains("login"))
                    {
                        logger.LogInformation("Login successful, URL changed to: {Url}", currentUrl);
                        return; // Exit early if login was successful
                    }
                }
                else
                {
                    logger.LogInformation("No form found to submit");
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning("Form submission failed: {Error}", ex.Message);
            }

            // Strategy 3: Try various CSS selectors
            logger.LogInformation("Strategy 3: Trying various CSS selectors");
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
                    logger.LogInformation("Trying selector: {Selector}", selector);
                    var button = page.Locator(selector);
                    var isVisible = await button.IsVisibleAsync();

                    if (isVisible)
                    {
                        logger.LogInformation("Button found with selector: {Selector}", selector);
                        await button.ClickAsync(new LocatorClickOptions { Force = true, Timeout = 5000 });
                        logger.LogInformation("Button clicked using selector: {Selector}", selector);

                        // Check if we've navigated away from the login page
                        await page.WaitForTimeoutAsync(2000);
                        var currentUrl = page.Url;
                        if (!currentUrl.Contains("login"))
                        {
                            logger.LogInformation("Login successful, URL changed to: {Url}", currentUrl);
                            return; // Exit early if login was successful
                        }

                        break;
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning("Selector {Selector} failed: {Error}", selector, ex.Message);
                }
            }

            // Strategy 4: Try pressing Enter in the password field
            logger.LogInformation("Strategy 4: Pressing Enter in password field");
            try
            {
                await page.FocusAsync("input[type=\"password\"]");
                await page.Keyboard.PressAsync("Enter");
                logger.LogInformation("Enter key pressed in password field");

                // Check if we've navigated away from the login page
                await page.WaitForTimeoutAsync(2000);
                var currentUrl = page.Url;
                if (!currentUrl.Contains("login"))
                {
                    logger.LogInformation("Login successful, URL changed to: {Url}", currentUrl);
                    return; // Exit early if login was successful
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning("Enter key press failed: {Error}", ex.Message);
            }

            // Check if login was successful without waiting for navigation
            var finalUrl = page.Url;
            logger.LogInformation("Current URL after login attempts: {Url}", finalUrl);

            if (finalUrl.Contains("login"))
            {
                logger.LogWarning("Login failed - still on login page");
                throw new Exception("Login failed");
            }
            else
            {
                logger.LogInformation("Login successful");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Login error");
            // Take a screenshot of the error state
            var errorScreenshotDir = Path.Combine("screenshots", "caterchoice");
            Directory.CreateDirectory(errorScreenshotDir);
            var errorScreenshotPath = Path.Combine(errorScreenshotDir, "login-error.png");
            await page.ScreenshotAsync(new PageScreenshotOptions { Path = errorScreenshotPath, FullPage = true });
            logger.LogInformation("Error state screenshot saved as {ErrorScreenshotPath}", errorScreenshotPath);
            throw;
        }
    }
}