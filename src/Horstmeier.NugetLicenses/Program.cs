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

var projectPathOption = new Option<string>(
    aliases: ["--project-path", "-p"],
    description: "Root directory to scan recursively for project files (csproj/fsproj) and packages.lock.json",
    getDefaultValue: () => ".");

var showAllPackagesOption = new Option<bool>(
    aliases: ["--show-all-packages", "-a"],
    description: "Include all packages in the report (not just violations)");

var outputFormatOption = new Option<string>(
    aliases: ["--output-format", "-o"],
    description: "Report format: console (default), markdown, html, or json",
    getDefaultValue: () => "console");

var quietOption = new Option<bool>(
    aliases: ["--quiet", "-q"],
    description: "Suppress info logging, only output the report");

var disableCacheOption = new Option<bool>(
    aliases: ["--disable-cache"],
    description: "Disable local license caching (caching is enabled by default)");

var cacheDurationOption = new Option<int?>(
    aliases: ["--cache-duration-days"],
    description: "Number of days to keep cached license information (default: 7)");

var nugetSourceOption = new Option<string?>(
    aliases: ["--nuget-source"],
    description: "NuGet v3 API source URL");

var enableLicenseHeuristicsOption = new Option<bool>(
    aliases: ["--enable-license-heuristics"],
    description: "Enable license file heuristics to identify unknown licenses from URLs (disabled by default)");

var configFileOption = new Option<string?>(
    aliases: ["--config-file"],
    description: "Path to a nuget-licenses.json config file. When specified, bypasses all auto-discovered " +
                 "config files (global and project-level). Only built-in defaults, this file, " +
                 "environment variables, and CLI options apply.");

rootCommand.AddOption(projectPathOption);
rootCommand.AddOption(showAllPackagesOption);
rootCommand.AddOption(outputFormatOption);
rootCommand.AddOption(quietOption);
rootCommand.AddOption(disableCacheOption);
rootCommand.AddOption(cacheDurationOption);
rootCommand.AddOption(nugetSourceOption);
rootCommand.AddOption(enableLicenseHeuristicsOption);
rootCommand.AddOption(configFileOption);

// Add validation for output format
outputFormatOption.AddValidator(result =>
{
    var value = result.GetValueOrDefault<string>();
    if (value != null && !new[] { "console", "markdown", "json", "html" }.Contains(value.ToLowerInvariant()))
    {
        result.ErrorMessage = "Output format must be one of: console, markdown, json, html";
    }
});

CommandLineOptions? cliOptions = null;
rootCommand.SetHandler(ctx =>
{
    var pr = ctx.ParseResult;
    var projectPath = pr.GetValueForOption(projectPathOption)!;
    var format = pr.GetValueForOption(outputFormatOption)!;
    cliOptions = new CommandLineOptions
    {
        ProjectPath = projectPath != "." ? projectPath : null,
        ShowAllPackages = pr.GetValueForOption(showAllPackagesOption) ? true : null,
        OutputFormat = format != "console" ? format : null,
        Quiet = pr.GetValueForOption(quietOption) ? true : null,
        DisableCache = pr.GetValueForOption(disableCacheOption) ? true : null,
        CacheDurationDays = pr.GetValueForOption(cacheDurationOption),
        NuGetSource = pr.GetValueForOption(nugetSourceOption),
        EnableLicenseFileHeuristics = pr.GetValueForOption(enableLicenseHeuristicsOption),
        ConfigFile = pr.GetValueForOption(configFileOption)
    };
});

var parseResult = await rootCommand.InvokeAsync(args);

// If parsing failed or help was shown, exit early
if (parseResult != 0 || cliOptions == null)
    return parseResult;

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

var services = serviceCollection.BuildServiceProvider();

var parser = services.GetRequiredService<IPackageLockParser>();
var resolver = services.GetRequiredService<ILicenseResolver>();
var validator = services.GetRequiredService<ILicenseValidator>();
var reportGenerator = services.GetRequiredService<IReportGenerator>();

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

var licenses = await resolver.ResolveAsync(scanResult.Packages);

if (!quiet)
    Console.Error.WriteLine("Resolving licenses...");

var validationResult = validator.Validate(licenses);

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

    if (isViolation || settings.ShowAllPackages)
    {
        entries.Add(new PackageReportEntry(
            license.PackageId,
            license.Version,
            licenseDisplay,
            isViolation,
            violation?.Reason,
            isViolation ? projects ?? [] : []));
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
    
    return cli.Quiet ?? false;
}
