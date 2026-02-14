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

    // -------------------------------------------------------------------------
    // HTML format
    // -------------------------------------------------------------------------

    [Fact]
    public void Generate_HtmlFormat_ContainsDocTypeAndTitle()
    {
        var settings = new LicenseCheckSettings { OutputFormat = "html" };
        var generator = new ReportGenerator(settings);

        var output = generator.Generate(MakeMixedEntries(), MakeResult(3, 1));

        Assert.Contains("<!DOCTYPE html>", output);
        Assert.Contains("<title>License Report</title>", output);
        Assert.Contains("<h1>License Report</h1>", output);
    }

    [Fact]
    public void Generate_HtmlFormat_OnlyViolations_ShowsViolationsTable()
    {
        var settings = new LicenseCheckSettings { OutputFormat = "html" };
        var generator = new ReportGenerator(settings);

        var violations = MakeMixedEntries().Where(e => e.IsViolation).ToList();
        var result = MakeResult(2, 0, [
            new LicenseViolation("BadPackage", "1.0.0", "GPL-3.0", null, "Not permitted"),
            new LicenseViolation("UnknownPkg", "2.0.0", null, null, "No SPDX")
        ]);

        var output = generator.Generate(violations, result);

        Assert.Contains("<h2>Violations</h2>", output);
        Assert.Contains("BadPackage", output);
        Assert.Contains("badge-fail", output);
        Assert.DoesNotContain("<h2>Valid Packages</h2>", output);
        Assert.DoesNotContain("<h2>Packages</h2>", output);
    }

    [Fact]
    public void Generate_HtmlFormat_OnlyValid_ShowsValidTable()
    {
        var settings = new LicenseCheckSettings { OutputFormat = "html" };
        var generator = new ReportGenerator(settings);

        var valid = new List<PackageReportEntry>
        {
            new("PkgA", "1.0.0", "MIT", false, null, []),
            new("PkgB", "2.0.0", "Apache-2.0", false, null, [])
        };
        var result = MakeResult(2, 2);

        var output = generator.Generate(valid, result);

        Assert.Contains("<h2>Valid Packages</h2>", output);
        Assert.Contains("PkgA", output);
        Assert.Contains("badge-ok", output);
        Assert.DoesNotContain("<h2>Violations</h2>", output);
        Assert.DoesNotContain("<h2>Packages</h2>", output);
    }

    [Fact]
    public void Generate_HtmlFormat_MixedEntries_ShowsCombinedTable()
    {
        var settings = new LicenseCheckSettings { OutputFormat = "html" };
        var generator = new ReportGenerator(settings);

        var output = generator.Generate(MakeMixedEntries(), MakeResult(3, 1));

        Assert.Contains("<h2>Packages</h2>", output);
        Assert.Contains("badge-ok", output);
        Assert.Contains("badge-fail", output);
        Assert.DoesNotContain("<h2>Violations</h2>", output);
        Assert.DoesNotContain("<h2>Valid Packages</h2>", output);
    }

    [Fact]
    public void Generate_HtmlFormat_ContainsSummaryDiv()
    {
        var settings = new LicenseCheckSettings { OutputFormat = "html" };
        var generator = new ReportGenerator(settings);

        var result = MakeResult(3, 1, [new LicenseViolation("X", "1.0.0", null, null, "Reason")]);
        var output = generator.Generate(MakeMixedEntries(), result);

        Assert.Contains("class=\"summary\"", output);
        Assert.Contains("1/3 packages valid, 1 violation(s)", output);
    }

    [Fact]
    public void Generate_HtmlFormat_WithProjects_ShowsProjectsTable()
    {
        var settings = new LicenseCheckSettings { OutputFormat = "html" };
        var generator = new ReportGenerator(settings);

        var projects = new List<ProjectInfo>
        {
            new("/path/MyApp.csproj", "MyApp", 5, true, true),
            new("/path/MyLib.csproj", "MyLib", 3, false, false)
        };

        var output = generator.Generate(MakeMixedEntries(), MakeResult(3, 1), projects);

        Assert.Contains("<h2>Projects</h2>", output);
        Assert.Contains("MyApp", output);
        Assert.Contains("MyLib", output);
        Assert.Contains(">Yes<", output);
        Assert.Contains(">No<", output);
    }

    [Fact]
    public void Generate_HtmlFormat_HtmlEncodesSpecialCharacters()
    {
        var settings = new LicenseCheckSettings { OutputFormat = "html" };
        var generator = new ReportGenerator(settings);

        var entries = new List<PackageReportEntry>
        {
            new("Pkg<&>", "1.0.0", "MIT & Apache", false, null, ["Project<X>"])
        };

        var output = generator.Generate(entries, MakeResult(1, 1));

        Assert.Contains("Pkg&lt;&amp;&gt;", output);
        Assert.Contains("MIT &amp; Apache", output);
        Assert.Contains("Project&lt;X&gt;", output);
        Assert.DoesNotContain("Pkg<&>", output);
    }

    [Fact]
    public void Generate_HtmlFormat_WithVersionInfo_ShowsLatestColumn()
    {
        var settings = new LicenseCheckSettings { OutputFormat = "html" };
        var generator = new ReportGenerator(settings);

        var entries = new List<PackageReportEntry>
        {
            new("PkgA", "1.0.0", "MIT", false, null, [], LatestVersion: "2.0.0", IsOutdated: true),
            new("PkgB", "3.0.0", "MIT", false, null, [], LatestVersion: "3.0.0", IsOutdated: false)
        };

        var output = generator.Generate(entries, MakeResult(2, 2));

        Assert.Contains("<th>Latest</th>", output);
        Assert.Contains("badge badge-outdated", output);   // PkgA is outdated
        Assert.Contains("badge badge-current", output);    // PkgB is current
        Assert.Contains("2.0.0", output);                  // outdated version shown
    }

    [Fact]
    public void Generate_HtmlFormat_WithoutVersionInfo_NoLatestColumn()
    {
        var settings = new LicenseCheckSettings { OutputFormat = "html" };
        var generator = new ReportGenerator(settings);

        var output = generator.Generate(MakeMixedEntries(), MakeResult(3, 1));

        Assert.DoesNotContain("<th>Latest</th>", output);
        // Check that the badge *spans* aren't used (CSS definitions are always emitted)
        Assert.DoesNotContain("badge badge-outdated", output);
        Assert.DoesNotContain("badge badge-current", output);
    }

    // -------------------------------------------------------------------------
    // Version formatting in console/markdown/json
    // -------------------------------------------------------------------------

    [Fact]
    public void Generate_ConsoleFormat_WithVersionInfo_ShowsLatestColumn()
    {
        var settings = new LicenseCheckSettings { OutputFormat = "console" };
        var generator = new ReportGenerator(settings);

        var entries = new List<PackageReportEntry>
        {
            new("PkgA", "1.0.0", "MIT", false, null, [], LatestVersion: "2.0.0", IsOutdated: true),
            new("PkgB", "3.0.0", "MIT", false, null, [], LatestVersion: "3.0.0", IsOutdated: false)
        };

        var output = generator.Generate(entries, MakeResult(2, 2));

        Assert.Contains("Latest", output);
        Assert.Contains("-> 2.0.0", output);   // outdated
        Assert.Contains("current", output);     // up to date
    }

    [Fact]
    public void Generate_ConsoleFormat_WhenNoVersionInfo_NoLatestColumn()
    {
        var settings = new LicenseCheckSettings { OutputFormat = "console" };
        var generator = new ReportGenerator(settings);

        var output = generator.Generate(MakeMixedEntries(), MakeResult(3, 1));

        Assert.DoesNotContain("Latest", output);
        Assert.DoesNotContain("current", output);
        Assert.DoesNotContain("->", output);
    }

    [Fact]
    public void Generate_MarkdownFormat_WithVersionInfo_ShowsLatestColumn()
    {
        var settings = new LicenseCheckSettings { OutputFormat = "markdown" };
        var generator = new ReportGenerator(settings);

        var entries = new List<PackageReportEntry>
        {
            new("PkgA", "1.0.0", "MIT", false, null, [], LatestVersion: "2.0.0", IsOutdated: true),
            new("PkgB", "3.0.0", "MIT", false, null, [], LatestVersion: "3.0.0", IsOutdated: false)
        };

        var output = generator.Generate(entries, MakeResult(2, 2));

        Assert.Contains("| Package | Version | Latest | License | Projects |", output);
        Assert.Contains("→ 2.0.0", output);   // outdated arrow
        Assert.Contains("✓", output);          // current checkmark
    }

    [Fact]
    public void Generate_JsonFormat_WithVersionInfo_IncludesVersionFields()
    {
        var settings = new LicenseCheckSettings { OutputFormat = "json", ShowAllPackages = true };
        var generator = new ReportGenerator(settings);

        var entries = new List<PackageReportEntry>
        {
            new("PkgA", "1.0.0", "GPL-3.0", true, "Not permitted", [],
                LatestVersion: "2.0.0", IsOutdated: true)
        };
        var result = MakeResult(1, 0, [new LicenseViolation("PkgA", "1.0.0", "GPL-3.0", null, "Not permitted")]);

        var output = generator.Generate(entries, result);

        Assert.Contains("\"latestVersion\"", output);
        Assert.Contains("\"2.0.0\"", output);
        Assert.Contains("\"isOutdated\"", output);
        Assert.Contains("true", output);
    }

    [Fact]
    public void Generate_JsonFormat_WithoutVersionInfo_OmitsVersionFields()
    {
        var settings = new LicenseCheckSettings { OutputFormat = "json" };
        var generator = new ReportGenerator(settings);

        var violations = MakeMixedEntries().Where(e => e.IsViolation).ToList();
        var result = MakeResult(2, 0, [
            new LicenseViolation("BadPackage", "1.0.0", "GPL-3.0", null, "Not permitted"),
            new LicenseViolation("UnknownPkg", "2.0.0", null, null, "No SPDX")
        ]);

        var output = generator.Generate(violations, result);

        Assert.DoesNotContain("\"latestVersion\"", output);
        Assert.DoesNotContain("\"isOutdated\"", output);
    }
}
