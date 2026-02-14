using System.Diagnostics;
using System.Text.Json;
using System.Xml.Linq;
using Horstmeier.NugetLicenses.Models;
using Microsoft.Extensions.Logging;

namespace Horstmeier.NugetLicenses.Services;

public class ProjectFileParser : IProjectFileParser
{
    private readonly ILogger<ProjectFileParser> _logger;

    public ProjectFileParser(ILogger<ProjectFileParser> logger)
    {
        _logger = logger;
    }

    public bool IsPackageLockEnabled(string projectFilePath)
    {
        try
        {
            var doc = XDocument.Load(projectFilePath);
            var value = doc.Descendants("RestorePackagesWithLockFile")
                .Select(e => e.Value.Trim())
                .FirstOrDefault();
            return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read project file for lock-enabled check: {Path}", projectFilePath);
            return false;
        }
    }

    public string? GetCustomLockFilePath(string projectFilePath)
    {
        try
        {
            var doc = XDocument.Load(projectFilePath);
            return doc.Descendants("NuGetLockFilePath")
                .Select(e => e.Value.Trim())
                .FirstOrDefault(v => !string.IsNullOrEmpty(v));
        }
        catch
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<PackageReference>> GetPackagesAsync(string projectFilePath, CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        psi.ArgumentList.Add("list");
        psi.ArgumentList.Add(projectFilePath);
        psi.ArgumentList.Add("package");
        psi.ArgumentList.Add("--include-transitive");
        psi.ArgumentList.Add("--format");
        psi.ArgumentList.Add("json");

        using var process = new Process { StartInfo = psi };
        process.Start();

        var outputTask = process.StandardOutput.ReadToEndAsync(ct);
        var errorTask = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);

        var output = await outputTask;
        var error = await errorTask;

        if (process.ExitCode != 0)
        {
            _logger.LogWarning("dotnet list package failed for {Path} (exit code {Code}): {Error}",
                projectFilePath, process.ExitCode, error.Trim());
            return [];
        }

        var packages = ParseDotnetListOutput(output);
        _logger.LogInformation("dotnet list package found {Count} packages for {Path}",
            packages.Count, projectFilePath);
        return packages;
    }

    private static IReadOnlyList<PackageReference> ParseDotnetListOutput(string output)
    {
        var jsonStart = output.IndexOf('{');
        if (jsonStart < 0)
            return [];

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var packages = new List<PackageReference>();

        try
        {
            using var doc = JsonDocument.Parse(output[jsonStart..]);
            var root = doc.RootElement;

            if (!root.TryGetProperty("projects", out var projects))
                return [];

            foreach (var project in projects.EnumerateArray())
            {
                if (!project.TryGetProperty("frameworks", out var frameworks))
                    continue;

                foreach (var framework in frameworks.EnumerateArray())
                {
                    var tfm = framework.TryGetProperty("framework", out var fwProp)
                        ? fwProp.GetString() ?? ""
                        : "";

                    if (framework.TryGetProperty("topLevelPackages", out var topLevel))
                    {
                        foreach (var pkg in topLevel.EnumerateArray())
                        {
                            var id = pkg.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? "" : "";
                            var version = pkg.TryGetProperty("resolvedVersion", out var vProp) ? vProp.GetString() ?? "" : "";
                            var key = $"{id.ToLowerInvariant()}|{version.ToLowerInvariant()}";
                            if (seen.Add(key))
                                packages.Add(new PackageReference(id, version, "Direct", tfm));
                        }
                    }

                    if (framework.TryGetProperty("transitivePackages", out var transitive))
                    {
                        foreach (var pkg in transitive.EnumerateArray())
                        {
                            var id = pkg.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? "" : "";
                            var version = pkg.TryGetProperty("resolvedVersion", out var vProp) ? vProp.GetString() ?? "" : "";
                            var key = $"{id.ToLowerInvariant()}|{version.ToLowerInvariant()}";
                            if (seen.Add(key))
                                packages.Add(new PackageReference(id, version, "Transitive", tfm));
                        }
                    }
                }
            }
        }
        catch (JsonException)
        {
            // Output was not valid JSON – return empty
        }

        return packages;
    }
}
