using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Horstmeier.NugetLicenses.Configuration;
using Horstmeier.NugetLicenses.Models;

namespace Horstmeier.NugetLicenses.Services;

public class ReportGenerator : IReportGenerator
{
    private readonly LicenseCheckSettings _settings;

    public ReportGenerator(LicenseCheckSettings settings)
    {
        _settings = settings;
    }

    public string Generate(IReadOnlyList<PackageReportEntry> entries, LicenseValidationResult result)
    {
        return _settings.OutputFormat.ToLowerInvariant() switch
        {
            "markdown" => GenerateMarkdown(entries, result),
            "json" => GenerateJson(entries, result),
            _ => GenerateConsole(entries, result)
        };
    }

    private static string GenerateConsole(IReadOnlyList<PackageReportEntry> entries, LicenseValidationResult result)
    {
        var sb = new StringBuilder();

        if (entries.Count > 0)
        {
            sb.AppendLine($"{"Package",-50} {"Version",-15} {"License",-30}");
            sb.AppendLine(new string('-', 95));

            foreach (var entry in entries.OrderBy(e => e.PackageId))
            {
                sb.AppendLine($"{entry.PackageId,-50} {entry.Version,-15} {entry.License,-30}");
                if (entry.IsViolation)
                {
                    sb.AppendLine($"  Reason: {entry.Reason}");
                    if (entry.Projects.Count > 0)
                        sb.AppendLine($"  Projects: {string.Join(", ", entry.Projects)}");
                }
            }

            sb.AppendLine();
        }

        sb.Append($"Summary: {result.ValidPackages}/{result.TotalPackages} packages valid, {result.Violations.Count} violation(s)");
        return sb.ToString();
    }

    private static string GenerateMarkdown(IReadOnlyList<PackageReportEntry> entries, LicenseValidationResult result)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# License Report");

        var violations = entries.Where(e => e.IsViolation).OrderBy(e => e.PackageId).ToList();
        var valid = entries.Where(e => !e.IsViolation).OrderBy(e => e.PackageId).ToList();

        if (violations.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Violations");
            sb.AppendLine();
            sb.AppendLine("| Package | Version | License | Reason | Projects |");
            sb.AppendLine("|---------|---------|---------|--------|----------|");
            foreach (var entry in violations)
            {
                var projects = string.Join(", ", entry.Projects);
                sb.AppendLine($"| {entry.PackageId} | {entry.Version} | {entry.License} | {entry.Reason} | {projects} |");
            }
        }

        if (valid.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Valid Packages");
            sb.AppendLine();
            sb.AppendLine("| Package | Version | License |");
            sb.AppendLine("|---------|---------|---------|");
            foreach (var entry in valid)
            {
                sb.AppendLine($"| {entry.PackageId} | {entry.Version} | {entry.License} |");
            }
        }

        sb.AppendLine();
        sb.Append($"**Summary:** {result.ValidPackages}/{result.TotalPackages} packages valid, {result.Violations.Count} violation(s)");
        return sb.ToString();
    }

    private string GenerateJson(IReadOnlyList<PackageReportEntry> entries, LicenseValidationResult result)
    {
        var violations = entries.Where(e => e.IsViolation).ToList();

        var obj = new Dictionary<string, object>
        {
            ["summary"] = new
            {
                total = result.TotalPackages,
                valid = result.ValidPackages,
                violations = result.Violations.Count
            },
            ["violations"] = violations.Select(e => new
            {
                package = e.PackageId,
                version = e.Version,
                license = e.License,
                reason = e.Reason,
                projects = e.Projects
            }).ToList()
        };

        if (_settings.ShowAllPackages)
        {
            obj["packages"] = entries.Select(e => new
            {
                package = e.PackageId,
                version = e.Version,
                license = e.License,
                isViolation = e.IsViolation,
                reason = e.Reason,
                projects = e.Projects
            }).ToList();
        }

        return JsonSerializer.Serialize(obj, new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });
    }
}
