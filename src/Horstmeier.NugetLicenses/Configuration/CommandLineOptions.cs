namespace Horstmeier.NugetLicenses.Configuration;

/// <summary>
/// Command-line options that can override configuration settings.
/// Null values indicate the option was not provided on the command line.
/// </summary>
public class CommandLineOptions
{
    /// <summary>
    /// Root directory to scan recursively for packages.lock.json files.
    /// </summary>
    public string? ProjectPath { get; set; }

    /// <summary>
    /// Include all packages in the report (not just violations).
    /// </summary>
    public bool? ShowAllPackages { get; set; }

    /// <summary>
    /// Report format: console, markdown, or json.
    /// </summary>
    public string? OutputFormat { get; set; }

    /// <summary>
    /// Suppress info logging, only output the report.
    /// </summary>
    public bool? Quiet { get; set; }

    /// <summary>
    /// Disable local license caching (caching is enabled by default).
    /// </summary>
    public bool? DisableCache { get; set; }

    /// <summary>
    /// Number of days to keep cached license information.
    /// </summary>
    public int? CacheDurationDays { get; set; }

    /// <summary>
    /// NuGet v3 API source URL.
    /// </summary>
    public string? NuGetSource { get; set; }

    /// <summary>
    /// Enable license file heuristics to identify unknown licenses from URLs.
    /// </summary>
    public bool? EnableLicenseFileHeuristics { get; set; }
}
