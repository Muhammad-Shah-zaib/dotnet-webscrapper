using System.ComponentModel.DataAnnotations;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace WebScrapperApi.Models;

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

    public string? OriginalFileName { get; set; }
    public string? ProductCode { get; set; }
    public string? ProductName { get; set; }
    public string? SingleCaseQuantity { get; set; }
    public string? MultipleCaseQuantity { get; set; }
    public double? SingleCasePrice { get; set; }
    public double? MultipleCasePrice { get; set; }
    public string? ProcessingStatus { get; set; }
    public string? AssignedCategoryByGemini { get; set; }
    public double? MergeConfidence { get; set; }
    public string? MergedWithAdamsProductSKU { get; set; }
    public string? MergedWithAdamsProductName { get; set; }
    public string? MatchedCKFastFoodProductId { get; set; }
    public double? CkFastFoodMatchScore { get; set; }

    public string Source { get; set; } = "AdamsFoodService_Standalone_Mongo";
    public DateTime ScrapedTimestamp { get; set; }
}