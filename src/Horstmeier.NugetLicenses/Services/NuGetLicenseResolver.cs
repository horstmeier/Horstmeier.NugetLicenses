using System.Collections.Concurrent;
using Horstmeier.NugetLicenses.Configuration;
using Horstmeier.NugetLicenses.Models;
using Microsoft.Extensions.Logging;
using NuGet.Common;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace Horstmeier.NugetLicenses.Services;

public class NuGetLicenseResolver : ILicenseResolver
{
    private const int MaxDegreeOfParallelism = 8;
    private readonly ILogger<NuGetLicenseResolver> _logger;
    private readonly ILicenseFileAnalyzer _analyzer;
    private readonly string _nuGetSource;

    public NuGetLicenseResolver(LicenseCheckSettings settings, ILicenseFileAnalyzer analyzer, ILogger<NuGetLicenseResolver> logger)
    {
        _nuGetSource = settings.NuGetSource;
        _analyzer = analyzer;
        _logger = logger;
    }

    public async Task<IReadOnlyList<LicenseInfo>> ResolveAsync(
        IReadOnlyList<PackageReference> packages,
        CancellationToken cancellationToken = default)
    {
        var repository = Repository.Factory.GetCoreV3(_nuGetSource);
        var metadataResource = await repository.GetResourceAsync<PackageMetadataResource>(cancellationToken);

        var results = new ConcurrentBag<LicenseInfo>();
        var cache = new SourceCacheContext();

        await Parallel.ForEachAsync(packages,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = MaxDegreeOfParallelism,
                CancellationToken = cancellationToken
            },
            async (package, ct) =>
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
                        ct);

                    if (metadata is null)
                    {
                        _logger.LogWarning("No metadata found for {PackageId} {Version}", package.Id, package.Version);
                        results.Add(new LicenseInfo(package.Id, package.Version, null, null));
                        return;
                    }

                    var licenseExpression = metadata.LicenseMetadata?.LicenseExpression?.ToString();
                    var licenseUrl = metadata.LicenseUrl?.ToString();

                    if (string.IsNullOrEmpty(licenseExpression) && !string.IsNullOrEmpty(licenseUrl))
                    {
                        var detected = await _analyzer.TryIdentifyFromUrlAsync(licenseUrl, ct);
                        if (detected is not null)
                            licenseExpression = detected;
                    }

                    results.Add(new LicenseInfo(package.Id, package.Version, licenseExpression, licenseUrl));
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to resolve license for {PackageId} {Version}", package.Id, package.Version);
                    results.Add(new LicenseInfo(package.Id, package.Version, null, null));
                }
            });

        return results.OrderBy(r => r.PackageId).ThenBy(r => r.Version).ToList();
    }
}
