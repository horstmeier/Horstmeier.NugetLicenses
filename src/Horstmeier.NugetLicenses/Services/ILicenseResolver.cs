using Horstmeier.NugetLicenses.Models;

namespace Horstmeier.NugetLicenses.Services;

public interface ILicenseResolver
{
    Task<IReadOnlyList<LicenseInfo>> ResolveAsync(IReadOnlyList<PackageReference> packages, CancellationToken cancellationToken = default);
}
