namespace Horstmeier.NugetLicenses.Configuration;

public class ExemptPackage
{
    public string PackageName { get; set; } = "";
    public string? Version { get; set; }
    public string? Reason { get; set; }
}
