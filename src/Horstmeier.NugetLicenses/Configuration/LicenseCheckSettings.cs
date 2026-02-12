namespace Horstmeier.NugetLicenses.Configuration;

public class LicenseCheckSettings
{
    public string[] PermittedLicenses { get; set; } = [];
    public ExemptPackage[] ExemptPackages { get; set; } = [];
    public string ProjectPath { get; set; } = ".";
    public bool ShowAllPackages { get; set; }
    public string OutputFormat { get; set; } = "console";
    public string NuGetSource { get; set; } = "https://api.nuget.org/v3/index.json";
}
