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

        var json = File.ReadAllText(lockFilePath);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (!root.TryGetProperty("dependencies", out var dependencies))
            return [];

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var packages = new List<PackageReference>();

        foreach (var framework in dependencies.EnumerateObject())
        {
            var targetFramework = framework.Name;

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
                    packages.Add(new PackageReference(id, version, type, targetFramework));
                }
            }
        }

        _logger.LogInformation("Parsed {Count} unique packages from {Path}", packages.Count, lockFilePath);
        return packages;
    }
}
