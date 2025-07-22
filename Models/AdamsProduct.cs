using System.ComponentModel.DataAnnotations;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace WebScrapperApi.Models
{
    public class AdamsProduct
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [Key]
        public string ProductId { get; set; } = string.Empty;
        public string? Name { get; set; }
        public string? Sku { get; set; }
        public string? ImageUrlScraped { get; set; }
        public string? ImageFilenameLocal { get; set; }
        public string? Category { get; set; }
        public string? ProductPageUrl { get; set; }
        public string? ScrapedFromCategoryPageUrl { get; set; }
        public string Source { get; set; } = "AdamsFoodService_Standalone_Mongo";
        public DateTime ScrapedTimestamp { get; set; }
    }
} 