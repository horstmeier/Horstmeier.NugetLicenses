namespace Horstmeier.NugetLicenses.Models;

public record VersionCheckResult(string PackageId, string CurrentVersion, string LatestVersion, bool IsOutdated);
