using System.Collections.Concurrent;
using Horstmeier.NugetLicenses.Configuration;
using Horstmeier.NugetLicenses.Models;
using Microsoft.Extensions.Logging;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace Horstmeier.NugetLicenses.Services;

public class NuGetLicenseResolver : ILicenseResolver
{
    private const int MaxDegreeOfParallelism = 8;
    private readonly ILogger<NuGetLicenseResolver> _logger;
    private readonly ILicenseFileAnalyzer _analyzer;
    private readonly ILicenseCache? _cache;
    private readonly string _nuGetSource;
    private readonly string? _nuGetApiKey;
    private readonly bool _enableLicenseFileHeuristics;

    public NuGetLicenseResolver(
        LicenseCheckSettings settings,
        ILicenseFileAnalyzer analyzer,
        ILogger<NuGetLicenseResolver> logger,
        ILicenseCache? cache = null)
    {
        _nuGetSource = settings.NuGetSource;
        _nuGetApiKey = settings.NuGetApiKey;
        _analyzer = analyzer;
        _logger = logger;
        _cache = cache;
        _enableLicenseFileHeuristics = settings.EnableLicenseFileHeuristics;
    }

    public async Task<IReadOnlyList<LicenseInfo>> ResolveAsync(
        IReadOnlyList<PackageReference> packages,
        CancellationToken cancellationToken = default)
    {
        var packageSource = new PackageSource(_nuGetSource);
        if (!string.IsNullOrEmpty(_nuGetApiKey))
            packageSource.Credentials = new PackageSourceCredential(
                _nuGetSource, "user", _nuGetApiKey, isPasswordClearText: true, validAuthenticationTypesText: null);
        var repository = Repository.Factory.GetCoreV3(packageSource);
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
                    // Try to get from cache first
                    if (_cache is not null)
                    {
                        var cached = await _cache.TryGetAsync(package.Id, package.Version);
                        if (cached is not null)
                        {
                            results.Add(cached);
                            return;
                        }
                    }

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
                        var licenseInfo = new LicenseInfo(package.Id, package.Version, null, null);
                        results.Add(licenseInfo);
                        
                        // Cache the result even if no metadata found
                        if (_cache is not null)
                            await _cache.SetAsync(licenseInfo);
                        
                        return;
                    }

                    var licenseExpression = metadata.LicenseMetadata?.LicenseExpression?.ToString();
                    var licenseUrl = metadata.LicenseUrl?.ToString();

                    if (_enableLicenseFileHeuristics && string.IsNullOrEmpty(licenseExpression) && !string.IsNullOrEmpty(licenseUrl))
                    {
                        var detected = await _analyzer.TryIdentifyFromUrlAsync(licenseUrl, ct);
                        if (detected is not null)
                            licenseExpression = detected;
                    }

                    var resolvedLicenseInfo = new LicenseInfo(package.Id, package.Version, licenseExpression, licenseUrl);
                    results.Add(resolvedLicenseInfo);
                    
                    // Cache the result
                    if (_cache is not null)
                        await _cache.SetAsync(resolvedLicenseInfo);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to resolve license for {PackageId} {Version}", package.Id, package.Version);
                    var licenseInfo = new LicenseInfo(package.Id, package.Version, null, null);
                    results.Add(licenseInfo);
                    
                    // Cache the failed result to avoid repeated failures
                    if (_cache is not null)
                        await _cache.SetAsync(licenseInfo);
                }
            });

        // Save cache after all packages are resolved
        if (_cache is not null)
            await _cache.SaveAsync();

        return results.OrderBy(r => r.PackageId).ThenBy(r => r.Version).ToList();
    }
}
