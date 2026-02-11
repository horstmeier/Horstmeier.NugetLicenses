namespace Horstmeier.NugetLicenses.Models;

public record LicenseViolation(string PackageId, string Version, string? LicenseExpression, string? LicenseUrl, string Reason);

public record LicenseValidationResult(IReadOnlyList<LicenseViolation> Violations, int TotalPackages, int ValidPackages)
{
    public bool HasViolations => Violations.Count > 0;
}
