using Microsoft.Extensions.Logging;

namespace Horstmeier.NugetLicenses.Logging;

/// <summary>
/// A console logger provider that creates loggers writing to stderr.
/// </summary>
public class StderrConsoleLoggerProvider : ILoggerProvider
{
    private readonly LogLevel _minimumLevel;

    public StderrConsoleLoggerProvider(LogLevel minimumLevel = LogLevel.Information)
    {
        _minimumLevel = minimumLevel;
    }

    public ILogger CreateLogger(string categoryName) => new StderrConsoleLogger(categoryName, _minimumLevel);

    public void Dispose() { }
}

