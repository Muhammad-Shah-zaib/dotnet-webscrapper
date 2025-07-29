namespace WebScrapperApi.Services;

public enum LogLevel
{
    Information,
    Warning,
    Error,
    Critical
}

public class LoggerService(ILogger<LoggerService> logger)
{
    private readonly string _logDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Logs");


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
                    logger.LogInformation(logMessage);
                    break;
                case LogLevel.Warning:
                    logger.LogWarning(logMessage);
                    break;
                case LogLevel.Error:
                    logger.LogError(logMessage);
                    break;
                case LogLevel.Critical:
                    logger.LogCritical(logMessage);
                    break;
            }

            File.AppendAllText(logFilePath, logMessage);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to write to log file.");
        }
    }
}