using System.Text.Json;
using Horstmeier.NugetLicenses.Configuration;
using Horstmeier.NugetLicenses.Models;
using Horstmeier.NugetLicenses.Services;

namespace Horstmeier.NugetLicenses.Tests;

public class ReportGeneratorTests
{
    private static LicenseValidationResult MakeResult(int total, int valid, List<LicenseViolation>? violations = null)
    {
        return new LicenseValidationResult(violations ?? [], total, valid);
    }

    private static List<PackageReportEntry> MakeMixedEntries()
    {
        return
        [
            new PackageReportEntry("BadPackage", "1.0.0", "GPL-3.0", true,
                "License 'GPL-3.0' is not permitted", ["src/ProjectA", "src/ProjectB"]),
            new PackageReportEntry("UnknownPkg", "2.0.0", "(unknown)", true,
                "No SPDX license expression found", ["src/ProjectA"]),
            new PackageReportEntry("GoodPackage", "3.0.0", "MIT", false, null, [])
        ];
    }

    [Fact]
    public void Generate_ConsoleFormat_ViolationsOnly()
    {
        var settings = new LicenseCheckSettings { OutputFormat = "console" };
        var generator = new ReportGenerator(settings);

        var violations = MakeMixedEntries().Where(e => e.IsViolation).ToList();
        var result = MakeResult(10, 8, [
            new LicenseViolation("BadPackage", "1.0.0", "GPL-3.0", null, "License 'GPL-3.0' is not permitted"),
            new LicenseViolation("UnknownPkg", "2.0.0", null, null, "No SPDX license expression found")
        ]);

        var output = generator.Generate(violations, result);

        Assert.Contains("BadPackage", output);
        Assert.Contains("GPL-3.0", output);
        Assert.Contains("Reason: License 'GPL-3.0' is not permitted", output);
        Assert.Contains("Projects: src/ProjectA, src/ProjectB", output);
        Assert.Contains("UnknownPkg", output);
        Assert.Contains("Summary: 8/10 packages valid, 2 violation(s)", output);
        Assert.DoesNotContain("GoodPackage", output);
    }

    [Fact]
    public void Generate_ConsoleFormat_ShowAll()
    {
        var settings = new LicenseCheckSettings { OutputFormat = "console", ShowAllPackages = true };
        var generator = new ReportGenerator(settings);

        var entries = MakeMixedEntries();
        var result = MakeResult(3, 1, [
            new LicenseViolation("BadPackage", "1.0.0", "GPL-3.0", null, "Not permitted"),
            new LicenseViolation("UnknownPkg", "2.0.0", null, null, "No SPDX license expression found")
        ]);

        var output = generator.Generate(entries, result);

        Assert.Contains("BadPackage", output);
        Assert.Contains("GoodPackage", output);
        Assert.Contains("MIT", output);
        Assert.Contains("Summary: 1/3 packages valid, 2 violation(s)", output);
    }

    [Fact]
    public void Generate_MarkdownFormat_ProducesValidMarkdown()
    {
        var settings = new LicenseCheckSettings { OutputFormat = "markdown", ShowAllPackages = true };
        var generator = new ReportGenerator(settings);

        var entries = MakeMixedEntries();
        var result = MakeResult(3, 1, [
            new LicenseViolation("BadPackage", "1.0.0", "GPL-3.0", null, "Not permitted"),
            new LicenseViolation("UnknownPkg", "2.0.0", null, null, "No SPDX")
        ]);

        var output = generator.Generate(entries, result);

        Assert.Contains("# License Report", output);
        Assert.Contains("## Violations", output);
        Assert.Contains("| BadPackage |", output);
        Assert.Contains("| Package | Version | License | Reason | Projects |", output);
        Assert.Contains("## Valid Packages", output);
        Assert.Contains("| GoodPackage |", output);
        Assert.Contains("**Summary:**", output);
    }

    [Fact]
    public void Generate_JsonFormat_ProducesValidJson()
    {
        var settings = new LicenseCheckSettings { OutputFormat = "json", ShowAllPackages = true };
        var generator = new ReportGenerator(settings);

        var entries = MakeMixedEntries();
        var result = MakeResult(3, 1, [
            new LicenseViolation("BadPackage", "1.0.0", "GPL-3.0", null, "Not permitted"),
            new LicenseViolation("UnknownPkg", "2.0.0", null, null, "No SPDX")
        ]);

        var output = generator.Generate(entries, result);

        var doc = JsonDocument.Parse(output);
        var root = doc.RootElement;

        Assert.Equal(3, root.GetProperty("summary").GetProperty("total").GetInt32());
        Assert.Equal(1, root.GetProperty("summary").GetProperty("valid").GetInt32());
        Assert.Equal(2, root.GetProperty("summary").GetProperty("violations").GetInt32());

        Assert.Equal(2, root.GetProperty("violations").GetArrayLength());
        Assert.Equal(3, root.GetProperty("packages").GetArrayLength());
    }

    [Fact]
    public void Generate_JsonFormat_OmitsPackagesWhenShowAllIsFalse()
    {
        var settings = new LicenseCheckSettings { OutputFormat = "json", ShowAllPackages = false };
        var generator = new ReportGenerator(settings);

        var violations = MakeMixedEntries().Where(e => e.IsViolation).ToList();
        var result = MakeResult(10, 8, [
            new LicenseViolation("BadPackage", "1.0.0", "GPL-3.0", null, "Not permitted"),
            new LicenseViolation("UnknownPkg", "2.0.0", null, null, "No SPDX")
        ]);

        var output = generator.Generate(violations, result);

        var doc = JsonDocument.Parse(output);
        var root = doc.RootElement;

        Assert.False(root.TryGetProperty("packages", out _));
        Assert.Equal(2, root.GetProperty("violations").GetArrayLength());
    }

    [Fact]
    public void Generate_ConsoleFormat_NoViolations_ShowsSummaryOnly()
    {
        var settings = new LicenseCheckSettings { OutputFormat = "console" };
        var generator = new ReportGenerator(settings);

        List<PackageReportEntry> entries = [];
        var result = MakeResult(5, 5);

        var output = generator.Generate(entries, result);

        Assert.Contains("Summary: 5/5 packages valid, 0 violation(s)", output);
        Assert.DoesNotContain("Reason:", output);
    }
}
