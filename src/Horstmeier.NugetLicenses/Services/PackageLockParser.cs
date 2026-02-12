using System.Text.Json;
using Horstmeier.NugetLicenses.Models;
using Microsoft.Extensions.Logging;

namespace Horstmeier.NugetLicenses.Services;

public class PackageLockParser : IPackageLockParser
{
    private readonly ILogger<PackageLockParser> _logger;

    public PackageLockParser(ILogger<PackageLockParser> logger)
    {
        _logger = logger;
    }

    public IReadOnlyList<PackageReference> Parse(string lockFilePath)
    {
        if (!File.Exists(lockFilePath))
            throw new FileNotFoundException($"Lock file not found: {lockFilePath}", lockFilePath);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var packages = new List<PackageReference>();
        ParseFile(lockFilePath, seen, packages, null);

        _logger.LogInformation("Parsed {Count} unique packages from {Path}", packages.Count, lockFilePath);
        return packages;
    }

    public DirectoryScanResult ParseDirectory(string rootPath)
    {
        var lockFiles = Directory.EnumerateFiles(rootPath, "packages.lock.json", SearchOption.AllDirectories).ToList();

        if (lockFiles.Count == 0)
        {
            _logger.LogWarning("No packages.lock.json files found under {RootPath}", rootPath);
            return new DirectoryScanResult([], new Dictionary<string, IReadOnlyList<string>>(), 0);
        }

        _logger.LogInformation("Found {Count} lock file(s) under {RootPath}", lockFiles.Count, rootPath);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var packages = new List<PackageReference>();
        var projectMap = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var lockFile in lockFiles)
        {
            _logger.LogInformation("Parsing {Path}", lockFile);
            ParseFile(lockFile, seen, packages, projectMap, rootPath);
        }

        _logger.LogInformation("Parsed {Count} unique packages across {FileCount} lock file(s)", packages.Count, lockFiles.Count);

        var readOnlyMap = projectMap.ToDictionary(
            kvp => kvp.Key,
            kvp => (IReadOnlyList<string>)kvp.Value,
            StringComparer.OrdinalIgnoreCase);

        return new DirectoryScanResult(packages, readOnlyMap, lockFiles.Count);
    }

    private void ParseFile(string lockFilePath, HashSet<string> seen, List<PackageReference> packages,
        Dictionary<string, List<string>>? projectMap, string? rootPath = null)
    {
        var projectName = rootPath is not null
            ? Path.GetRelativePath(rootPath, Path.GetDirectoryName(lockFilePath)!)
            : null;

        var json = File.ReadAllText(lockFilePath);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (!root.TryGetProperty("dependencies", out var dependencies))
            return;

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
                {
                    packages.Add(new PackageReference(id, version, type, framework.Name));
                }

                if (projectMap is not null && projectName is not null)
                {
                    if (!projectMap.TryGetValue(key, out var projects))
                    {
                        projects = [];
                        projectMap[key] = projects;
                    }

                    if (!projects.Contains(projectName, StringComparer.OrdinalIgnoreCase))
                        projects.Add(projectName);
                }
            }
        }
    }
}
