using System.ComponentModel.DataAnnotations;

namespace WebScrapperApi.Models;

public class ScrapingOptions
{
    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public bool UseCredentials { get; set; } = false;

    public bool Headless { get; set; } = false;

    public bool DownloadImages { get; set; } = false;

    public bool StoreInMongoDB { get; set; } = false;

    [Required(ErrorMessage = "OutputFile is required.")]
    public string OutputFile { get; set; } = "products.json";
}
