namespace Horstmeier.NugetLicenses.Configuration;

public class LicenseCheckSettings
{
    public string[] PermittedLicenses { get; set; } = [];
    public string[] ExemptPackages { get; set; } = [];
    public string ProjectPath { get; set; } = ".";
    public bool DumpPackages { get; set; }
}
