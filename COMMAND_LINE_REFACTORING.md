# Command-Line Refactoring Summary

## Overview
Refactored command-line argument handling from using Microsoft.Extensions.Configuration.CommandLine with manual boolean flag normalization to using System.CommandLine library for proper command-line parsing with kebab-case arguments.

## Changes Made

### 1. Updated Project File
**File**: `Horstmeier.NugetLicenses.csproj`
- Changed version from `1.0.0` to `0.1.0-beta.0`
- Removed `Microsoft.Extensions.Configuration.CommandLine` package
- Added `System.CommandLine` version `2.0.0-beta4.22272.1`

### 2. Created CommandLineOptions Class
**File**: `Configuration/CommandLineOptions.cs` (new)
- Defined POCO for command-line options
- All properties are nullable to distinguish "not provided" from "provided with value"
- Properties: `ProjectPath`, `ShowAllPackages`, `OutputFormat`, `Quiet`, `EnableCache`, `CacheDurationDays`, `NuGetSource`
- Includes XML documentation for each property

### 3. Refactored Program.cs
**File**: `Program.cs`
- **Removed**: Manual boolean flag normalization (lines 9-14)
- **Removed**: `.AddCommandLine()` from ConfigurationBuilder
- **Removed**: Manual CLI override logic (lines 27-38)
- **Added**: `using System.CommandLine;`
- **Added**: `RootCommand` definition with description
- **Added**: `Option<T>` definitions for each command-line argument with:
  - Kebab-case names (e.g., `--project-path`, `--show-all-packages`)
  - Short aliases (e.g., `-p`, `-a`, `-o`, `-q`)
  - Descriptive help text
  - Default values where appropriate
- **Added**: Validation for `--output-format` option
- **Added**: `SetHandler` to parse arguments into `CommandLineOptions`
- **Added**: `MergeSettings()` helper method to merge CLI options with configuration settings
- **Changed**: Configuration layering now: appsettings.json → environment variables → CLI args

### 4. Updated Configuration Tests
**File**: `tests/ConfigurationTests.cs`
- **Removed**: `Settings_CommandLineOverride_TakesPrecedence` test (old approach)
- **Added**: `CommandLineOptions_MergeWithSettings_OverridesCorrectly` test
- **Added**: `CommandLineOptions_NullValues_DontOverrideSettings` test
- **Added**: `CommandLineOptions_QuietFlag_ReturnsCorrectly` test
- **Added**: `MergeSettingsForTest()` helper method that mirrors Program.cs logic

### 5. Updated README
**File**: `README.md`
- Updated all command-line examples to use kebab-case
- Added `--help` command example at the top
- Created dedicated "Command-line arguments" section with:
  - Complete list of options with aliases
  - Clear kebab-case syntax documentation
  - Updated examples using new syntax
- Updated cache configuration examples to use kebab-case
- Clarified configuration layering precedence

## Command-Line Syntax Changes

### Before (PascalCase)
```bash
dotnet run -- --ProjectPath=/path --ShowAllPackages --OutputFormat=json --Quiet
```

### After (kebab-case)
```bash
nuget-licenses --project-path /path --show-all-packages --output-format json --quiet
# Or with short aliases:
nuget-licenses -p /path -a -o json -q
```

## Available Options

| Option | Alias | Type | Description |
|--------|-------|------|-------------|
| `--project-path` | `-p` | string | Root directory to scan |
| `--show-all-packages` | `-a` | bool | Include all packages in report |
| `--output-format` | `-o` | string | Report format (console/markdown/json) |
| `--quiet` | `-q` | bool | Suppress info logging |
| `--enable-cache` | | bool? | Enable/disable caching |
| `--cache-duration-days` | | int? | Cache duration in days |
| `--nuget-source` | | string? | Custom NuGet API URL |
| `--help` | `-h`, `-?` | | Show help information |

## Benefits

1. **Better UX**: Auto-generated help with `--help`
2. **Type Safety**: Automatic type conversion and validation
3. **Standards Compliance**: Follows .NET CLI conventions (kebab-case)
4. **Cleaner Code**: Removed manual normalization and override logic
5. **Better Validation**: Built-in validation support (e.g., output format)
6. **Short Aliases**: Convenient single-letter shortcuts
7. **Better Error Messages**: Clear error messages for invalid arguments

## Test Results
All 65 tests passing (62 original + 3 new configuration tests)

## Breaking Changes
- Command-line syntax changed from PascalCase to kebab-case
- This is acceptable as the tool has not been published yet (version 0.1.0-beta.0)

## Migration Guide for Users
Users need to update their scripts/CI configs:
- `--ProjectPath` → `--project-path` or `-p`
- `--ShowAllPackages` → `--show-all-packages` or `-a`
- `--OutputFormat` → `--output-format` or `-o`
- `--Quiet` → `--quiet` or `-q`
- `--EnableCache` → `--enable-cache`
- `--CacheDurationDays` → `--cache-duration-days`

