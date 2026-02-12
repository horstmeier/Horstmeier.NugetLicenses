# Horstmeier.NugetLicenses

A .NET console application that recursively scans a directory for `packages.lock.json` files, resolves NuGet package licenses via the NuGet v3 API, and validates them against a configurable list of permitted SPDX licenses. Returns exit code 1 if any non-exempt package has a disallowed or missing license.

## Requirements

- .NET 10.0 SDK
- A project with `packages.lock.json` (enable via `<RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>` in your `.csproj`)

## Usage

```bash
# Run against the current directory
dotnet run --project src/Horstmeier.NugetLicenses

# Run against a specific project path (scans recursively for packages.lock.json files)
dotnet run --project src/Horstmeier.NugetLicenses -- --ProjectPath=/path/to/your/solution

# Show all packages (not just violations)
dotnet run --project src/Horstmeier.NugetLicenses -- --ShowAllPackages

# Output as markdown or JSON
dotnet run --project src/Horstmeier.NugetLicenses -- --OutputFormat=markdown
dotnet run --project src/Horstmeier.NugetLicenses -- --OutputFormat=json

# Quiet mode — suppress info logging, only output the report
dotnet run --project src/Horstmeier.NugetLicenses -- --Quiet
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
    "OutputFormat": "console"
  }
}
```

| Setting | Description |
|---|---|
| `PermittedLicenses` | SPDX license identifiers that are allowed |
| `ExemptPackages` | Packages to skip validation (see [Exempt Packages](#exempt-packages)) |
| `ProjectPath` | Root directory to scan recursively for `packages.lock.json` files |
| `ShowAllPackages` | When `true`, include all packages in the report (not just violations) |
| `OutputFormat` | Report format: `console` (default), `markdown`, or `json` |
| `NuGetSource` | NuGet v3 API source URL (default: `https://api.nuget.org/v3/index.json`) |

### Environment variables

Use the `LICENSECHECK_` prefix with section separators as `__`:

```bash
export LICENSECHECK_LicenseCheck__ProjectPath=/path/to/project
```

### Command-line arguments

```bash
dotnet run -- --ProjectPath=/path/to/project
dotnet run -- --ShowAllPackages
dotnet run -- --OutputFormat=json
```

## Directory Scanning

The tool recursively scans the configured `ProjectPath` for all `packages.lock.json` files. This means you can point it at a solution root and it will discover packages from all projects at once.

- Packages are deduplicated across lock files (by lowercase ID + version)
- Violations report which project(s) reference the offending package
- The summary shows how many lock files were found and how many unique packages were resolved

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

## License File Heuristic Fallback

When a NuGet package has no SPDX license expression in its metadata, the tool attempts to identify the license automatically before flagging it as a violation:

1. **URL pattern matching** — URLs like `https://licenses.nuget.org/MIT` are recognized and the SPDX identifier is extracted directly.
2. **Content fingerprinting** — The license URL is fetched and the text is matched against known license fingerprints (case-insensitive).

Supported licenses for content detection:

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
