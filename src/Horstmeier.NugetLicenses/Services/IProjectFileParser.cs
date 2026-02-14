using Horstmeier.NugetLicenses.Models;

namespace Horstmeier.NugetLicenses.Services;

public interface IProjectFileParser
{
    bool IsPackageLockEnabled(string projectFilePath);
    string? GetCustomLockFilePath(string projectFilePath);
    Task<IReadOnlyList<PackageReference>> GetPackagesAsync(string projectFilePath, CancellationToken ct = default);
}
