using Horstmeier.NugetLicenses.Configuration;
using Horstmeier.NugetLicenses.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables(prefix: "LICENSECHECK_")
    .AddCommandLine(args)
    .Build();

var settings = new LicenseCheckSettings();
configuration.GetSection("LicenseCheck").Bind(settings);

// Allow CLI overrides
var cliPath = configuration["ProjectPath"];
if (!string.IsNullOrWhiteSpace(cliPath))
    settings.ProjectPath = cliPath;

if (configuration["DumpPackages"] is { } dump)
    settings.DumpPackages = string.IsNullOrEmpty(dump) || bool.Parse(dump);

// Configuration validation
if (settings.PermittedLicenses.Length == 0)
    Console.Error.WriteLine("Warning: No permitted licenses configured. All packages will be flagged as violations.");

if (!Directory.Exists(settings.ProjectPath))
    Console.Error.WriteLine($"Warning: Project path does not exist: {Path.GetFullPath(settings.ProjectPath)}");

var services = new ServiceCollection()
    .AddLogging(b => b.AddConsole())
    .AddSingleton(settings)
    .AddSingleton<IPackageLockParser, PackageLockParser>()
    .AddSingleton<ILicenseResolver, NuGetLicenseResolver>()
    .AddSingleton<ILicenseValidator, LicenseValidator>()
    .BuildServiceProvider();

var parser = services.GetRequiredService<IPackageLockParser>();
var resolver = services.GetRequiredService<ILicenseResolver>();
var validator = services.GetRequiredService<ILicenseValidator>();

var lockFilePath = Path.Combine(settings.ProjectPath, "packages.lock.json");
Console.WriteLine($"Checking licenses for: {Path.GetFullPath(lockFilePath)}");

IReadOnlyList<Horstmeier.NugetLicenses.Models.PackageReference> packages;
try
{
    packages = parser.Parse(lockFilePath);
}
catch (FileNotFoundException ex)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}

Console.WriteLine($"Found {packages.Count} unique packages");

var licenses = await resolver.ResolveAsync(packages);

if (settings.DumpPackages)
{
    Console.WriteLine();
    Console.WriteLine($"{"Package",-50} {"Version",-20} {"License",-30}");
    Console.WriteLine(new string('-', 100));
    foreach (var license in licenses.OrderBy(l => l.PackageId))
    {
        var expr = license.LicenseExpression ?? license.LicenseUrl ?? "(unknown)";
        Console.WriteLine($"{license.PackageId,-50} {license.Version,-20} {expr,-30}");
    }
    Console.WriteLine();
}

var result = validator.Validate(licenses);

Console.WriteLine($"Valid: {result.ValidPackages}/{result.TotalPackages}");

if (result.HasViolations)
{
    Console.Error.WriteLine($"\n{result.Violations.Count} license violation(s) found:");
    foreach (var violation in result.Violations)
    {
        Console.Error.WriteLine($"  - {violation.PackageId} {violation.Version}: {violation.Reason}");
        if (violation.LicenseUrl is not null)
            Console.Error.WriteLine($"    License URL: {violation.LicenseUrl}");
    }

    return 1;
}

Console.WriteLine("\nAll package licenses are permitted.");
return 0;
