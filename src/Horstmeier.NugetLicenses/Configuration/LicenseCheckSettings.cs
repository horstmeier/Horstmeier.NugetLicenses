namespace Horstmeier.NugetLicenses.Configuration;

public class LicenseCheckSettings
{
    public string[] PermittedLicenses { get; set; } = [];
    public ExemptPackage[] ExemptPackages { get; set; } = [];
    public string ProjectPath { get; set; } = ".";
    public bool ShowAllPackages { get; set; }
    public string OutputFormat { get; set; } = "console";
    public string NuGetSource { get; set; } = "https://api.nuget.org/v3/index.json";
    public bool EnableCache { get; set; } = true;
    public int CacheDurationDays { get; set; } = 365;
    public bool EnableLicenseFileHeuristics { get; set; } = false;
    public bool CheckUpdates { get; set; } = false;
    public bool CheckUpdatesAll { get; set; } = false;
    public string? NuGetApiKey { get; set; } = null;
    public string? CacheDirectory { get; set; } = null;
    public bool RequireLockFiles { get; set; } = false;
}
