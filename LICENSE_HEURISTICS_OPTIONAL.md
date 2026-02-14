# License File Heuristics - Made Optional

## Summary

Successfully made the license file heuristics feature optional by default. The feature can now only be enabled explicitly through configuration or command-line options.

## Changes Made

### 1. Configuration Layer (`LicenseCheckSettings.cs`)
- Added `bool EnableLicenseFileHeuristics { get; set; } = false;`
- Defaults to **disabled** for safer, more predictable behavior

### 2. Command-Line Interface (`CommandLineOptions.cs`)
- Added `bool? EnableLicenseFileHeuristics { get; set; };`
- Allows passing the flag to override configuration

### 3. Command-Line Parsing (`Program.cs`)
- Added `--enable-license-heuristics` flag
  - No short form (intentionally verbose to discourage accidental use)
  - Description: "Enable license file heuristics to identify unknown licenses from URLs (disabled by default)"
- Updated `SetHandler` to accept the new parameter
- Updated `MergeSettings()` to handle the flag

### 4. Implementation (`NuGetLicenseResolver.cs`)
- Added `_enableLicenseFileHeuristics` field from settings
- Changed the heuristic call from unconditional to conditional:
  ```csharp
  if (_enableLicenseFileHeuristics && string.IsNullOrEmpty(licenseExpression) && !string.IsNullOrEmpty(licenseUrl))
  {
      var detected = await _analyzer.TryIdentifyFromUrlAsync(licenseUrl, ct);
      if (detected is not null)
          licenseExpression = detected;
  }
  ```

### 5. Documentation (README.md)
- Added `EnableLicenseFileHeuristics` to appsettings.json example (default: `false`)
- Updated configuration table to document the setting
- Added `--enable-license-heuristics` to command-line options list with example
- Rewrote "License File Heuristic Fallback" section to clearly state:
  - Feature is **disabled by default**
  - Three ways to enable it (config file, env var, CLI)
  - What the heuristic does
  - Supported license detection patterns

## Enabling the Feature

Users can now enable this feature in three ways:

### Via Configuration File
```json
{
  "LicenseCheck": {
    "EnableLicenseFileHeuristics": true
  }
}
```

### Via Environment Variable
```bash
export LICENSECHECK_LicenseCheck__EnableLicenseFileHeuristics=true
```

### Via Command-Line
```bash
nuget-licenses --enable-license-heuristics
```

## Default Behavior

By default (without explicit configuration), the tool will:
- ✅ Require explicit SPDX license expressions in package metadata
- ❌ Not attempt to identify licenses from URLs
- ❌ Not fetch or analyze license files
- ✅ Report packages without license expressions as violations

This is safer and more predictable for automated license compliance checking.

## Benefits

1. **Opt-in approach** — Safer, doesn't change behavior unexpectedly
2. **Clearer intent** — Users must explicitly enable heuristics
3. **Better for compliance** — Stricter by default
4. **Documentation** — Clear explanation of what the feature does
5. **Flexibility** — Easy to enable when needed

## Testing

✅ No compilation errors
✅ All configuration merging logic tested (ConfigurationTests.cs)
✅ Command-line parsing tested

## Examples in README

The README now includes clear examples:

```bash
# Enable heuristics via CLI
nuget-licenses --enable-license-heuristics

# Enable heuristics via config
# In appsettings.json: "EnableLicenseFileHeuristics": true

# Enable heuristics via environment
export LICENSECHECK_LicenseCheck__EnableLicenseFileHeuristics=true
```

