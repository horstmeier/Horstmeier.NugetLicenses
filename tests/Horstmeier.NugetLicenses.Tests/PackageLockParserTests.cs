using FluentAssertions;
using Horstmeier.NugetLicenses.Services;

namespace Horstmeier.NugetLicenses.Tests;

public class PackageLockParserTests : IDisposable
{
    private readonly List<string> _tempFiles = [];
    private readonly PackageLockParser _parser = new();

    private string CreateTempLockFile(string json)
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, json);
        _tempFiles.Add(path);
        return path;
    }

    [Fact]
    public void Parse_SingleFramework_ReturnsPackages()
    {
        var json = """
        {
          "version": 1,
          "dependencies": {
            "net8.0": {
              "Newtonsoft.Json": {
                "type": "Direct",
                "resolved": "13.0.3"
              },
              "Serilog": {
                "type": "Transitive",
                "resolved": "3.1.1"
              }
            }
          }
        }
        """;

        var path = CreateTempLockFile(json);
        var result = _parser.Parse(path);

        result.Should().HaveCount(2);
        result[0].Id.Should().Be("Newtonsoft.Json");
        result[0].Version.Should().Be("13.0.3");
        result[0].Type.Should().Be("Direct");
        result[0].TargetFramework.Should().Be("net8.0");
        result[1].Id.Should().Be("Serilog");
        result[1].Type.Should().Be("Transitive");
    }

    [Fact]
    public void Parse_MultipleFrameworks_ReturnsAllPackages()
    {
        var json = """
        {
          "version": 1,
          "dependencies": {
            "net8.0": {
              "PackageA": { "type": "Direct", "resolved": "1.0.0" }
            },
            "net9.0": {
              "PackageB": { "type": "Direct", "resolved": "2.0.0" }
            }
          }
        }
        """;

        var path = CreateTempLockFile(json);
        var result = _parser.Parse(path);

        result.Should().HaveCount(2);
        result.Should().Contain(p => p.Id == "PackageA" && p.TargetFramework == "net8.0");
        result.Should().Contain(p => p.Id == "PackageB" && p.TargetFramework == "net9.0");
    }

    [Fact]
    public void Parse_DuplicateAcrossFrameworks_DeduplicatesByIdAndVersion()
    {
        var json = """
        {
          "version": 1,
          "dependencies": {
            "net8.0": {
              "PackageA": { "type": "Direct", "resolved": "1.0.0" }
            },
            "net9.0": {
              "PackageA": { "type": "Direct", "resolved": "1.0.0" }
            }
          }
        }
        """;

        var path = CreateTempLockFile(json);
        var result = _parser.Parse(path);

        result.Should().HaveCount(1);
        result[0].Id.Should().Be("PackageA");
    }

    [Fact]
    public void Parse_EmptyDependencies_ReturnsEmpty()
    {
        var json = """
        {
          "version": 1,
          "dependencies": {}
        }
        """;

        var path = CreateTempLockFile(json);
        var result = _parser.Parse(path);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Parse_NoDependenciesProperty_ReturnsEmpty()
    {
        var json = """{ "version": 1 }""";

        var path = CreateTempLockFile(json);
        var result = _parser.Parse(path);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Parse_MissingFile_ThrowsFileNotFoundException()
    {
        var act = () => _parser.Parse("/nonexistent/packages.lock.json");
        act.Should().Throw<FileNotFoundException>();
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
