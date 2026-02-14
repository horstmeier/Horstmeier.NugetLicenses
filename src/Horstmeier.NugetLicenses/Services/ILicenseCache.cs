namespace Horstmeier.NugetLicenses.Services;

public interface ILicenseCache
{
    /// <summary>
    /// Try to get a cached license info for the specified package.
    /// </summary>
    /// <param name="packageId">Package ID</param>
    /// <param name="version">Package version</param>
    /// <returns>Cached license info if available and not expired; otherwise null</returns>
    Task<Models.LicenseInfo?> TryGetAsync(string packageId, string version);

    /// <summary>
    /// Store license info in the cache.
    /// </summary>
    /// <param name="licenseInfo">License info to cache</param>
    Task SetAsync(Models.LicenseInfo licenseInfo);

    /// <summary>
    /// Save the cache to disk.
    /// </summary>
    Task SaveAsync();
}

