using FluentAssertions;
using Horstmeier.NugetLicenses.Configuration;
using Microsoft.Extensions.Configuration;

namespace Horstmeier.NugetLicenses.Tests;

public class ConfigurationTests : IDisposable
{
    private readonly List<string> _tempFiles = [];

    private string CreateTempJsonFile(string json)
    {
        var path = Path.GetTempFileName();
        var jsonPath = Path.ChangeExtension(path, ".json");
        File.WriteAllText(jsonPath, json);
        _tempFiles.Add(path);
        _tempFiles.Add(jsonPath);
        return jsonPath;
    }

    [Fact]
    public void Settings_BindFromJson_CorrectValues()
    {
        var json = """
        {
          "LicenseCheck": {
            "PermittedLicenses": ["MIT", "Apache-2.0"],
            "ExemptPackages": [
              {
                "PackageName": "MyInternal.Pkg",
                "Version": "1.0.0",
                "Reason": "Internal package"
              }
            ],
            "ProjectPath": "/some/path"
          }
        }
        """;

        var path = CreateTempJsonFile(json);
        var config = new ConfigurationBuilder()
            .AddJsonFile(path)
            .Build();

        var settings = new LicenseCheckSettings();
        config.GetSection("LicenseCheck").Bind(settings);

        settings.PermittedLicenses.Should().BeEquivalentTo("MIT", "Apache-2.0");
        settings.ExemptPackages.Should().HaveCount(1);
        settings.ExemptPackages[0].PackageName.Should().Be("MyInternal.Pkg");
        settings.ExemptPackages[0].Version.Should().Be("1.0.0");
        settings.ExemptPackages[0].Reason.Should().Be("Internal package");
        settings.ProjectPath.Should().Be("/some/path");
    }

    [Fact]
    public void Settings_Defaults_AreReasonable()
    {
        var settings = new LicenseCheckSettings();

        settings.PermittedLicenses.Should().BeEmpty();
        settings.ExemptPackages.Should().BeEmpty();
        settings.ProjectPath.Should().Be(".");
        settings.ShowAllPackages.Should().BeFalse();
        settings.OutputFormat.Should().Be("console");
    }

    [Fact]
    public void Settings_CommandLineOverride_TakesPrecedence()
    {
        var json = """
        {
          "LicenseCheck": {
            "ProjectPath": "/default/path"
          }
        }
        """;

        var path = CreateTempJsonFile(json);
        var config = new ConfigurationBuilder()
            .AddJsonFile(path)
            .AddCommandLine(["--ProjectPath=/cli/path"])
            .Build();

        var settings = new LicenseCheckSettings();
        config.GetSection("LicenseCheck").Bind(settings);

        // CLI override at root level
        var cliPath = config["ProjectPath"];
        if (!string.IsNullOrWhiteSpace(cliPath))
            settings.ProjectPath = cliPath;

        settings.ProjectPath.Should().Be("/cli/path");
    }

    [Fact]
    public void Settings_BindShowAllPackagesFromJson()
    {
        var json = """
        {
          "LicenseCheck": {
            "ShowAllPackages": true,
            "OutputFormat": "markdown"
          }
        }
        """;

        var path = CreateTempJsonFile(json);
        var config = new ConfigurationBuilder()
            .AddJsonFile(path)
            .Build();

        var settings = new LicenseCheckSettings();
        config.GetSection("LicenseCheck").Bind(settings);

        settings.ShowAllPackages.Should().BeTrue();
        settings.OutputFormat.Should().Be("markdown");
    }

    public void Dispose()
    {
        foreach (var file in _tempFiles)
        {
            try { File.Delete(file); }
            catch { /* ignore cleanup errors */ }
        }
    }
}
