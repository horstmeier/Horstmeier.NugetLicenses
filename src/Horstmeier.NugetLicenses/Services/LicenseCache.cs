using System.Text.Json;
using Horstmeier.NugetLicenses.Configuration;
using Horstmeier.NugetLicenses.Models;
using Microsoft.Extensions.Logging;

namespace Horstmeier.NugetLicenses.Services;

public class LicenseCache : ILicenseCache
{
    private readonly string _cacheFilePath;
    private readonly int _cacheDurationDays;
    private readonly ILogger<LicenseCache> _logger;
    private readonly Dictionary<string, CachedLicenseEntry> _cache = new();
    private readonly SemaphoreSlim _lock = new(1, 1);
    private bool _isDirty;

    public LicenseCache(LicenseCheckSettings settings, ILogger<LicenseCache> logger)
        : this(settings, logger, null)
    {
    }

    protected LicenseCache(LicenseCheckSettings settings, ILogger<LicenseCache> logger, string? cacheDirectory)
    {
        _logger = logger;
        _cacheDurationDays = settings.CacheDurationDays;

        var cacheDir = cacheDirectory ?? GetDefaultCacheDirectory();
        Directory.CreateDirectory(cacheDir);
        _cacheFilePath = Path.Combine(cacheDir, "cache.json");

        LoadCache();
    }

    private static string GetDefaultCacheDirectory()
    {
        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(homeDir, ".nugetlicenses");
    }

    private void LoadCache()
    {
        if (!File.Exists(_cacheFilePath))
        {
            _logger.LogDebug("Cache file not found at {Path}, starting with empty cache", _cacheFilePath);
            return;
        }

        try
        {
            var json = File.ReadAllText(_cacheFilePath);
            var entries = JsonSerializer.Deserialize<List<CachedLicenseEntry>>(json);
            
            if (entries != null)
            {
                foreach (var entry in entries)
                {
                    var key = GetKey(entry.PackageId, entry.Version);
                    _cache[key] = entry;
                }
                _logger.LogInformation("Loaded {Count} entries from license cache", _cache.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load license cache from {Path}, starting with empty cache", _cacheFilePath);
        }
    }

    public async Task<LicenseInfo?> TryGetAsync(string packageId, string version)
    {
        await _lock.WaitAsync();
        try
        {
            var key = GetKey(packageId, version);
            if (_cache.TryGetValue(key, out var entry))
            {
                // Check if cache entry is expired
                if (DateTime.UtcNow - entry.CachedAt <= TimeSpan.FromDays(_cacheDurationDays))
                {
                    _logger.LogDebug("Cache hit for {PackageId} {Version}", packageId, version);
                    return new LicenseInfo(entry.PackageId, entry.Version, entry.LicenseExpression, entry.LicenseUrl);
                }
                else
                {
                    _logger.LogDebug("Cache entry expired for {PackageId} {Version}", packageId, version);
                    _cache.Remove(key);
                    _isDirty = true;
                }
            }
            return null;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SetAsync(LicenseInfo licenseInfo)
    {
        await _lock.WaitAsync();
        try
        {
            var key = GetKey(licenseInfo.PackageId, licenseInfo.Version);
            var entry = new CachedLicenseEntry(
                licenseInfo.PackageId,
                licenseInfo.Version,
                licenseInfo.LicenseExpression,
                licenseInfo.LicenseUrl,
                DateTime.UtcNow);

            _cache[key] = entry;
            _isDirty = true;
            _logger.LogDebug("Cached license for {PackageId} {Version}", licenseInfo.PackageId, licenseInfo.Version);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveAsync()
    {
        await _lock.WaitAsync();
        try
        {
            if (!_isDirty)
                return;

            var entries = _cache.Values.ToList();
            var json = JsonSerializer.Serialize(entries, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            await File.WriteAllTextAsync(_cacheFilePath, json);
            _isDirty = false;
            _logger.LogInformation("Saved {Count} entries to license cache at {Path}", entries.Count, _cacheFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save license cache to {Path}", _cacheFilePath);
        }
        finally
        {
            _lock.Release();
        }
    }

    private static string GetKey(string packageId, string version)
    {
        return $"{packageId.ToLowerInvariant()}|{version.ToLowerInvariant()}";
    }

    private record CachedLicenseEntry(
        string PackageId,
        string Version,
        string? LicenseExpression,
        string? LicenseUrl,
        DateTime CachedAt);
}


