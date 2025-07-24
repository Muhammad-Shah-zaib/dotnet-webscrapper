using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace WebScrapperApi.Services
{
    public class UtilityService(IWebHostEnvironment environment)
    {
        private readonly IWebHostEnvironment _environment = environment;

        public string SaveToJson(object data, string filename)
        {
            try
            {
                var json = JsonConvert.SerializeObject(data, Formatting.Indented);

                var folderPath = Path.Combine(_environment.ContentRootPath, "LocalStorage");

                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }

                var filePath = Path.Combine(folderPath, filename);
                File.WriteAllText(filePath, json);

                return filePath;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to save JSON file: {ex.Message}");
            }
        }


        public async Task<ImageDownloadResult?> DownloadImageAsync(string imageUrl, string productNameHash, string folderPath)
        {
            try
            {
                using var httpClient = new HttpClient();

                var response = await httpClient.GetAsync(imageUrl);

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[Warning] Failed to download image: {imageUrl}, StatusCode: {response.StatusCode}");
                    return null;
                }

                var contentType = response.Content.Headers.ContentType?.MediaType ?? "";
                if (!contentType.StartsWith("image"))
                {
                    Console.WriteLine($"[Warning] Content is not an image: {imageUrl}, Content-Type: {contentType}");
                    return null;
                }

                // Clean up extension (handle ?crop=1 etc.)
                var extension = Path.GetExtension(imageUrl.Split('?')[0]);
                if (string.IsNullOrEmpty(extension) || extension.Length > 5)
                {
                    extension = ".jpg"; // fallback
                }

                var fullFolderPath = Path.Combine(_environment.ContentRootPath, folderPath);
                if (!Directory.Exists(fullFolderPath))
                {
                    Directory.CreateDirectory(fullFolderPath);
                }

                var filename = $"{productNameHash}{extension}";
                var filePath = Path.Combine(fullFolderPath, filename);

                var imageBytes = await response.Content.ReadAsByteArrayAsync();
                await File.WriteAllBytesAsync(filePath, imageBytes);

                Console.WriteLine($"[Success] Image downloaded: {imageUrl} -> {filename}");

                return new ImageDownloadResult
                {
                    Filename = filename,
                    FilePath = filePath
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error] Exception while downloading image: {imageUrl}\n{ex}");
                return null;
            }
        }

        public string FormatProcessingTime(double seconds)
        {
            var timeSpan = TimeSpan.FromSeconds(seconds);
            return $"{timeSpan.Hours:D2}:{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}";
        }

        public string GenerateUUID()
        {
            return Guid.NewGuid().ToString();
        }

        public string GenerateHash(string input)
        {
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(input);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToHexString(hash).ToLower();
        }

        public void InstallPlaywrightAsync()
        {
            try
            {
                // Install Playwright browsers if not already installed
                var exitCode = Microsoft.Playwright.Program.Main(["install", "chromium"]);
                if (exitCode != 0)
                {
                    throw new Exception("Failed to install Playwright browsers");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to install Playwright: {ex.Message}");
            }
        }
    }

    public class ImageDownloadResult
    {
        public string Filename { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
    }
} 