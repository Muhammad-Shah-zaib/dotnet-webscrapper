using MongoDB.Driver;

namespace WebScrapperApi.Data
{
    public class ScraperDbContext
    {
        private readonly IMongoClient _client;
        private readonly IConfiguration _configuration;
        private readonly Dictionary<string, string> _databaseMapping;

        public ScraperDbContext(IConfiguration configuration)
        {
            _configuration = configuration;

            var connectionString = _configuration["ConnectionStrings:DefaultMongo"];

            _client = new MongoClient(connectionString);
            // Get database mapping from configuration
            _databaseMapping = _configuration.GetSection("MongoDB:ScraperDatabases")
                .Get<Dictionary<string, string>>() ?? new Dictionary<string, string>();
        }

        private IMongoDatabase GetDatabase(string scraperName)
        {
            if (!_databaseMapping.TryGetValue(scraperName.ToLower(), out var databaseName))
            {
                throw new InvalidOperationException($"No database mapping found for scraper: {scraperName}");
            }

            return _client.GetDatabase(databaseName);
        }

        private IMongoCollection<T> GetCollection<T>(string scraperName, string collectionName)
        {
            var database = GetDatabase(scraperName);
            return database.GetCollection<T>(collectionName);
        }

        public async Task ConnectAsync(string scraperName)
        {
            var database = GetDatabase(scraperName);
            await database.RunCommandAsync((Command<object>)"{ping:1}");
        }

        public async Task<MongoStats> SaveCaterChoiceProductsAsync(List<CaterChoiceProduct> products)
        {
            var stats = new MongoStats();
            var collection = GetCollection<CaterChoiceProduct>("caterchoice", "caterchoice");

            foreach (var product in products)
            {
                try
                {
                    // Check if product already exists by product code and category
                    var filter = Builders<CaterChoiceProduct>.Filter.And(
                        Builders<CaterChoiceProduct>.Filter.Eq(p => p.ProductCode, product.ProductCode),
                        Builders<CaterChoiceProduct>.Filter.Eq(p => p.Category, product.Category)
                    );

                    var existingProduct = await collection
                        .Find(filter)
                        .FirstOrDefaultAsync();

                    if (existingProduct == null)
                    {
                        // Insert new product
                        await collection.InsertOneAsync(product);
                        stats.NewRecordsAdded++;
                    }
                    else
                    {
                        // Update existing product
                        var update = Builders<CaterChoiceProduct>.Update
                            .Set(p => p.ProductCode, product.ProductCode)
                            .Set(p => p.ProductDescription, product.ProductDescription)
                            .Set(p => p.ProductSize, product.ProductSize)
                            .Set(p => p.ProductSinglePrice, product.ProductSinglePrice)
                            .Set(p => p.ProductCasePrice, product.ProductCasePrice)
                            .Set(p => p.ProductUrl, product.ProductUrl)
                            .Set(p => p.OriginalImageUrl, product.OriginalImageUrl)
                            .Set(p => p.LocalImageFilename, product.LocalImageFilename)
                            .Set(p => p.LocalImageFilepath, product.LocalImageFilepath)
                            .Set(p => p.ScrapedTimestamp, product.ScrapedTimestamp);

                        var result = await collection.UpdateOneAsync(filter, update);

                        Console.WriteLine(
                            $"Update Result - Product: {product.ProductName}, Matched: {result.MatchedCount}, Modified: {result.ModifiedCount}");

                        if (result.ModifiedCount > 0)
                        {
                            stats.ExistingRecordsUpdated++;
                        }
                        else
                        {
                            stats.RecordsUnchanged++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    stats.Errors++;
                    Console.WriteLine($"❌ Failed to save product: {product.ProductName}, Category: {product.Category}");
                    Console.WriteLine($"Error: {ex.Message}");
                }
            }

            return stats;
        }

        public async Task<MongoStats> SaveAdamsProductsAsync(List<AdamsProduct> products)
        {
            var stats = new MongoStats();
            var collection = GetCollection<AdamsProduct>("adams", "adams");

            Console.WriteLine($"🔄 Starting to save {products.Count} Adams products to MongoDB...");

            foreach (var product in products)
            {
                try
                {
                    FilterDefinition<AdamsProduct> filter;

                    // Debug logging
                    Console.WriteLine(
                        $"Processing product: {product.Name}, SKU: '{product.Sku}', Category: {product.Category}");

                    if (!string.IsNullOrWhiteSpace(product.Sku))
                    {
                        // For numeric SKUs, use EXACT string matching (case-sensitive, no collation)
                        // This ensures "001234" != "1234" and avoids any collation issues
                        filter = Builders<AdamsProduct>.Filter.And(
                            Builders<AdamsProduct>.Filter.Eq(p => p.Sku, product.Sku), // Exact string match
                            Builders<AdamsProduct>.Filter.Eq(p => p.Category, product.Category)
                        );
                        Console.WriteLine(
                            $"Using EXACT SKU+Category filter: SKU='{product.Sku}', Category='{product.Category}'");
                    }
                    else
                    {
                        // For names, we can still use exact matching
                        filter = Builders<AdamsProduct>.Filter.And(
                            Builders<AdamsProduct>.Filter.Eq(p => p.Name, product.Name),
                            Builders<AdamsProduct>.Filter.Eq(p => p.Category, product.Category)
                        );
                        Console.WriteLine(
                            $"Using EXACT Name+Category filter: Name='{product.Name}', Category='{product.Category}'");
                    }

                    // Use exact matching - no collation options
                    var existingProduct = await collection
                        .Find(filter)
                        .FirstOrDefaultAsync();

                    if (existingProduct == null)
                    {
                        // Insert new product
                        Console.WriteLine($"✅ Inserting NEW product: {product.Name} (SKU: {product.Sku})");
                        await collection.InsertOneAsync(product);
                        stats.NewRecordsAdded++;
                    }
                    else
                    {
                        Console.WriteLine(
                            $"🔄 Found EXISTING product: {existingProduct.Name} (SKU: {existingProduct.Sku})");
                        Console.WriteLine($"   Existing ID: {existingProduct.ProductId}");
                        Console.WriteLine($"   New timestamp: {product.ScrapedTimestamp}");

                        // Update existing product
                        var update = Builders<AdamsProduct>.Update
                            .Set(p => p.Sku, product.Sku)
                            .Set(p => p.Name, product.Name)
                            .Set(p => p.ImageUrlScraped, product.ImageUrlScraped)
                            .Set(p => p.ImageFilenameLocal, product.ImageFilenameLocal)
                            .Set(p => p.ProductPageUrl, product.ProductPageUrl)
                            .Set(p => p.ScrapedFromCategoryPageUrl, product.ScrapedFromCategoryPageUrl)
                            .Set(p => p.ScrapedTimestamp, product.ScrapedTimestamp);

                        // Use exact matching for update too - no collation
                        var result = await collection.UpdateOneAsync(filter, update);

                        Console.WriteLine(
                            $"Update Result - Product: {product.Name}, Matched: {result.MatchedCount}, Modified: {result.ModifiedCount}");

                        if (result.ModifiedCount > 0)
                        {
                            stats.ExistingRecordsUpdated++;
                            Console.WriteLine($"✅ UPDATED existing product: {product.Name}");
                        }
                        else
                        {
                            stats.RecordsUnchanged++;
                            Console.WriteLine($"➡️ Product UNCHANGED: {product.Name} (data identical)");
                        }
                    }
                }
                catch (Exception ex)
                {
                    stats.Errors++;
                    Console.WriteLine(
                        $"❌ Failed to save Adams product: {product.Name}, SKU: {product.Sku}, Category: {product.Category}");
                    Console.WriteLine($"Error: {ex.Message}");
                    Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                }
            }

            Console.WriteLine(
                $"🏁 MongoDB save completed. New: {stats.NewRecordsAdded}, Updated: {stats.ExistingRecordsUpdated}, Unchanged: {stats.RecordsUnchanged}, Errors: {stats.Errors}");
            return stats;
        }

        public async Task<MongoStats> SaveMetroProductsAsync(List<MetroProduct> products)
        {
            var stats = new MongoStats();
            var collection = GetCollection<MetroProduct>("metro", "metro");

            foreach (var product in products)
            {
                try
                {
                    // Check if product already exists by ProductName and Category
                    var filter = Builders<MetroProduct>.Filter.And(
                        Builders<MetroProduct>.Filter.Eq(p => p.ProductName, product.ProductName),
                        Builders<MetroProduct>.Filter.Eq(p => p.Category, product.Category)
                    );

                    var existingProduct = await collection
                        .Find(filter)
                        .FirstOrDefaultAsync();

                    if (existingProduct == null)
                    {
                        // Insert new product
                        await collection.InsertOneAsync(product);
                        stats.NewRecordsAdded++;
                    }
                    else
                    {
                        // Update existing product
                        var update = Builders<MetroProduct>.Update
                            .Set(p => p.ProductCode, product.ProductCode)
                            .Set(p => p.ProductName, product.ProductName)
                            .Set(p => p.ProductDescription, product.ProductDescription)
                            .Set(p => p.ProductSize, product.ProductSize)
                            .Set(p => p.ProductPrice, product.ProductPrice)
                            .Set(p => p.ProductUrl, product.ProductUrl)
                            .Set(p => p.ImageName, product.ImageName)
                            .Set(p => p.ImageUrl, product.ImageUrl)
                            .Set(p => p.ImageLocation, product.ImageLocation)
                            .Set(p => p.Source, product.Source)
                            .Set(p => p.NeedsGeminiProcessing, product.NeedsGeminiProcessing)
                            .Set(p => p.ScrapedTimestamp, product.ScrapedTimestamp);

                        var result = await collection.UpdateOneAsync(filter, update);

                        if (result.ModifiedCount > 0)
                        {
                            stats.ExistingRecordsUpdated++;
                        }
                        else
                        {
                            stats.RecordsUnchanged++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    stats.Errors++;
                    Console.WriteLine(
                        $"❌ Failed to save Metro product: {product.ProductName}, Category: {product.Category}");
                    Console.WriteLine($"Error: {ex.Message}");
                }
            }

            return stats;
        }

        public void Disconnect()
        {
            _client?.Dispose();
        }
    }

    public class MongoStats
    {
        public int NewRecordsAdded { get; set; }
        public int ExistingRecordsUpdated { get; set; }
        public int RecordsUnchanged { get; set; }
        public int Errors { get; set; }
    }
}