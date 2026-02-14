# License Cache Implementation Summary

## Overview
Implemented a local license cache system for the NuGet Licenses tool to significantly improve performance by caching license information retrieved from the NuGet API.

## Files Created

### 1. `ILicenseCache.cs`
Interface defining the cache contract with methods:
- `TryGetAsync(packageId, version)` - Retrieve cached license info
- `SetAsync(licenseInfo)` - Store license info in cache
- `SaveAsync()` - Persist cache to disk

### 2. `LicenseCache.cs`
Concrete implementation of the license cache:
- Stores cache in `~/.nugetlicenses/cache.json` (Linux/macOS) or `%USERPROFILE%\.nugetlicenses\cache.json` (Windows)
- Thread-safe operations using `SemaphoreSlim`
- Automatic expiration based on configurable duration
- JSON serialization for persistence
- Case-insensitive package lookups

### 3. `LicenseCacheTests.cs`
Comprehensive test suite with 6 tests covering:
- Cache hit/miss scenarios
- Persistence across instances
- Case-insensitive lookups
- Entry storage and retrieval

## Files Modified

### 1. `LicenseCheckSettings.cs`
Added two new configuration properties:
- `EnableCache` (bool, default: true) - Toggle caching on/off
- `CacheDurationDays` (int, default: 365) - Number of days to cache entries

### 2. `NuGetLicenseResolver.cs`
Enhanced to use the cache:
- Optional `ILicenseCache` dependency injection
- Checks cache before fetching from NuGet API
- Caches results after fetching (including failures to avoid repeated errors)
- Saves cache after all packages are resolved

### 3. `Program.cs`
Updated service registration:
- Conditionally registers `ILicenseCache` based on `EnableCache` setting
- Maintains proper dependency injection order

### 4. `appsettings.json`
Added cache configuration:
```json
"EnableCache": true,
"CacheDurationDays": 7
```

### 5. `README.md`
Added comprehensive documentation:
- Cache location details for each platform
- Configuration options
- Command-line examples
- Manual cache clearing instructions

## Key Features

1. **Platform-Aware**: Automatically uses the correct cache location for Windows, Linux, and macOS

2. **Performance**: First run fetches from NuGet API, subsequent runs use cached data (significantly faster)

3. **Configurable Expiration**: Cache entries expire after a configurable number of days (default: 7)

4. **Thread-Safe**: Safe for concurrent access

5. **Automatic Cleanup**: Expired entries are automatically removed on access

6. **Failure Caching**: Failed lookups are also cached to avoid repeated failed API calls

7. **Easy Testing**: Protected constructor allows test isolation with custom cache directories

## Configuration Examples

### Disable cache via environment variable
```bash
export LICENSECHECK_LicenseCheck__EnableCache=false
```

### Disable cache via command line
```bash
dotnet run -- --EnableCache=false
```

### Set custom cache duration (30 days)
```bash
dotnet run -- --CacheDurationDays=30
```

### Clear cache manually
```bash
# Linux/macOS
rm ~/.nugetlicenses/cache.json

# Windows (PowerShell)
Remove-Item $env:USERPROFILE\.nugetlicenses\cache.json
```

## Test Results
All 62 tests passing, including:
- 6 new cache-specific tests
- 56 existing tests (unchanged)

## Design Decisions

1. **JSON Storage**: Simple, human-readable, and debuggable
2. **In-Memory + Disk**: Fast in-memory operations with periodic disk persistence
3. **Case-Insensitive**: Matches NuGet's package ID conventions
4. **Optional Injection**: Cache is optional, allowing the resolver to work without it
5. **Timestamp-Based Expiration**: Simple and effective expiration strategy
6. **Write-On-Change**: Only persists to disk when cache is modified (dirty flag pattern)

## Benefits

- **Faster repeated scans**: Subsequent scans are much faster
- **Reduced API load**: Less pressure on NuGet API servers
- **Offline capability**: Can work with cached data when network is unavailable
- **Cost reduction**: Fewer API calls in CI/CD pipelines
- **Better user experience**: Near-instant results for cached packages

