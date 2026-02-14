using System.Collections.Concurrent;
using System.Text.Json;
using Horstmeier.NugetLicenses.Configuration;
using Horstmeier.NugetLicenses.Models;
using Microsoft.Extensions.Logging;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace Horstmeier.NugetLicenses.Services;

public class VersionChecker : IVersionChecker
{
    private const int MaxDegreeOfParallelism = 8;
    private const int VersionCacheDurationDays = 7;

    private readonly ILogger<VersionChecker> _logger;
    private readonly string _nuGetSource;
    private readonly string? _nuGetApiKey;
    private readonly bool _enableCache;
    private readonly string _cacheFilePath;
    private readonly Dictionary<string, VersionCacheEntry> _cache = new();
    private readonly SemaphoreSlim _cacheLock = new(1, 1);
    private bool _isCacheDirty;

    public VersionChecker(LicenseCheckSettings settings, ILogger<VersionChecker> logger)
    {
        _logger = logger;
        _nuGetSource = settings.NuGetSource;
        _nuGetApiKey = settings.NuGetApiKey;
        _enableCache = settings.EnableCache;

        var cacheDir = settings.CacheDirectory
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nugetlicenses");
        Directory.CreateDirectory(cacheDir);
        _cacheFilePath = Path.Combine(cacheDir, "version-cache.json");

        if (_enableCache)
            LoadCache();
    }

    public async Task<IReadOnlyList<VersionCheckResult>> CheckAsync(
        IReadOnlyList<PackageReference> packages,
        bool includeTransitive,
        CancellationToken cancellationToken = default)
    {
        var filtered = includeTransitive
            ? packages
            : packages.Where(p => !string.Equals(p.Type, "Transitive", StringComparison.OrdinalIgnoreCase)).ToList();

        if (filtered.Count == 0)
            return [];

        // Deduplicate by package ID — latest version is the same regardless of which version is installed
        var distinctById = filtered
            .GroupBy(p => p.Id, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

        var packageSource = new PackageSource(_nuGetSource);
        if (!string.IsNullOrEmpty(_nuGetApiKey))
            packageSource.Credentials = new PackageSourceCredential(
                _nuGetSource, "user", _nuGetApiKey, isPasswordClearText: true, validAuthenticationTypesText: null);
        var repository = Repository.Factory.GetCoreV3(packageSource);
        var findResource = await repository.GetResourceAsync<FindPackageByIdResource>(cancellationToken);
        var sourceCache = new SourceCacheContext();

        var latestVersionMap = new ConcurrentDictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        await Parallel.ForEachAsync(distinctById,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = MaxDegreeOfParallelism,
                CancellationToken = cancellationToken
            },
            async (package, ct) =>
            {
                var cacheKey = package.Id.ToLowerInvariant();

                if (_enableCache)
                {
                    var cached = await TryGetCachedAsync(cacheKey);
                    if (cached != null)
                    {
                        latestVersionMap[package.Id] = cached;
                        return;
                    }
                }

                try
                {
                    var versions = await findResource.GetAllVersionsAsync(package.Id, sourceCache, NullLogger.Instance, ct);
                    var latestStable = versions
                        .Where(v => !v.IsPrerelease)
                        .OrderByDescending(v => v)
                        .FirstOrDefault();

                    var latestVersion = latestStable?.ToString();
                    latestVersionMap[package.Id] = latestVersion;

                    if (_enableCache && latestVersion != null)
                        await SetCachedAsync(cacheKey, latestVersion);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to check latest version for {PackageId}", package.Id);
                    latestVersionMap[package.Id] = null;
                }
            });

        if (_enableCache && _isCacheDirty)
            await SaveCacheAsync();

        var results = new List<VersionCheckResult>();
        foreach (var package in filtered)
        {
            if (!latestVersionMap.TryGetValue(package.Id, out var latestVersion) || latestVersion == null)
                continue;

            var isOutdated = NuGetVersion.TryParse(package.Version, out var current)
                && NuGetVersion.TryParse(latestVersion, out var latest)
                && latest > current;

            results.Add(new VersionCheckResult(package.Id, package.Version, latestVersion, isOutdated));
        }

        return results;
    }

    private async Task<string?> TryGetCachedAsync(string cacheKey)
    {
        await _cacheLock.WaitAsync();
        try
        {
            if (_cache.TryGetValue(cacheKey, out var entry))
            {
                if (DateTime.UtcNow - entry.CheckedAt <= TimeSpan.FromDays(VersionCacheDurationDays))
                {
                    _logger.LogDebug("Version cache hit for {PackageId}", cacheKey);
                    return entry.LatestVersion;
                }
                _cache.Remove(cacheKey);
                _isCacheDirty = true;
            }
            return null;
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    private async Task SetCachedAsync(string cacheKey, string latestVersion)
    {
        await _cacheLock.WaitAsync();
        try
        {
            _cache[cacheKey] = new VersionCacheEntry(cacheKey, latestVersion, DateTime.UtcNow);
            _isCacheDirty = true;
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    private void LoadCache()
    {
        if (!File.Exists(_cacheFilePath))
            return;

        try
        {
            var json = File.ReadAllText(_cacheFilePath);
            var entries = JsonSerializer.Deserialize<List<VersionCacheEntry>>(json);
            if (entries != null)
            {
                foreach (var entry in entries)
                    _cache[entry.PackageId] = entry;
                _logger.LogDebug("Loaded {Count} entries from version cache", _cache.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load version cache from {Path}, starting fresh", _cacheFilePath);
        }
    }

    private async Task SaveCacheAsync()
    {
        await _cacheLock.WaitAsync();
        try
        {
            if (!_isCacheDirty)
                return;

            var json = JsonSerializer.Serialize(_cache.Values.ToList(), new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_cacheFilePath, json);
            _isCacheDirty = false;
            _logger.LogDebug("Saved {Count} entries to version cache at {Path}", _cache.Count, _cacheFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save version cache to {Path}", _cacheFilePath);
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    private record VersionCacheEntry(string PackageId, string LatestVersion, DateTime CheckedAt);
}
