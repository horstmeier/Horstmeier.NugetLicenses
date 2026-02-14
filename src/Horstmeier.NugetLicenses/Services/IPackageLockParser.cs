using Horstmeier.NugetLicenses.Models;

namespace Horstmeier.NugetLicenses.Services;

public interface IPackageLockParser
{
    IReadOnlyList<PackageReference> Parse(string lockFilePath);
    Task<DirectoryScanResult> ParseDirectoryAsync(string rootPath, CancellationToken ct = default);
}
