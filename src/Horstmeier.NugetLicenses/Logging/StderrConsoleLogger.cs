using Microsoft.Extensions.Logging;

namespace Horstmeier.NugetLicenses.Logging;

/// <summary>
/// A console logger that writes to stderr instead of stdout.
/// This ensures that only the report output goes to stdout.
/// </summary>
public class StderrConsoleLogger : ILogger
{
    private readonly string _categoryName;
    private readonly LogLevel _minimumLevel;

    public StderrConsoleLogger(string categoryName, LogLevel minimumLevel)
    {
        _categoryName = categoryName;
        _minimumLevel = minimumLevel;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= _minimumLevel;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
            return;

        var message = formatter(state, exception);
        if (string.IsNullOrEmpty(message))
            return;

        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        var levelString = GetLevelString(logLevel);
        
        // Write to stderr
        Console.Error.WriteLine($"[{timestamp}] [{levelString}] {message}");

        if (exception is not null)
            Console.Error.WriteLine(exception.ToString());
    }

    private static string GetLevelString(LogLevel level) => level switch
    {
        LogLevel.Trace => "TRCE",
        LogLevel.Debug => "DBUG",
        LogLevel.Information => "INFO",
        LogLevel.Warning => "WARN",
        LogLevel.Error => "ERRR",
        LogLevel.Critical => "CRIT",
        _ => level.ToString().ToUpperInvariant()[..4]
    };
}

