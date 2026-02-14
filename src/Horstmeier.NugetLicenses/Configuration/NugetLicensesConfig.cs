namespace Horstmeier.NugetLicenses.Configuration;

/// <summary>
/// Represents the content of a nuget-licenses.json configuration file.
/// Only explicitly set (non-null) properties override settings from lower-priority layers.
/// </summary>
internal class NugetLicensesConfig
{
    public string[]? PermittedLicenses { get; set; }
    public ExemptPackage[]? ExemptPackages { get; set; }
    public string? NuGetSource { get; set; }
    public bool? EnableCache { get; set; }
    public int? CacheDurationDays { get; set; }
    public bool? EnableLicenseFileHeuristics { get; set; }
    public string? NuGetApiKey { get; set; }
    public string? CacheDirectory { get; set; }
    public bool? RequireLockFiles { get; set; }
}
