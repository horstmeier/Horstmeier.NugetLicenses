namespace Horstmeier.NugetLicenses.Models;

public record PackageReportEntry(
    string PackageId,
    string Version,
    string License,
    bool IsViolation,
    string? Reason,
    IReadOnlyList<string> Projects);
