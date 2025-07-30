namespace WebScrapperApi.Services;

public enum LogLevel
{
    Information,
    Warning,
    Error,
    Critical
}

public class LoggerService(ILogger<LoggerService> logger, IWebHostEnvironment environment)
{
    private readonly ILogger<LoggerService> _logger = logger;
    private readonly string _logDirectory = Path.Combine(environment.ContentRootPath, "Logs");

    public void Log(string scraperName, LogLevel logLevel, string message)
    {
        try
        {
            var timestamp = DateTime.Now.ToString("dd-ddd-MMMM-yyyy");
            var scraperLogDir = Path.Combine(_logDirectory, scraperName, timestamp);
            Directory.CreateDirectory(scraperLogDir);

            var logFilePath = Path.Combine(scraperLogDir, $"{logLevel}.log");
            var logMessage = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - [{scraperName}] - {message}{Environment.NewLine}";

            switch (logLevel)
            {
                case LogLevel.Information:
                    _logger.LogInformation(logMessage);
                    break;
                case LogLevel.Warning:
                    _logger.LogWarning(logMessage);
                    break;
                case LogLevel.Error:
                    _logger.LogError(logMessage);
                    break;
                case LogLevel.Critical:
                    _logger.LogCritical(logMessage);
                    break;
            }

            File.AppendAllText(logFilePath, logMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write to log file.");
        }
    }
}
