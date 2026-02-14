using System.Text.Json;
using Horstmeier.NugetLicenses.Models;
using Microsoft.Extensions.Logging;

namespace Horstmeier.NugetLicenses.Services;

public class PackageLockParser : IPackageLockParser
{
    private readonly ILogger<PackageLockParser> _logger;
    private readonly IProjectFileParser _projectFileParser;

    public PackageLockParser(ILogger<PackageLockParser> logger, IProjectFileParser projectFileParser)
    {
        _logger = logger;
        _projectFileParser = projectFileParser;
    }

    public IReadOnlyList<PackageReference> Parse(string lockFilePath)
    {
        if (!File.Exists(lockFilePath))
            throw new FileNotFoundException($"Lock file not found: {lockFilePath}", lockFilePath);

        var result = ReadLockFilePackagesRaw(lockFilePath);
        _logger.LogInformation("Parsed {Count} unique packages from {Path}", result.Count, lockFilePath);
        return result;
    }

    public async Task<DirectoryScanResult> ParseDirectoryAsync(string rootPath, CancellationToken ct = default)
    {
        var projectFiles = Directory.EnumerateFiles(rootPath, "*.csproj", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(rootPath, "*.fsproj", SearchOption.AllDirectories))
            .OrderBy(f => f)
            .ToList();

        if (projectFiles.Count == 0)
        {
            _logger.LogInformation("No project files found, falling back to packages.lock.json scan under {RootPath}", rootPath);
            return ScanLockFilesLegacy(rootPath);
        }

        _logger.LogInformation("Found {Count} project file(s) under {RootPath}", projectFiles.Count, rootPath);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var packages = new List<PackageReference>();
        var projectMap = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var projectInfos = new List<ProjectInfo>();
        var lockFileCount = 0;

        foreach (var projectFile in projectFiles)
        {
            var projectDir = Path.GetDirectoryName(projectFile)!;
            var projectDirRelative = Path.GetRelativePath(rootPath, projectDir);
            var projectName = projectDirRelative == "."
                ? Path.GetFileNameWithoutExtension(projectFile)
                : projectDirRelative;

            var lockFileEnabled = _projectFileParser.IsPackageLockEnabled(projectFile);
            var lockFilePath = FindLockFile(projectFile, projectDir);

            IReadOnlyList<PackageReference> projectPackages;
            if (lockFilePath is not null)
            {
                _logger.LogInformation("Parsing lock file for {Project}: {Path}", projectName, lockFilePath);
                projectPackages = ReadLockFilePackagesRaw(lockFilePath);
                lockFileCount++;
            }
            else
            {
                if (lockFileEnabled)
                    _logger.LogWarning("Lock file is enabled but missing for project: {Project}", projectName);
                else
                    _logger.LogInformation("No lock file for {Project}, using dotnet list package", projectName);

                projectPackages = await _projectFileParser.GetPackagesAsync(projectFile, ct);
            }

            foreach (var pkg in projectPackages)
            {
                var key = $"{pkg.Id.ToLowerInvariant()}|{pkg.Version.ToLowerInvariant()}";
                if (seen.Add(key))
                    packages.Add(pkg);

                if (!projectMap.TryGetValue(key, out var projs))
                {
                    projs = [];
                    projectMap[key] = projs;
                }
                if (!projs.Contains(projectName, StringComparer.OrdinalIgnoreCase))
                    projs.Add(projectName);
            }

            projectInfos.Add(new ProjectInfo(
                Path.GetRelativePath(rootPath, projectFile),
                projectName,
                projectPackages.Count,
                lockFileEnabled,
                lockFilePath is not null));
        }

        _logger.LogInformation("Parsed {Count} unique packages across {FileCount} project file(s)",
            packages.Count, projectFiles.Count);

        var readOnlyMap = projectMap.ToDictionary(
            kvp => kvp.Key,
            kvp => (IReadOnlyList<string>)kvp.Value,
            StringComparer.OrdinalIgnoreCase);

        return new DirectoryScanResult(packages, readOnlyMap, lockFileCount, projectInfos);
    }

    private string? FindLockFile(string projectFilePath, string projectDir)
    {
        var customPath = _projectFileParser.GetCustomLockFilePath(projectFilePath);
        if (customPath is not null)
        {
            var fullPath = Path.IsPathRooted(customPath)
                ? customPath
                : Path.Combine(projectDir, customPath);
            return File.Exists(fullPath) ? fullPath : null;
        }

        var defaultPath = Path.Combine(projectDir, "packages.lock.json");
        return File.Exists(defaultPath) ? defaultPath : null;
    }

    private DirectoryScanResult ScanLockFilesLegacy(string rootPath)
    {
        var lockFiles = Directory.EnumerateFiles(rootPath, "packages.lock.json", SearchOption.AllDirectories).ToList();

        if (lockFiles.Count == 0)
        {
            _logger.LogWarning("No packages.lock.json files found under {RootPath}", rootPath);
            return new DirectoryScanResult([], new Dictionary<string, IReadOnlyList<string>>(), 0, []);
        }

        _logger.LogInformation("Found {Count} lock file(s) under {RootPath}", lockFiles.Count, rootPath);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var packages = new List<PackageReference>();
        var projectMap = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var lockFile in lockFiles)
        {
            _logger.LogInformation("Parsing {Path}", lockFile);
            var projectName = Path.GetRelativePath(rootPath, Path.GetDirectoryName(lockFile)!);
            var rawPackages = ReadLockFilePackagesRaw(lockFile);

            foreach (var pkg in rawPackages)
            {
                var key = $"{pkg.Id.ToLowerInvariant()}|{pkg.Version.ToLowerInvariant()}";
                if (seen.Add(key))
                    packages.Add(pkg);

                if (!projectMap.TryGetValue(key, out var projs))
                {
                    projs = [];
                    projectMap[key] = projs;
                }
                if (!projs.Contains(projectName, StringComparer.OrdinalIgnoreCase))
                    projs.Add(projectName);
            }
        }

        _logger.LogInformation("Parsed {Count} unique packages across {FileCount} lock file(s)", packages.Count, lockFiles.Count);

        var readOnlyMap = projectMap.ToDictionary(
            kvp => kvp.Key,
            kvp => (IReadOnlyList<string>)kvp.Value,
            StringComparer.OrdinalIgnoreCase);

        return new DirectoryScanResult(packages, readOnlyMap, lockFiles.Count, []);
    }

    private static IReadOnlyList<PackageReference> ReadLockFilePackagesRaw(string lockFilePath)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var packages = new List<PackageReference>();

        var json = File.ReadAllText(lockFilePath);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (!root.TryGetProperty("dependencies", out var dependencies))
            return packages;

        foreach (var framework in dependencies.EnumerateObject())
        {
            foreach (var package in framework.Value.EnumerateObject())
            {
                var id = package.Name;
                var version = package.Value.TryGetProperty("resolved", out var resolvedProp)
                    ? resolvedProp.GetString() ?? ""
                    : "";
                var type = package.Value.TryGetProperty("type", out var typeProp)
                    ? typeProp.GetString() ?? "Transitive"
                    : "Transitive";

                var key = $"{id.ToLowerInvariant()}|{version.ToLowerInvariant()}";
                if (seen.Add(key))
                    packages.Add(new PackageReference(id, version, type, framework.Name));
            }
        }

        return packages;
    }
}
