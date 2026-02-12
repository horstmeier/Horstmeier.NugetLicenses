using Horstmeier.NugetLicenses.Configuration;
using Horstmeier.NugetLicenses.Models;
using Horstmeier.NugetLicenses.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// Normalize bare boolean flags (e.g. --ShowAllPackages) to --Flag=true
// so the CommandLine provider doesn't consume the next argument as its value
string[] booleanFlags = ["--ShowAllPackages", "--Quiet"];
var normalizedArgs = args.Select(a =>
    booleanFlags.Any(f => f.Equals(a, StringComparison.OrdinalIgnoreCase))
        ? $"{a}=true"
        : a).ToArray();

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables(prefix: "LICENSECHECK_")
    .AddCommandLine(normalizedArgs)
    .Build();

var settings = new LicenseCheckSettings();
configuration.GetSection("LicenseCheck").Bind(settings);

// Allow CLI overrides
var cliPath = configuration["ProjectPath"];
if (!string.IsNullOrWhiteSpace(cliPath))
    settings.ProjectPath = cliPath;

if (configuration["ShowAllPackages"] is { } showAll)
    settings.ShowAllPackages = bool.Parse(showAll);

if (configuration["OutputFormat"] is { } fmt)
    settings.OutputFormat = fmt;

var quiet = false;
if (configuration["Quiet"] is { } q)
    quiet = bool.Parse(q);

// Configuration validation
if (settings.PermittedLicenses.Length == 0)
    Console.Error.WriteLine("Warning: No permitted licenses configured. All packages will be flagged as violations.");

if (!Directory.Exists(settings.ProjectPath))
    Console.Error.WriteLine($"Warning: Project path does not exist: {Path.GetFullPath(settings.ProjectPath)}");

var services = new ServiceCollection()
    .AddLogging(b => b.AddConsole().SetMinimumLevel(quiet ? LogLevel.Warning : LogLevel.Information))
    .AddHttpClient()
    .AddSingleton(settings)
    .AddSingleton<IPackageLockParser, PackageLockParser>()
    .AddSingleton<ILicenseFileAnalyzer, LicenseFileAnalyzer>()
    .AddSingleton<ILicenseResolver, NuGetLicenseResolver>()
    .AddSingleton<ILicenseValidator, LicenseValidator>()
    .AddSingleton<IReportGenerator, ReportGenerator>()
    .BuildServiceProvider();

var parser = services.GetRequiredService<IPackageLockParser>();
var resolver = services.GetRequiredService<ILicenseResolver>();
var validator = services.GetRequiredService<ILicenseValidator>();
var reportGenerator = services.GetRequiredService<IReportGenerator>();

if (!quiet)
    Console.Error.WriteLine($"Scanning for lock files under: {Path.GetFullPath(settings.ProjectPath)}");

var scanResult = parser.ParseDirectory(settings.ProjectPath);

if (!quiet)
    Console.Error.WriteLine($"Found {scanResult.Packages.Count} unique packages across {scanResult.LockFileCount} lock file(s)");

var licenses = await resolver.ResolveAsync(scanResult.Packages);

if (!quiet)
    Console.Error.WriteLine("Resolving licenses...");

var validationResult = validator.Validate(licenses);

// Build report entries
var licenseMap = licenses.ToDictionary(
    l => $"{l.PackageId.ToLowerInvariant()}|{l.Version.ToLowerInvariant()}",
    l => l,
    StringComparer.OrdinalIgnoreCase);

var violationMap = validationResult.Violations.ToDictionary(
    v => $"{v.PackageId.ToLowerInvariant()}|{v.Version.ToLowerInvariant()}",
    v => v,
    StringComparer.OrdinalIgnoreCase);

var entries = new List<PackageReportEntry>();
foreach (var license in licenses)
{
    var key = $"{license.PackageId.ToLowerInvariant()}|{license.Version.ToLowerInvariant()}";
    var isViolation = violationMap.TryGetValue(key, out var violation);
    var licenseDisplay = license.LicenseExpression ?? license.LicenseUrl ?? "(unknown)";

    scanResult.ProjectsByPackage.TryGetValue(key, out var projects);

    if (isViolation || settings.ShowAllPackages)
    {
        entries.Add(new PackageReportEntry(
            license.PackageId,
            license.Version,
            licenseDisplay,
            isViolation,
            violation?.Reason,
            isViolation ? (IReadOnlyList<string>)(projects ?? []) : []));
    }
}

var report = reportGenerator.Generate(entries, validationResult);
Console.Out.WriteLine(report);

return validationResult.HasViolations ? 1 : 0;
