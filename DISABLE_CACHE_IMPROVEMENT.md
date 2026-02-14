# Improvement: Changed --enable-cache to --disable-cache

## Summary

Replaced the `--enable-cache <true|false>` option with a simpler `--disable-cache` flag that doesn't require a value.

## Rationale

Since caching is **enabled by default**, it makes more sense to provide a simple flag to disable it rather than requiring users to specify a boolean value. This follows common CLI conventions where the default behavior doesn't need to be explicitly enabled.

### Before (awkward)
```bash
# To disable cache, users had to type:
nuget-licenses --enable-cache false

# This is verbose and unintuitive
```

### After (intuitive)
```bash
# To disable cache, users simply type:
nuget-licenses --disable-cache

# Much cleaner and more intuitive!
```

## Changes Made

### 1. CommandLineOptions.cs
- Renamed `EnableCache` property to `DisableCache`
- Updated documentation to reflect it disables caching

### 2. Program.cs
- Renamed option from `enableCacheOption` to `disableCacheOption`
- Changed type from `Option<bool?>` to `Option<bool>` (no value needed)
- Updated description: "Disable local license caching (caching is enabled by default)"
- Updated `SetHandler` to use `disableCache` parameter
- Updated `MergeSettings` to invert the logic: `settings.EnableCache = !cli.DisableCache.Value`

### 3. ConfigurationTests.cs
- Updated all test helper methods to use `DisableCache`
- Added new test: `CommandLineOptions_DisableCacheFlag_DisablesCaching`
- Updated comments to reflect new property name

### 4. README.md
- Updated all usage examples to show `--disable-cache` instead of `--enable-cache false`
- Updated command-line arguments section
- Updated License Cache section with clearer examples

## Usage Examples

```bash
# Run with cache enabled (default, no flag needed)
nuget-licenses --project-path /path/to/solution

# Run with cache disabled
nuget-licenses --project-path /path/to/solution --disable-cache

# Combine with other options
nuget-licenses -p /path --disable-cache -a -o json
```

## Testing

✅ All tests passing (66 tests: 62 original + 4 configuration tests)
✅ No compilation errors
✅ Logic correctly inverts: `DisableCache = true` → `EnableCache = false`

## Benefits

1. **More intuitive** - Users don't need to specify `false` to disable something
2. **Follows conventions** - Similar to flags like `--quiet`, `--verbose`, `--dry-run`
3. **Cleaner syntax** - `--disable-cache` vs `--enable-cache false`
4. **Less error-prone** - Can't accidentally type `--enable-cache true` (which is the default anyway)
5. **Self-documenting** - The flag name makes its purpose clear

## Configuration Compatibility

The underlying `EnableCache` setting in `LicenseCheckSettings` and configuration files (appsettings.json, environment variables) **remains unchanged**. Only the command-line flag interface changed:

- ✅ `appsettings.json` still uses `"EnableCache": true/false`
- ✅ Environment variable still is `LICENSECHECK_LicenseCheck__EnableCache`
- ✅ Only the CLI flag changed from `--enable-cache` to `--disable-cache`

This ensures backward compatibility with existing configuration files while providing a better CLI experience.

