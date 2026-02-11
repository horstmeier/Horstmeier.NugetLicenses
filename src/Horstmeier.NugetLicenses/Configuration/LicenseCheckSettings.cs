namespace Horstmeier.NugetLicenses.Configuration;

public class LicenseCheckSettings
{
    public string[] PermittedLicenses { get; set; } = [];
    public string[] ExemptPackages { get; set; } = [];
    public string ProjectPath { get; set; } = ".";
    public bool DumpPackages { get; set; }
    public string NuGetSource { get; set; } = "https://api.nuget.org/v3/index.json";
}
