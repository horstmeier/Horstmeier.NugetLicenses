using System.CommandLine;
using Horstmeier.NugetLicenses.Configuration;
using Horstmeier.NugetLicenses.Logging;
using Horstmeier.NugetLicenses.Models;
using Horstmeier.NugetLicenses.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// Define command-line options using System.CommandLine
var rootCommand = new RootCommand("Check NuGet package licenses against a permitted list");

var projectPathOption = new Option<string>("--project-path", "-p")
{
    Description = "Root directory to scan recursively for project files (csproj/fsproj) and packages.lock.json",
    
    DefaultValueFactory = _ =>  "."
};

var showAllPackagesOption = new Option<bool>("--show-all-packages", "-a")
{
    Description = "Include all packages in the report (not just violations)"
};

var outputFormatOption = new Option<string>("--output-format", "-o")
{
    Description = "Report format: console (default), markdown, html, or json",
    DefaultValueFactory = _ => "console"
};

var quietOption = new Option<bool>("--quiet", "-q")
{
    Description = "Suppress info logging, only output the report"
};

var disableCacheOption = new Option<bool>("--disable-cache")
{
    Description = "Disable local license caching (caching is enabled by default)"
};

var cacheDurationOption = new Option<int?>("--cache-duration-days")
{
    Description = "Number of days to keep cached license information (default: 7)"
};

var nugetSourceOption = new Option<string?>("--nuget-source")
{
    Description = "NuGet v3 API source URL"
};

var enableLicenseHeuristicsOption = new Option<bool>("--enable-license-heuristics")
{
    Description = "Enable license file heuristics to identify unknown licenses from URLs (disabled by default)"
};

var configFileOption = new Option<string?>("--config-file")
{
    Description = "Path to a nuget-licenses.json config file. When specified, bypasses all auto-discovered " +
                 "config files (global and project-level). Only built-in defaults, this file, " +
                 "environment variables, and CLI options apply."
};

var checkUpdatesOption = new Option<bool>("--check-updates")
{
    Description = "Check for newer stable versions of direct dependencies"
};

var checkUpdatesAllOption = new Option<bool>("--check-updates-all")
{
    Description = "Check for newer stable versions of all packages, including transitive dependencies"
};

var nugetApiKeyOption = new Option<string?>("--nuget-api-key")
{
    Description = "API key for authenticating against a private NuGet feed"
};

var cacheDirOption = new Option<string?>("--cache-dir")
{
    Description = "Directory for storing the license and version cache files (default: ~/.nugetlicenses)"
};

var requireLockFilesOption = new Option<bool>("--require-lock-files")
{
    Description = "Fail if any project has RestorePackagesWithLockFile enabled but the lock file is missing"
};

rootCommand.Options.Add(projectPathOption);
rootCommand.Options.Add(showAllPackagesOption);
rootCommand.Options.Add(outputFormatOption);
rootCommand.Options.Add(quietOption);
rootCommand.Options.Add(disableCacheOption);
rootCommand.Options.Add(cacheDurationOption);
rootCommand.Options.Add(nugetSourceOption);
rootCommand.Options.Add(enableLicenseHeuristicsOption);
rootCommand.Options.Add(configFileOption);
rootCommand.Options.Add(checkUpdatesOption);
rootCommand.Options.Add(checkUpdatesAllOption);
rootCommand.Options.Add(nugetApiKeyOption);
rootCommand.Options.Add(cacheDirOption);
rootCommand.Options.Add(requireLockFilesOption);

// Add validation for output format
outputFormatOption.Validators.Add(result =>
{
    var value = result.GetValueOrDefault<string>();
    if (!new[] { "console", "markdown", "json", "html" }.Contains(value.ToLowerInvariant()))
    {
        result.AddError($"Invalid output format: {value}. Output format must be one of: console, markdown, json, html");
    }
});

CommandLineOptions? cliOptions = null;

rootCommand.SetAction(async (pr, ct) =>
{
    
    var projectPath = pr.GetValue(projectPathOption)!;
    var format = pr.GetValue(outputFormatOption)!;
    cliOptions = new CommandLineOptions
    {
        ProjectPath = projectPath != "." ? projectPath : null,
        ShowAllPackages = pr.GetValue(showAllPackagesOption) ? true : null,
        OutputFormat = format != "console" ? format : null,
        Quiet = pr.GetValue(quietOption) ? true : null,
        DisableCache = pr.GetValue(disableCacheOption) ? true : null,
        CacheDurationDays = pr.GetValue(cacheDurationOption),
        NuGetSource = pr.GetValue(nugetSourceOption),
        EnableLicenseFileHeuristics = pr.GetValue(enableLicenseHeuristicsOption),
        ConfigFile = pr.GetValue(configFileOption),
        CheckUpdates = pr.GetValue(checkUpdatesOption) ? true : null,
        CheckUpdatesAll = pr.GetValue(checkUpdatesAllOption) ? true : null,
        NuGetApiKey = pr.GetValue(nugetApiKeyOption),
        CacheDir = pr.GetValue(cacheDirOption),
        RequireLockFiles = pr.GetValue(requireLockFilesOption) ? true : null
    };
});

var parseResult = rootCommand.Parse(args);
var retCode = await parseResult.InvokeAsync();
// If parsing failed or help was shown, exit early
if (retCode != 0 || cliOptions == null)
    return retCode;

// Resolve project path early — needed for config file walk-up discovery
var resolvedProjectPath = Path.GetFullPath(cliOptions.ProjectPath ?? ".");

// Layer 1: built-in defaults (LicenseCheckSettings property initializers)
var settings = new LicenseCheckSettings();

if (cliOptions.ConfigFile != null)
{
    // Explicit --config-file bypasses all auto-discovered config files
    if (!File.Exists(cliOptions.ConfigFile))
    {
        Console.Error.WriteLine($"Error: Config file not found: {cliOptions.ConfigFile}");
        return 2;
    }
    var explicitConfig = ConfigurationLoader.LoadConfigFile(cliOptions.ConfigFile);
    if (explicitConfig != null)
        ConfigurationLoader.Apply(settings, explicitConfig);
}
else
{
    // Layer 2: global user config (~/.config/nuget-licenses/config.json)
    var globalConfigPath = ConfigurationLoader.GetGlobalConfigPath();
    if (File.Exists(globalConfigPath))
    {
        var globalConfig = ConfigurationLoader.LoadConfigFile(globalConfigPath);
        if (globalConfig != null)
            ConfigurationLoader.Apply(settings, globalConfig);
    }

    // Layer 3: project config (nuget-licenses.json, walk up from project path)
    var projectConfigPath = ConfigurationLoader.FindProjectConfig(resolvedProjectPath);
    if (projectConfigPath != null)
    {
        var projectConfig = ConfigurationLoader.LoadConfigFile(projectConfigPath);
        if (projectConfig != null)
            ConfigurationLoader.Apply(settings, projectConfig);
    }
}

// Layer 4: environment variables (LICENSECHECK_ prefix, e.g. LICENSECHECK_NuGetSource)
var envConfig = new ConfigurationBuilder()
    .AddEnvironmentVariables(prefix: "LICENSECHECK_")
    .Build();
var envSettings = new NugetLicensesConfig();
envConfig.Bind(envSettings);
ConfigurationLoader.Apply(settings, envSettings);

// Layer 5: CLI options
var quiet = MergeSettings(settings, cliOptions);

// Configuration validation
if (settings.PermittedLicenses.Length == 0)
    Console.Error.WriteLine("Warning: No permitted licenses configured. All packages will be flagged as violations.");

if (!Directory.Exists(settings.ProjectPath))
    Console.Error.WriteLine($"Warning: Project path does not exist: {Path.GetFullPath(settings.ProjectPath)}");

var serviceCollection = new ServiceCollection()
    .AddLogging(b => b.AddStderrConsole().SetMinimumLevel(quiet ? LogLevel.Warning : LogLevel.Information))
    .AddHttpClient()
    .AddSingleton(settings)
    .AddSingleton<IProjectFileParser, ProjectFileParser>()
    .AddSingleton<IPackageLockParser, PackageLockParser>()
    .AddSingleton<ILicenseFileAnalyzer, LicenseFileAnalyzer>()
    .AddSingleton<ILicenseValidator, LicenseValidator>()
    .AddSingleton<IReportGenerator, ReportGenerator>();

// Register cache if enabled
if (settings.EnableCache)
    serviceCollection.AddSingleton<ILicenseCache, LicenseCache>();

serviceCollection.AddSingleton<ILicenseResolver, NuGetLicenseResolver>();
serviceCollection.AddSingleton<IVersionChecker, VersionChecker>();

var services = serviceCollection.BuildServiceProvider();

var parser = services.GetRequiredService<IPackageLockParser>();
var resolver = services.GetRequiredService<ILicenseResolver>();
var validator = services.GetRequiredService<ILicenseValidator>();
var reportGenerator = services.GetRequiredService<IReportGenerator>();
var versionChecker = services.GetRequiredService<IVersionChecker>();

if (!quiet)
    Console.Error.WriteLine($"Scanning for project files under: {Path.GetFullPath(settings.ProjectPath)}");

var scanResult = await parser.ParseDirectoryAsync(settings.ProjectPath);

if (!quiet)
{
    if (scanResult.Projects.Count > 0)
        Console.Error.WriteLine($"Found {scanResult.Projects.Count} project(s) with {scanResult.Packages.Count} unique packages");
    else
        Console.Error.WriteLine($"Found {scanResult.Packages.Count} unique packages across {scanResult.LockFileCount} lock file(s)");
}

if (settings.RequireLockFiles)
{
    var missing = scanResult.Projects
        .Where(p => p.LockFileEnabled && !p.HasLockFile)
        .ToList();
    if (missing.Count > 0)
    {
        foreach (var p in missing)
            Console.Error.WriteLine($"Error: Lock file missing for project: {p.ProjectName}");
        return 1;
    }
}

var licenses = await resolver.ResolveAsync(scanResult.Packages);

if (!quiet)
    Console.Error.WriteLine("Resolving licenses...");

var validationResult = validator.Validate(licenses);

// Check for outdated packages if requested
Dictionary<string, VersionCheckResult> versionCheckMap = [];
if (settings.CheckUpdates)
{
    if (!quiet)
        Console.Error.WriteLine("Checking for package updates...");
    var versionResults = await versionChecker.CheckAsync(scanResult.Packages, settings.CheckUpdatesAll);
    versionCheckMap = versionResults.ToDictionary(
        r => $"{r.PackageId.ToLowerInvariant()}|{r.CurrentVersion.ToLowerInvariant()}",
        StringComparer.OrdinalIgnoreCase);
}

// Build report entries
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
    versionCheckMap.TryGetValue(key, out var versionCheck);

    if (isViolation || settings.ShowAllPackages)
    {
        entries.Add(new PackageReportEntry(
            license.PackageId,
            license.Version,
            licenseDisplay,
            isViolation,
            violation?.Reason,
            projects ?? [],
            versionCheck?.LatestVersion,
            versionCheck?.IsOutdated ?? false));
    }
}

var report = reportGenerator.Generate(entries, validationResult, scanResult.Projects.Count > 0 ? scanResult.Projects : null);
Console.Out.WriteLine(report);

return validationResult.HasViolations ? 1 : 0;

static bool MergeSettings(LicenseCheckSettings settings, CommandLineOptions cli)
{
    if (cli.ProjectPath != null) settings.ProjectPath = cli.ProjectPath;
    if (cli.ShowAllPackages != null) settings.ShowAllPackages = cli.ShowAllPackages.Value;
    if (cli.OutputFormat != null) settings.OutputFormat = cli.OutputFormat;
    if (cli.DisableCache != null) settings.EnableCache = !cli.DisableCache.Value;
    if (cli.CacheDurationDays != null) settings.CacheDurationDays = cli.CacheDurationDays.Value;
    if (cli.NuGetSource != null) settings.NuGetSource = cli.NuGetSource;
    if (cli.EnableLicenseFileHeuristics != null) settings.EnableLicenseFileHeuristics = cli.EnableLicenseFileHeuristics.Value;
    if (cli.CheckUpdates != null) settings.CheckUpdates = cli.CheckUpdates.Value;
    if (cli.CheckUpdatesAll != null) settings.CheckUpdatesAll = cli.CheckUpdatesAll.Value;
    if (cli.NuGetApiKey != null) settings.NuGetApiKey = cli.NuGetApiKey;
    if (cli.CacheDir != null) settings.CacheDirectory = cli.CacheDir;
    if (cli.RequireLockFiles != null) settings.RequireLockFiles = cli.RequireLockFiles.Value;

    // --check-updates-all implies --check-updates
    if (settings.CheckUpdatesAll) settings.CheckUpdates = true;

    return cli.Quiet ?? false;
}
