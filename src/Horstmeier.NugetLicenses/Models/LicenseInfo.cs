namespace Horstmeier.NugetLicenses.Models;

public record LicenseInfo(string PackageId, string Version, string? LicenseExpression, string? LicenseUrl);
