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
    /// Report format: console, markdown, html, or json.
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

    /// <summary>
    /// Explicit config file path. When set, skips all auto-discovered config files
    /// (global and project-level). Only built-in defaults, this file, environment
    /// variables, and CLI options apply.
    /// </summary>
    public string? ConfigFile { get; set; }

    /// <summary>
    /// Check for newer versions of direct dependencies.
    /// </summary>
    public bool? CheckUpdates { get; set; }

    /// <summary>
    /// Check for newer versions of all packages, including transitive dependencies.
    /// </summary>
    public bool? CheckUpdatesAll { get; set; }

    /// <summary>
    /// API key for authenticating against a private NuGet feed.
    /// </summary>
    public string? NuGetApiKey { get; set; }

    /// <summary>
    /// Directory for storing the license and version cache files.
    /// Defaults to ~/.nugetlicenses when not set.
    /// </summary>
    public string? CacheDir { get; set; }

    /// <summary>
    /// Fail if any project has RestorePackagesWithLockFile enabled but the lock file is missing.
    /// </summary>
    public bool? RequireLockFiles { get; set; }
}
