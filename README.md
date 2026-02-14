# Horstmeier.NugetLicenses

A .NET console application that scans your projects for NuGet package dependencies, resolves their licenses via the NuGet v3 API, and validates them against a configurable list of permitted SPDX licenses. Returns exit code 1 if any non-exempt package has a disallowed or missing license.

The tool discovers `.csproj` and `.fsproj` project files and extracts package dependencies from `packages.lock.json` files when available. If a lock file is missing, it falls back to using `dotnet list package` to resolve dependencies.

## Requirements

- .NET 10.0 SDK
- A project with `packages.lock.json` (enable via `<RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>` in your `.csproj`)

## Usage

```bash
# Show help
nuget-licenses --help

# Run against the current directory
dotnet run --project src/Horstmeier.NugetLicenses

# Run against a specific project path (scans recursively for packages.lock.json files)
dotnet run --project src/Horstmeier.NugetLicenses -- --project-path /path/to/your/solution
# or using short form
dotnet run --project src/Horstmeier.NugetLicenses -- -p /path/to/your/solution

# Show all packages (not just violations)
dotnet run --project src/Horstmeier.NugetLicenses -- --show-all-packages
# or using short form
dotnet run --project src/Horstmeier.NugetLicenses -- -a

# Output as markdown or JSON
dotnet run --project src/Horstmeier.NugetLicenses -- --output-format markdown
dotnet run --project src/Horstmeier.NugetLicenses -- -o json
dotnet run --project src/Horstmeier.NugetLicenses -- -o html

# Quiet mode — suppress info logging, only output the report
dotnet run --project src/Horstmeier.NugetLicenses -- --quiet
# or using short form
dotnet run --project src/Horstmeier.NugetLicenses -- -q

# Disable cache
dotnet run --project src/Horstmeier.NugetLicenses -- --disable-cache

# Set cache duration
dotnet run --project src/Horstmeier.NugetLicenses -- --cache-duration-days 30
```

Exit codes:
- `0` — all packages have permitted licenses
- `1` — one or more violations found, or `packages.lock.json` not found

## Configuration

Configuration is layered (later sources override earlier ones):

1. `appsettings.json` (defaults)
2. Environment variables (prefix `LICENSECHECK_`)
3. Command-line arguments

### appsettings.json

```json
{
  "LicenseCheck": {
    "PermittedLicenses": [
      "MIT",
      "Apache-2.0",
      "BSD-2-Clause",
      "BSD-3-Clause",
      "ISC",
      "MS-PL",
      "Unlicense",
      "0BSD"
    ],
    "ExemptPackages": [
      {
        "PackageName": "Example.Package",
        "Version": "1.0.0",
        "Reason": "Manually reviewed and approved"
      }
    ],
    "ProjectPath": ".",
    "ShowAllPackages": false,
    "OutputFormat": "console",
    "EnableLicenseFileHeuristics": false
  }
}
```

| Setting | Description                                                                                 |
|---|---------------------------------------------------------------------------------------------|
| `PermittedLicenses` | SPDX license identifiers that are allowed                                                   |
| `ExemptPackages` | Packages to skip validation (see [Exempt Packages](#exempt-packages))                       |
| `ProjectPath` | Root directory to scan recursively for `packages.lock.json` files                           |
| `ShowAllPackages` | When `true`, include all packages in the report (not just violations)                       |
| `OutputFormat` | Report format: `console` (default), `markdown`, `html`, or `json`                           |
| `NuGetSource` | NuGet v3 API source URL (default: `https://api.nuget.org/v3/index.json`)                    |
| `EnableCache` | When `true` (default), cache license information locally to speed up subsequent runs        |
| `CacheDurationDays` | Number of days to keep cached license information (default: 7)                              |
| `EnableLicenseFileHeuristics` | When `true`, enables heuristics for license detection from license files (default: `false`) |

### Environment variables

Use the `LICENSECHECK_` prefix with section separators as `__`:

```bash
export LICENSECHECK_LicenseCheck__ProjectPath=/path/to/project
```

### Command-line arguments

All command-line options use kebab-case with double dashes. Most options have short aliases with a single dash.

Available options:
- `--project-path <path>` or `-p <path>` — Root directory to scan
- `--show-all-packages` or `-a` — Include all packages in report
- `--output-format <format>` or `-o <format>` — Report format (console, markdown, json)
- `--quiet` or `-q` — Suppress info logging
- `--disable-cache` — Disable local license caching (caching is enabled by default)
- `--cache-duration-days <days>` — Cache duration in days
- `--nuget-source <url>` — Custom NuGet API URL
- `--enable-license-heuristics` — Enable license file heuristics to identify unknown licenses from URLs

Examples:
```bash
nuget-licenses --project-path /path/to/project
nuget-licenses -p /path/to/project --show-all-packages
nuget-licenses --output-format json --quiet
nuget-licenses --disable-cache
nuget-licenses --enable-license-heuristics
```

## License Cache

To speed up repeated scans, the tool caches resolved license information locally:

- **Cache location**: `~/.nugetlicenses/cache.json` (Linux/macOS) or `%USERPROFILE%\.nugetlicenses\cache.json` (Windows)
- **Cache duration**: Configurable via `CacheDurationDays` (default: 365 days)
- **Cache behavior**: 
  - First run: Fetches all license info from NuGet API and caches it
  - Subsequent runs: Uses cached data for packages, only fetching new/expired entries
  - Expired entries are automatically removed and refetched

The cache significantly reduces scan time for large projects. To disable caching, use the `--disable-cache` flag or set `EnableCache` to `false` in configuration.

```bash
# Disable cache via environment variable
export LICENSECHECK_LicenseCheck__EnableCache=false

# Disable cache via command line (simple flag, no value needed)
nuget-licenses --disable-cache

# Set custom cache duration (30 days)
nuget-licenses --cache-duration-days 30
```

To manually clear the cache, simply delete the cache file:

```bash
# Linux/macOS
rm ~/.nugetlicenses/cache.json

# Windows (PowerShell)
Remove-Item $env:USERPROFILE\.nugetlicenses\cache.json
```

## Directory Scanning

The tool discovers `.csproj` and `.fsproj` project files and extracts package dependencies for each project:

### Primary Approach: Package Lock Files

For each project discovered, the tool first looks for a `packages.lock.json` file:
- If found, parses dependencies from the lock file (fast and reliable)
- If missing, falls back to `dotnet list package` command (when lock file is expected but not found, a warning is logged)

This hybrid approach allows the tool to work with projects that have lock files enabled as well as those that don't.

### When No Project Files Are Found

If the tool doesn't find any `.csproj` or `.fsproj` files in the directory tree, it falls back to scanning for `packages.lock.json` files directly. This legacy fallback is useful for:
- Solutions without project files in the root
- Monorepos with a non-standard structure
- Scenarios where you only have lock files available

**Note:** When using legacy lock file scanning, per-project tracking is not available. The tool will still deduplicate packages and report violations, but without project-level context.

### Configuration

To enable lock files in your projects, add this to your `.csproj` or `.fsproj`:

```xml
<PropertyGroup>
  <RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>
</PropertyGroup>
```

To use a custom lock file location, add:

```xml
<PropertyGroup>
  <NuGetLockFilePath>./locks/packages.lock.json</NuGetLockFilePath>
</PropertyGroup>
```

### Behavior

The tool recursively scans the configured `ProjectPath`:
- Discovers all `.csproj` and `.fsproj` files
- For each project, extracts packages from lock files or `dotnet list package`
- Deduplicates packages across projects (by lowercase ID + version)
- Reports which projects reference each package
- Tracks lock file status per project (enabled, missing, or not applicable)

## Exempt Packages

Packages can be exempted from validation via the `ExemptPackages` configuration. Each entry has:

| Field | Description |
|---|---|
| `PackageName` | Package ID to exempt. Supports trailing wildcard (`*`) for prefix matching (e.g. `System.*` exempts all `System.` packages) |
| `Version` | Specific version to exempt, `*` for any version, or omit/null for any version |
| `Reason` | Optional explanation for the exemption |

```json
{
  "ExemptPackages": [
    { "PackageName": "System.*", "Reason": "Framework packages" },
    { "PackageName": "My.Internal.Lib", "Version": "2.0.0", "Reason": "Reviewed by legal" }
  ]
}
```

## Output Formats

The `OutputFormat` setting controls the report format:

- **`console`** (default) — tabular text output with a summary line
- **`markdown`** — structured markdown with separate Violations and Valid Packages tables
- **`json`** — machine-readable JSON with summary, violations, and (when `ShowAllPackages` is enabled) a full package list
- **`html`** — responsive HTML report with styled tables
  - Professional design with modern aesthetics
  - Blue color scheme with hover effects
  - Violations highlighted with yellow background
  - Tables with striped hover state for better readability
  - Responsive layout that works on desktop and mobile
  - Proper HTML encoding for security

### HTML Report Features

The HTML report includes:
- Clean, professional styling using modern CSS
- Summary card with violation count
- Separate sections for violations and valid packages
- Project information table (if available)
- Optimized for printing (can be printed to PDF)
- Self-contained (no external dependencies)

**Example usage:**
```bash
# Generate HTML report
nuget-licenses --output-format html > license-report.html

# Open in browser
open license-report.html

# Or save to specific location
nuget-licenses -o html --project-path /path > reports/licenses.html
```

## License File Heuristic Fallback

**This feature is disabled by default.** To enable it, set `EnableLicenseFileHeuristics` to `true` in configuration or use the `--enable-license-heuristics` command-line option.

When enabled and a NuGet package has no SPDX license expression in its metadata, the tool attempts to identify the license automatically from the license file URL before flagging it as a violation:

1. **URL pattern matching** — URLs like `https://licenses.nuget.org/MIT` are recognized and the SPDX identifier is extracted directly.
2. **Content fingerprinting** — The license URL is fetched and the text is matched against known license fingerprints (case-insensitive).

### Enabling License File Heuristics

Via configuration file:
```json
{
  "LicenseCheck": {
    "EnableLicenseFileHeuristics": true
  }
}
```

Via environment variable:
```bash
export LICENSECHECK_LicenseCheck__EnableLicenseFileHeuristics=true
```

Via command-line:
```bash
nuget-licenses --enable-license-heuristics
```

### Supported Licenses for Content Detection

| SPDX ID | Detection method |
|---|---|
| MIT | "permission is hereby granted, free of charge" |
| Apache-2.0 | "apache license" + "version 2.0" |
| BSD-2-Clause | BSD redistribution clause without "neither the name" |
| BSD-3-Clause | BSD redistribution clause with "neither the name" |
| ISC | "permission to use, copy, modify, and/or distribute" |
| Unlicense | "this is free and unencumbered software" |
| MS-PL | "microsoft public license" |
| MPL-2.0 | "mozilla public license" + "version 2.0" |
| LGPL-2.1 | "gnu lesser general public license" + "version 2.1" |
| GPL-2.0 | "gnu general public license" + "version 2" |
| GPL-3.0 | "gnu general public license" + "version 3" |

If the URL is unreachable (timeout, HTTP error, non-text content) or the text doesn't match any known license, the package is still reported as having no license expression.

## SPDX Expression Handling

The validator supports compound SPDX license expressions:

- **OR** — at least one branch must be a permitted license (`MIT OR GPL-3.0` passes if `MIT` is permitted)
- **AND** — all parts must be permitted licenses (`MIT AND Apache-2.0` requires both to be permitted)
- **Parentheses** — nested expressions like `(MIT AND Apache-2.0) OR GPL-3.0` are evaluated correctly
- **WITH** — exception clauses are stripped before matching (`Apache-2.0 WITH LLVM-exception` is checked as `Apache-2.0`)

## Project Structure

```
Horstmeier.NugetLicenses/
  src/
    Horstmeier.NugetLicenses/          Console application
      Configuration/                   Settings POCO
      Models/                          PackageReference, LicenseInfo, LicenseValidationResult
      Services/                        Parser, resolver, validator, license file analyzer with interfaces
  tests/
    Horstmeier.NugetLicenses.Tests/    xUnit tests
```

## Building and Testing

```bash
# Build
dotnet build

# Run unit tests
dotnet test --filter "Category!=Integration"

# Run all tests (including integration tests that call nuget.org)
dotnet test
```

## License

MIT
