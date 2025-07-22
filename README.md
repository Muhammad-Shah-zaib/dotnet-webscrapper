# Web Scraper API

A C# ASP.NET Core Web API that scrapes product data from multiple websites using Microsoft.Playwright for browser automation. This is a conversion of the original JavaScript Playwright scrapers to C#.

## Supported Scrapers

- **Cater Choice Scraper** - Scrapes product data from Cater Choice website
- **Adams Food Service Scraper** - Scrapes product data from Adams Food Service website

## Features

- Scrape all categories or specific categories
- MongoDB integration for data storage (optional)
- Image downloading capability
- Comprehensive logging
- RESTful API endpoints
- Swagger documentation
- Full browser automation with Playwright
- Screenshot capture for debugging

## Prerequisites

- .NET 8.0 SDK
- MongoDB (optional, for data storage)

## Installation

1. Clone the repository
2. Install NuGet packages:
   ```bash
   dotnet restore
   ```

3. Install Playwright browsers:
   ```bash
   pwsh bin/Debug/net8.0/playwright.ps1 install chromium
   ```
   Or on Linux/macOS:
   ```bash
   playwright install chromium
   ```

4. Update configuration in `Configuration/CaterChoiceConfig.cs`:
   - Replace placeholder URLs with actual Cater Choice URLs
   - Update CSS selectors to match the current website structure

## Configuration

### MongoDB Storage Control

The API now supports an optional `storeInMongoDB` flag in the request body that allows you to control whether scraped data should be stored in MongoDB:

- **`storeInMongoDB: true`** (default): Data will be stored in MongoDB if connection is successful
- **`storeInMongoDB: false`**: Data will only be saved to JSON file, MongoDB connection will be skipped

This is useful when you want to:
- Test scraping without affecting your database
- Save data only to files for backup purposes
- Reduce database load during large scraping operations
- Work in environments where MongoDB is not available

### Required Configuration Updates

Before running the scrapers, you need to update the configuration files with actual URLs and selectors:

#### Cater Choice Configuration (`Configuration/CaterChoiceConfig.cs`)

1. **Base URL**: Replace the placeholder with the actual Cater Choice base URL
2. **Category URLs**: Add the actual category URLs you want to scrape
3. **CSS Selectors**: Update the selectors to match the current website structure

```csharp
// TODO: Replace with actual base URL
public const string CATER_CHOICE_BASE_URL = "https://cater-choice.com/";

// TODO: Replace with actual category URLs
public static readonly List<Category> CATER_CHOICE_CATEGORIES = new()
{
    new Category { Name = "Beverages", Url = "https://cater-choice.com/category/beverages" },
    new Category { Name = "Snacks", Url = "https://cater-choice.com/category/snacks" },
    // Add more categories as needed
};
```

#### Adams Food Service Configuration (`Configuration/AdamsConfig.cs`)

1. **Base URL**: Replace the placeholder with the actual Adams Food Service base URL
2. **Category URLs**: Add the actual category URLs you want to scrape
3. **CSS Selectors**: Update the selectors to match the current website structure

```csharp
// TODO: Replace with actual base URL
public const string ADAMS_BASE_URL = "https://adamsfoodservice.com/";

// TODO: Replace with actual category URLs
public static readonly List<Category> ADAMS_CATEGORIES = new()
{
    new Category { Name = "Beverages", Url = "https://adamsfoodservice.com/category/beverages" },
    new Category { Name = "Snacks", Url = "https://adamsfoodservice.com/category/snacks" },
    // Add more categories as needed
};
```

## API Endpoints

### Cater Choice Scraper

#### 1. Scrape All Categories
**POST** `/api/scraper/scrape-all-categories`

Scrapes all configured categories and returns comprehensive results.

**Request Body:**
```json
{
  "email": "your-email@example.com",
  "password": "your-password",
  "headless": false,
  "downloadImages": false,
  "storeInMongoDB": true,
  "outputFile": "cater_choice_products.json"
}
```

**Response:**
```json
{
  "status": "success",
  "scraper": "cater-choice",
  "message": "Scraping completed successfully",
  "timestamp": "2024-01-01T12:00:00Z",
  "totalProducts": 150,
  "downloadImagesEnabled": false,
  "outputFile": "cater_choice_products.json",
  "mongoDbEnabled": true,
  "mongoDbDatabase": "COMPETITION",
  "mongoDbCollection": "caterchoice",
  "statistics": {
    "totalProcessed": 150,
    "newRecordsAdded": 120,
    "existingRecordsUpdated": 30,
    "recordsUnchanged": 0,
    "errors": 0,
    "categoriesProcessed": ["Beverages", "Snacks"],
    "totalProcessingTimeSeconds": 45.5,
    "processingTimeFormatted": "00:00:45"
  },
  "outputPath": "/path/to/output/file.json",
  "products": [...]
}
```

#### 2. Scrape Specific Category
**POST** `/api/scraper/scrape-category/{categoryName}`

Scrapes a specific category by name.

**Request Body:** Same as above

**Response:**
```json
{
  "status": "success",
  "category": "Beverages",
  "total_products": 75,
  "products": [...],
  "timestamp": "2024-01-01T12:00:00Z"
}
```

#### 3. Get Available Categories
**GET** `/api/scraper/categories`

Returns a list of all available categories for scraping.

**Response:**
```json
{
  "status": "success",
  "categories": [
    {
      "name": "Beverages",
      "url": "https://cater-choice.com/category/beverages"
    },
    {
      "name": "Snacks",
      "url": "https://cater-choice.com/category/snacks"
    }
  ],
  "total_categories": 2
}
```

#### 4. Get Configuration
**GET** `/api/scraper/config`

Returns the current scraping configuration including selectors and URLs.

#### 5. Health Check
**GET** `/api/scraper/health`

Returns the service health status.

### Adams Food Service Scraper

#### 1. Scrape All Categories
**POST** `/api/adams/scrape-all-categories`

Scrapes all configured Adams categories and returns comprehensive results.

**Request Body:**
```json
{
  "headless": false,
  "downloadImages": false,
  "storeInMongoDB": true,
  "outputFile": "adams_products.json"
}
```

**Response:**
```json
{
  "status": "success",
  "scraper": "adams",
  "message": "Adams scraping completed successfully",
  "timestamp": "2024-01-01T12:00:00Z",
  "totalProducts": 150,
  "downloadImagesEnabled": false,
  "outputFile": "adams_products.json",
  "mongoDbEnabled": true,
  "mongoDbDatabase": "COMPETITION",
  "mongoDbCollection": "adams",
  "statistics": {
    "totalProcessed": 150,
    "newRecordsAdded": 120,
    "existingRecordsUpdated": 30,
    "recordsUnchanged": 0,
    "errors": 0,
    "categoriesProcessed": ["Beverages", "Snacks"],
    "totalProcessingTimeSeconds": 45.5,
    "processingTimeFormatted": "00:00:45"
  },
  "outputPath": "/path/to/output/file.json",
  "adamsProducts": [...]
}
```

#### 2. Scrape Specific Category
**POST** `/api/adams/scrape-category/{categoryName}`

Scrapes a specific Adams category by name.

**Request Body:** Same as above

**Response:**
```json
{
  "status": "success",
  "category": "Beverages",
  "total_products": 75,
  "products": [...],
  "timestamp": "2024-01-01T12:00:00Z"
}
```

#### 3. Get Available Categories
**GET** `/api/adams/categories`

Returns a list of all available Adams categories for scraping.

#### 4. Get Configuration
**GET** `/api/adams/config`

Returns the current Adams scraping configuration including selectors and URLs.

#### 5. Health Check
**GET** `/api/adams/health`

Returns the Adams service health status.

## Data Models

### CaterChoiceProduct
```csharp
public class CaterChoiceProduct
{
    public string ProductId { get; set; }
    public string? ProductCode { get; set; }
    public string? ProductName { get; set; }
    public string? ProductDescription { get; set; }
    public string? ProductSize { get; set; }
    public string? ProductSinglePrice { get; set; }
    public string? ProductCasePrice { get; set; }
    public string? ProductUrl { get; set; }
    public string? OriginalImageUrl { get; set; }
    public string? LocalImageFilename { get; set; }
    public string? LocalImageFilepath { get; set; }
    public string? Category { get; set; }
    public DateTime ScrapedTimestamp { get; set; }
    public string Source { get; set; }
}
```

### AdamsProduct
```csharp
public class AdamsProduct
{
    public string ProductId { get; set; }
    public string? Name { get; set; }
    public string? Sku { get; set; }
    public string? ImageUrlScraped { get; set; }
    public string? ImageFilenameLocal { get; set; }
    public string? Category { get; set; }
    public string? ProductPageUrl { get; set; }
    public string? ScrapedFromCategoryPageUrl { get; set; }
    public string Source { get; set; }
    public DateTime ScrapedTimestamp { get; set; }
}
```

## Running the Application

1. **Development:**
   ```bash
   dotnet run
   ```

2. **Production:**
   ```bash
   dotnet publish -c Release
   dotnet WebScrapperApi.dll
   ```

3. **Access Swagger UI:**
   Navigate to `https://localhost:7001/swagger` (or the configured port)

## Browser Automation Features

### Playwright Integration
This API uses Microsoft.Playwright for full browser automation, providing:

- **Real Browser Rendering**: Uses Chromium browser for accurate page rendering
- **JavaScript Execution**: Handles dynamic content and SPAs
- **Screenshot Capture**: Automatic screenshots for debugging
- **Multiple Login Strategies**: Robust login handling with fallback methods
- **Resource Management**: Proper cleanup of browser resources

### Browser Options
- **Headless Mode**: Can run with or without visible browser window
- **Viewport Configuration**: Set to 1280x720 for consistent rendering
- **Slow Motion**: 50ms delay for debugging (when headless=false)
- **Timeout Handling**: Configurable timeouts for page loading and element selection

## Important Notes

### Playwright Browser Installation
Make sure Playwright browsers are installed before running the application:

```bash
# Windows
pwsh bin/Debug/net8.0/playwright.ps1 install chromium

# Linux/macOS
playwright install chromium
```

### Authentication
The login functionality uses multiple strategies to handle different login form implementations:

1. **XPath Locator**: Primary method using specific XPath
2. **Form Submission**: Direct form submission as fallback
3. **CSS Selectors**: Multiple selector attempts for login buttons
4. **Keyboard Input**: Enter key press as last resort

### Rate Limiting
Consider implementing rate limiting to avoid overwhelming the target website:

```csharp
// Add delays between requests
await page.WaitForTimeoutAsync(1000); // 1 second delay
```

### Error Handling
The API includes comprehensive error handling and logging:

- **Screenshot Capture**: Automatic screenshots on errors
- **Detailed Logging**: Structured logging with correlation IDs
- **Resource Cleanup**: Proper disposal of browser resources
- **Graceful Degradation**: Continues processing even if individual products fail

### Debugging Features
- **Screenshot Capture**: Automatic screenshots of pages and error states
- **HTML Content Saving**: Debug HTML files for troubleshooting
- **Detailed Logging**: Comprehensive logging of all operations
- **Multiple Selector Strategies**: Fallback selectors for robust element selection

## Dependencies

- **Microsoft.Playwright**: Browser automation and web scraping
- **MongoDB.Driver**: MongoDB database operations
- **Newtonsoft.Json**: JSON serialization
- **Swashbuckle.AspNetCore**: API documentation

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests if applicable
5. Submit a pull request

## License

This project is licensed under the MIT License. 