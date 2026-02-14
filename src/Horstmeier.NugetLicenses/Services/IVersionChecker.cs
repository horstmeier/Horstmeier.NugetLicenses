using Horstmeier.NugetLicenses.Models;

namespace Horstmeier.NugetLicenses.Services;

public interface IVersionChecker
{
    /// <summary>
    /// Check for newer stable versions of the specified packages.
    /// </summary>
    /// <param name="packages">Packages to check</param>
    /// <param name="includeTransitive">When false, only direct packages are checked</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Version check results for each checked package</returns>
    Task<IReadOnlyList<VersionCheckResult>> CheckAsync(
        IReadOnlyList<PackageReference> packages,
        bool includeTransitive,
        CancellationToken cancellationToken = default);
}
