using System.ComponentModel.DataAnnotations;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace WebScrapperApi.Models;

public class MetroProduct
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [Key]
    public string ProductId { get; set; } = string.Empty;

    public string? ProductCode { get; set; }
    public string? ProductName { get; set; }
    public string? ProductDescription { get; set; }
    public string? ProductSize { get; set; }
    public string? ProductPrice { get; set; }
    public string? ProductUrl { get; set; }

    public string? ImageName { get; set; }
    public string? ImageUrl { get; set; }
    public string? ImageLocation { get; set; }

    public string? Category { get; set; }

    public string Source { get; set; } = "metro";
    public bool NeedsGeminiProcessing { get; set; } = true;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public string? MatchedOutputJsonProductId { get; set; }
    public double? MatchConfidence { get; set; }
    public string? MatchedCKFastFoodProductId { get; set; }
    public double? CkFastFoodMatchScore { get; set; }

    public DateTime ScrapedTimestamp { get; set; }
}