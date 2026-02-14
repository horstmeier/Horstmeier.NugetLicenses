using Microsoft.Extensions.Logging;

namespace Horstmeier.NugetLicenses.Logging;

/// <summary>
/// Extension methods for logging configuration.
/// </summary>
public static class LoggingExtensions
{
    /// <summary>
    /// Adds a console logger that writes to stderr instead of stdout.
    /// This ensures that only report output goes to stdout.
    /// </summary>
    public static ILoggingBuilder AddStderrConsole(this ILoggingBuilder builder)
    {
        return builder.AddProvider(new StderrConsoleLoggerProvider());
    }

    /// <summary>
    /// Adds a console logger that writes to stderr with a specific minimum log level.
    /// </summary>
    public static ILoggingBuilder AddStderrConsole(this ILoggingBuilder builder, LogLevel minimumLevel)
    {
        return builder.AddProvider(new StderrConsoleLoggerProvider(minimumLevel));
    }
}

