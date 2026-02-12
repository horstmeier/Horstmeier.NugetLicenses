namespace Horstmeier.NugetLicenses.Models;

public record DirectoryScanResult(
    IReadOnlyList<PackageReference> Packages,
    IReadOnlyDictionary<string, IReadOnlyList<string>> ProjectsByPackage,
    int LockFileCount);
