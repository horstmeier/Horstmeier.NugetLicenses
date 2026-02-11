# Horstmeier.NugetLicenses

A .NET console application that reads a project's `packages.lock.json`, resolves NuGet package licenses via the NuGet v3 API, and validates them against a configurable list of permitted SPDX licenses. Returns exit code 1 if any non-exempt package has a disallowed or missing license.

## Requirements

- .NET 10.0 SDK
- A project with `packages.lock.json` (enable via `<RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>` in your `.csproj`)

## Usage

```bash
# Run against the current directory
dotnet run --project src/Horstmeier.NugetLicenses

# Run against a specific project path
dotnet run --project src/Horstmeier.NugetLicenses -- --ProjectPath=/path/to/your/project

# Dump all packages and their licenses to the console
dotnet run --project src/Horstmeier.NugetLicenses -- --DumpPackages
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
    "ExemptPackages": [],
    "ProjectPath": "."
  }
}
```

| Setting | Description |
|---|---|
| `PermittedLicenses` | SPDX license identifiers that are allowed |
| `ExemptPackages` | Package IDs to skip validation entirely (case-insensitive) |
| `ProjectPath` | Directory containing `packages.lock.json` |
| `DumpPackages` | Print a table of all packages with their resolved licenses to the console |

### Environment variables

Use the `LICENSECHECK_` prefix with section separators as `__`:

```bash
export LICENSECHECK_LicenseCheck__ProjectPath=/path/to/project
```

### Command-line arguments

```bash
dotnet run -- --ProjectPath=/path/to/project
dotnet run -- --DumpPackages
```

## SPDX Expression Handling

The validator supports compound SPDX license expressions:

- **OR** — at least one branch must be a permitted license (`MIT OR GPL-3.0` passes if `MIT` is permitted)
- **AND** — all parts must be permitted licenses (`MIT AND Apache-2.0` requires both to be permitted)
- **Parentheses** — nested expressions like `(MIT AND Apache-2.0) OR GPL-3.0` are evaluated correctly

## Project Structure

```
Horstmeier.NugetLicenses/
  src/
    Horstmeier.NugetLicenses/          Console application
      Configuration/                   Settings POCO
      Models/                          PackageReference, LicenseInfo, LicenseValidationResult
      Services/                        Parser, resolver, validator with interfaces
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
