using System.ComponentModel.DataAnnotations;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace WebScrapperApi.Models;

public class CaterChoiceProduct
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
    public string? ProductSinglePrice { get; set; }
    public string? ProductCasePrice { get; set; }
    public string? ProductUrl { get; set; }

    public string? OriginalImageUrl { get; set; }
    public string? LocalImageFilename { get; set; }
    public string? LocalImageFilepath { get; set; }

    public string? Category { get; set; }

    public DateTime ScrapedTimestamp { get; set; }
    public string Source { get; set; } = "CaterChoice_Standalone_Mongo";

    public string? MatchedCKFastFoodProductId { get; set; } = string.Empty;
    public double? CkFastFoodMatchScore { get; set; }
}