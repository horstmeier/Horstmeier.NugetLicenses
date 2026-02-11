using Horstmeier.NugetLicenses.Models;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace Horstmeier.NugetLicenses.Services;

public class NuGetLicenseResolver : ILicenseResolver
{
    public async Task<IReadOnlyList<LicenseInfo>> ResolveAsync(
        IReadOnlyList<PackageReference> packages,
        CancellationToken cancellationToken = default)
    {
        var repository = Repository.Factory.GetCoreV3("https://api.nuget.org/v3/index.json");
        var metadataResource = await repository.GetResourceAsync<PackageMetadataResource>(cancellationToken);

        var results = new List<LicenseInfo>();
        var cache = new SourceCacheContext();

        foreach (var package in packages)
        {
            try
            {
                var identity = new NuGet.Packaging.Core.PackageIdentity(
                    package.Id,
                    NuGetVersion.Parse(package.Version));

                var metadata = await metadataResource.GetMetadataAsync(
                    identity,
                    cache,
                    NullLogger.Instance,
                    cancellationToken);

                if (metadata is null)
                {
                    results.Add(new LicenseInfo(package.Id, package.Version, null, null));
                    continue;
                }

                var licenseExpression = metadata.LicenseMetadata?.LicenseExpression?.ToString();
                var licenseUrl = metadata.LicenseUrl?.ToString();

                results.Add(new LicenseInfo(package.Id, package.Version, licenseExpression, licenseUrl));
            }
            catch (Exception)
            {
                results.Add(new LicenseInfo(package.Id, package.Version, null, null));
            }
        }

        return results;
    }
}
