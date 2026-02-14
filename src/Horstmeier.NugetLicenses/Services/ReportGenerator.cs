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

    public string Generate(IReadOnlyList<PackageReportEntry> entries, LicenseValidationResult result,
        IReadOnlyList<ProjectInfo>? projects = null)
    {
        return _settings.OutputFormat.ToLowerInvariant() switch
        {
            "markdown" => GenerateMarkdown(entries, result, projects),
            "json" => GenerateJson(entries, result, projects),
            "html" => GenerateHtml(entries, result, projects),
            _ => GenerateConsole(entries, result, projects)
        };
    }

    private static string GenerateConsole(IReadOnlyList<PackageReportEntry> entries, LicenseValidationResult result,
        IReadOnlyList<ProjectInfo>? projects)
    {
        var sb = new StringBuilder();
        var showUpdates = entries.Any(e => e.LatestVersion != null);

        if (entries.Count > 0)
        {
            if (showUpdates)
            {
                sb.AppendLine($"{"Package",-50} {"Version",-15} {"License",-30} {"Status",-6} {"Latest",-15}");
                sb.AppendLine(new string('-', 119));
            }
            else
            {
                sb.AppendLine($"{"Package",-50} {"Version",-15} {"License",-30} {"Status",-6}");
                sb.AppendLine(new string('-', 103));
            }

            foreach (var entry in entries.OrderBy(e => e.PackageId))
            {
                var status = entry.IsViolation ? "FAIL" : "OK";
                if (showUpdates)
                {
                    var latest = FormatLatestConsole(entry);
                    sb.AppendLine($"{entry.PackageId,-50} {entry.Version,-15} {entry.License,-30} {status,-6} {latest,-15}");
                }
                else
                {
                    sb.AppendLine($"{entry.PackageId,-50} {entry.Version,-15} {entry.License,-30} {status,-6}");
                }
                if (entry.IsViolation)
                    sb.AppendLine($"  Reason: {entry.Reason}");
                if (entry.Projects.Count > 0)
                    sb.AppendLine($"  Projects: {string.Join(", ", entry.Projects)}");
            }

            sb.AppendLine();
        }

        sb.AppendLine($"Summary: {result.ValidPackages}/{result.TotalPackages} packages valid, {result.Violations.Count} violation(s)");

        if (projects is { Count: > 0 })
        {
            sb.AppendLine();
            sb.AppendLine($"{"Project",-50} {"Packages",10}  {"Lock Enabled",13}  {"Lock File",10}");
            sb.AppendLine(new string('-', 90));
            foreach (var p in projects.OrderBy(p => p.ProjectName))
            {
                var lockEnabled = p.LockFileEnabled ? "Yes" : "No";
                var hasLock = p.HasLockFile ? "Yes" : "No";
                sb.AppendLine($"{p.ProjectName,-50} {p.PackageCount,10}  {lockEnabled,13}  {hasLock,10}");
            }
        }

        return sb.ToString().TrimEnd();
    }

    private static string FormatLatestConsole(PackageReportEntry entry)
    {
        if (entry.LatestVersion == null)
            return string.Empty;
        return entry.IsOutdated ? $"-> {entry.LatestVersion}" : "current";
    }

    private static string GenerateMarkdown(IReadOnlyList<PackageReportEntry> entries, LicenseValidationResult result,
        IReadOnlyList<ProjectInfo>? projects)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# License Report");

        var violations = entries.Where(e => e.IsViolation).OrderBy(e => e.PackageId).ToList();
        var valid = entries.Where(e => !e.IsViolation).OrderBy(e => e.PackageId).ToList();
        var showUpdates = entries.Any(e => e.LatestVersion != null);

        if (violations.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Violations");
            sb.AppendLine();
            sb.AppendLine("| Package | Version | License | Reason | Projects |");
            sb.AppendLine("|---------|---------|---------|--------|----------|");
            foreach (var entry in violations)
            {
                var projectList = string.Join(", ", entry.Projects);
                sb.AppendLine($"| {entry.PackageId} | {entry.Version} | {entry.License} | {entry.Reason} | {projectList} |");
            }
        }

        if (valid.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Valid Packages");
            sb.AppendLine();
            if (showUpdates)
            {
                sb.AppendLine("| Package | Version | Latest | License | Projects |");
                sb.AppendLine("|---------|---------|--------|---------|----------|");
                foreach (var entry in valid)
                {
                    var latest = FormatLatestMarkdown(entry);
                    var projectList = string.Join(", ", entry.Projects);
                    sb.AppendLine($"| {entry.PackageId} | {entry.Version} | {latest} | {entry.License} | {projectList} |");
                }
            }
            else
            {
                sb.AppendLine("| Package | Version | License | Projects |");
                sb.AppendLine("|---------|---------|---------|----------|");
                foreach (var entry in valid)
                {
                    var projectList = string.Join(", ", entry.Projects);
                    sb.AppendLine($"| {entry.PackageId} | {entry.Version} | {entry.License} | {projectList} |");
                }
            }
        }

        sb.AppendLine();
        sb.AppendLine($"**Summary:** {result.ValidPackages}/{result.TotalPackages} packages valid, {result.Violations.Count} violation(s)");

        if (projects is { Count: > 0 })
        {
            sb.AppendLine();
            sb.AppendLine("## Projects");
            sb.AppendLine();
            sb.AppendLine("| Project | Packages | Lock Enabled | Lock File |");
            sb.AppendLine("|---------|----------|--------------|-----------|");
            foreach (var p in projects.OrderBy(p => p.ProjectName))
            {
                var lockEnabled = p.LockFileEnabled ? "Yes" : "No";
                var hasLock = p.HasLockFile ? "Yes" : "No";
                sb.AppendLine($"| {p.ProjectName} | {p.PackageCount} | {lockEnabled} | {hasLock} |");
            }
        }

        return sb.ToString().TrimEnd();
    }

    private static string FormatLatestMarkdown(PackageReportEntry entry)
    {
        if (entry.LatestVersion == null)
            return string.Empty;
        return entry.IsOutdated ? $"→ {entry.LatestVersion}" : "✓";
    }

    private string GenerateJson(IReadOnlyList<PackageReportEntry> entries, LicenseValidationResult result,
        IReadOnlyList<ProjectInfo>? projects)
    {
        var violations = entries.Where(e => e.IsViolation).ToList();
        var showUpdates = entries.Any(e => e.LatestVersion != null);

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
                projects = e.Projects,
                latestVersion = showUpdates ? e.LatestVersion : null,
                isOutdated = showUpdates ? (bool?)e.IsOutdated : null
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
                projects = e.Projects,
                latestVersion = showUpdates ? e.LatestVersion : null,
                isOutdated = showUpdates ? (bool?)e.IsOutdated : null
            }).ToList();
        }

        if (projects is { Count: > 0 })
        {
            obj["projects"] = projects.OrderBy(p => p.ProjectName).Select(p => new
            {
                project = p.ProjectName,
                projectFile = p.ProjectFilePath,
                packages = p.PackageCount,
                lockFileEnabled = p.LockFileEnabled,
                hasLockFile = p.HasLockFile
            }).ToList();
        }

        return JsonSerializer.Serialize(obj, new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });
    }

    private static string GenerateHtml(IReadOnlyList<PackageReportEntry> entries, LicenseValidationResult result,
        IReadOnlyList<ProjectInfo>? projects)
    {
        var sb = new StringBuilder();
        var showUpdates = entries.Any(e => e.LatestVersion != null);

        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html>");
        sb.AppendLine("<head>");
        sb.AppendLine("<meta charset=\"utf-8\">");
        sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        sb.AppendLine("<title>License Report</title>");
        sb.AppendLine("<style>");
        sb.AppendLine("body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif; margin: 20px; background: #f5f5f5; color: #333; }");
        sb.AppendLine("h1 { color: #1a1a1a; border-bottom: 3px solid #0066cc; padding-bottom: 10px; }");
        sb.AppendLine("h2 { color: #0066cc; margin-top: 30px; margin-bottom: 15px; }");
        sb.AppendLine("table { border-collapse: collapse; width: 100%; margin-bottom: 20px; background: white; box-shadow: 0 1px 3px rgba(0,0,0,0.1); }");
        sb.AppendLine("th { background: #0066cc; color: white; padding: 12px; text-align: left; font-weight: 600; }");
        sb.AppendLine("td { padding: 12px; border-bottom: 1px solid #e0e0e0; }");
        sb.AppendLine("tr:last-child td { border-bottom: none; }");
        sb.AppendLine(".summary { background: white; padding: 15px; border-left: 4px solid #0066cc; margin: 20px 0; border-radius: 4px; font-size: 16px; }");
        sb.AppendLine(".summary strong { color: #0066cc; }");
        sb.AppendLine(".row-ok { background: #f0fff4; } .row-ok:hover { background: #d4edda; }");
        sb.AppendLine(".row-fail { background: #fff5f5; } .row-fail:hover { background: #f8d7da; }");
        sb.AppendLine(".badge { display: inline-block; padding: 2px 9px; border-radius: 12px; font-size: 0.8em; font-weight: 700; letter-spacing: 0.03em; }");
        sb.AppendLine(".badge-ok { background: #28a745; color: white; } .badge-fail { background: #dc3545; color: white; }");
        sb.AppendLine(".badge-current { background: #6c757d; color: white; } .badge-outdated { background: #fd7e14; color: white; }");
        sb.AppendLine("</style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("<h1>License Report</h1>");

        var violations = entries.Where(e => e.IsViolation).OrderBy(e => e.PackageId).ToList();
        var valid = entries.Where(e => !e.IsViolation).OrderBy(e => e.PackageId).ToList();

        if (violations.Count > 0 && valid.Count > 0)
        {
            sb.AppendLine("<h2>Packages</h2>");
            sb.AppendLine("<table>");
            if (showUpdates)
                sb.AppendLine("<thead><tr><th>Package</th><th>Version</th><th>Latest</th><th>License</th><th>Status</th><th>Reason</th><th>Projects</th></tr></thead>");
            else
                sb.AppendLine("<thead><tr><th>Package</th><th>Version</th><th>License</th><th>Status</th><th>Reason</th><th>Projects</th></tr></thead>");
            sb.AppendLine("<tbody>");
            foreach (var entry in entries.OrderBy(e => e.PackageId))
            {
                var rowClass = entry.IsViolation ? "row-fail" : "row-ok";
                var badge = entry.IsViolation
                    ? "<span class=\"badge badge-fail\">FAIL</span>"
                    : "<span class=\"badge badge-ok\">OK</span>";
                var projectList = string.Join(", ", entry.Projects);
                if (showUpdates)
                {
                    var latestCell = FormatLatestHtml(entry);
                    sb.AppendLine($"<tr class=\"{rowClass}\"><td>{HtmlEncode(entry.PackageId)}</td><td>{HtmlEncode(entry.Version)}</td><td>{latestCell}</td><td>{HtmlEncode(entry.License)}</td><td>{badge}</td><td>{HtmlEncode(entry.Reason)}</td><td>{HtmlEncode(projectList)}</td></tr>");
                }
                else
                {
                    sb.AppendLine($"<tr class=\"{rowClass}\"><td>{HtmlEncode(entry.PackageId)}</td><td>{HtmlEncode(entry.Version)}</td><td>{HtmlEncode(entry.License)}</td><td>{badge}</td><td>{HtmlEncode(entry.Reason)}</td><td>{HtmlEncode(projectList)}</td></tr>");
                }
            }
            sb.AppendLine("</tbody>");
            sb.AppendLine("</table>");
        }
        else if (violations.Count > 0)
        {
            sb.AppendLine("<h2>Violations</h2>");
            sb.AppendLine("<table>");
            sb.AppendLine("<thead><tr><th>Package</th><th>Version</th><th>License</th><th>Status</th><th>Reason</th><th>Projects</th></tr></thead>");
            sb.AppendLine("<tbody>");
            foreach (var entry in violations)
            {
                var projectList = string.Join(", ", entry.Projects);
                sb.AppendLine($"<tr class=\"row-fail\"><td>{HtmlEncode(entry.PackageId)}</td><td>{HtmlEncode(entry.Version)}</td><td>{HtmlEncode(entry.License)}</td><td><span class=\"badge badge-fail\">FAIL</span></td><td>{HtmlEncode(entry.Reason)}</td><td>{HtmlEncode(projectList)}</td></tr>");
            }
            sb.AppendLine("</tbody>");
            sb.AppendLine("</table>");
        }
        else if (valid.Count > 0)
        {
            sb.AppendLine("<h2>Valid Packages</h2>");
            sb.AppendLine("<table>");
            if (showUpdates)
                sb.AppendLine("<thead><tr><th>Package</th><th>Version</th><th>Latest</th><th>License</th><th>Status</th><th>Projects</th></tr></thead>");
            else
                sb.AppendLine("<thead><tr><th>Package</th><th>Version</th><th>License</th><th>Status</th><th>Projects</th></tr></thead>");
            sb.AppendLine("<tbody>");
            foreach (var entry in valid)
            {
                var projectList = HtmlEncode(string.Join(", ", entry.Projects));
                if (showUpdates)
                {
                    var latestCell = FormatLatestHtml(entry);
                    sb.AppendLine($"<tr class=\"row-ok\"><td>{HtmlEncode(entry.PackageId)}</td><td>{HtmlEncode(entry.Version)}</td><td>{latestCell}</td><td>{HtmlEncode(entry.License)}</td><td><span class=\"badge badge-ok\">OK</span></td><td>{projectList}</td></tr>");
                }
                else
                {
                    sb.AppendLine($"<tr class=\"row-ok\"><td>{HtmlEncode(entry.PackageId)}</td><td>{HtmlEncode(entry.Version)}</td><td>{HtmlEncode(entry.License)}</td><td><span class=\"badge badge-ok\">OK</span></td><td>{projectList}</td></tr>");
                }
            }
            sb.AppendLine("</tbody>");
            sb.AppendLine("</table>");
        }

        sb.AppendLine($"<div class=\"summary\"><strong>Summary:</strong> {result.ValidPackages}/{result.TotalPackages} packages valid, {result.Violations.Count} violation(s)</div>");

        if (projects is { Count: > 0 })
        {
            sb.AppendLine("<h2>Projects</h2>");
            sb.AppendLine("<table>");
            sb.AppendLine("<thead><tr><th>Project</th><th>Packages</th><th>Lock Enabled</th><th>Lock File</th></tr></thead>");
            sb.AppendLine("<tbody>");
            foreach (var p in projects.OrderBy(p => p.ProjectName))
            {
                var lockEnabled = p.LockFileEnabled ? "Yes" : "No";
                var hasLock = p.HasLockFile ? "Yes" : "No";
                sb.AppendLine($"<tr><td>{HtmlEncode(p.ProjectName)}</td><td>{p.PackageCount}</td><td>{lockEnabled}</td><td>{hasLock}</td></tr>");
            }
            sb.AppendLine("</tbody>");
            sb.AppendLine("</table>");
        }

        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString().TrimEnd();
    }

    private static string FormatLatestHtml(PackageReportEntry entry)
    {
        if (entry.LatestVersion == null)
            return string.Empty;
        return entry.IsOutdated
            ? $"<span class=\"badge badge-outdated\">\u2192 {HtmlEncode(entry.LatestVersion)}</span>"
            : "<span class=\"badge badge-current\">\u2713</span>";
    }

    private static string HtmlEncode(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return text ?? string.Empty;

        return System.Net.WebUtility.HtmlEncode(text);
    }
}
