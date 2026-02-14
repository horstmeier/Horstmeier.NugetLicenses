using FluentAssertions;
using Horstmeier.NugetLicenses.Models;
using Horstmeier.NugetLicenses.Services;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Horstmeier.NugetLicenses.Tests;

public class PackageLockParserTests : IDisposable
{
    private readonly List<string> _tempFiles = [];
    private readonly List<string> _tempDirs = [];
    private readonly IProjectFileParser _projectFileParser = Substitute.For<IProjectFileParser>();
    private readonly PackageLockParser _parser;

    public PackageLockParserTests()
    {
        _parser = new PackageLockParser(NullLogger<PackageLockParser>.Instance, _projectFileParser);
    }

    private string CreateTempLockFile(string json)
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, json);
        _tempFiles.Add(path);
        return path;
    }

    private string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);
        _tempDirs.Add(dir);
        return dir;
    }

    private void CreateLockFile(string directory, string json)
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "packages.lock.json"), json);
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

    [Fact]
    public async Task ParseDirectoryAsync_FindsLockFilesRecursively()
    {
        var root = CreateTempDir();
        CreateLockFile(Path.Combine(root, "projectA"), """
        {
          "version": 1,
          "dependencies": {
            "net10.0": {
              "PackageA": { "type": "Direct", "resolved": "1.0.0" }
            }
          }
        }
        """);
        CreateLockFile(Path.Combine(root, "src", "projectB"), """
        {
          "version": 1,
          "dependencies": {
            "net10.0": {
              "PackageB": { "type": "Transitive", "resolved": "2.0.0" }
            }
          }
        }
        """);

        // No csproj files → legacy lock file scan
        var result = await _parser.ParseDirectoryAsync(root);

        result.Packages.Should().HaveCount(2);
        result.Packages.Should().Contain(p => p.Id == "PackageA" && p.Version == "1.0.0");
        result.Packages.Should().Contain(p => p.Id == "PackageB" && p.Version == "2.0.0");
        result.LockFileCount.Should().Be(2);
        result.Projects.Should().BeEmpty();
    }

    [Fact]
    public async Task ParseDirectoryAsync_NoLockFiles_ReturnsEmpty()
    {
        var root = CreateTempDir();

        var result = await _parser.ParseDirectoryAsync(root);

        result.Packages.Should().BeEmpty();
        result.ProjectsByPackage.Should().BeEmpty();
        result.LockFileCount.Should().Be(0);
        result.Projects.Should().BeEmpty();
    }

    [Fact]
    public async Task ParseDirectoryAsync_DuplicateAcrossProjects_Deduplicated()
    {
        var root = CreateTempDir();
        var json = """
        {
          "version": 1,
          "dependencies": {
            "net10.0": {
              "SharedPackage": { "type": "Direct", "resolved": "3.0.0" }
            }
          }
        }
        """;
        CreateLockFile(Path.Combine(root, "projectA"), json);
        CreateLockFile(Path.Combine(root, "projectB"), json);

        var result = await _parser.ParseDirectoryAsync(root);

        result.Packages.Should().HaveCount(1);
        result.Packages[0].Id.Should().Be("SharedPackage");
        result.Packages[0].Version.Should().Be("3.0.0");
    }

    [Fact]
    public async Task ParseDirectoryAsync_TracksProjectsPerPackage()
    {
        var root = CreateTempDir();
        var sharedJson = """
        {
          "version": 1,
          "dependencies": {
            "net10.0": {
              "SharedPackage": { "type": "Direct", "resolved": "1.0.0" },
              "UniqueA": { "type": "Direct", "resolved": "2.0.0" }
            }
          }
        }
        """;
        var projectBJson = """
        {
          "version": 1,
          "dependencies": {
            "net10.0": {
              "SharedPackage": { "type": "Direct", "resolved": "1.0.0" },
              "UniqueB": { "type": "Transitive", "resolved": "3.0.0" }
            }
          }
        }
        """;
        CreateLockFile(Path.Combine(root, "projectA"), sharedJson);
        CreateLockFile(Path.Combine(root, "projectB"), projectBJson);

        var result = await _parser.ParseDirectoryAsync(root);

        result.Packages.Should().HaveCount(3);
        result.LockFileCount.Should().Be(2);

        var sharedKey = "sharedpackage|1.0.0";
        result.ProjectsByPackage.Should().ContainKey(sharedKey);
        result.ProjectsByPackage[sharedKey].Should().BeEquivalentTo("projectA", "projectB");

        var uniqueAKey = "uniquea|2.0.0";
        result.ProjectsByPackage.Should().ContainKey(uniqueAKey);
        result.ProjectsByPackage[uniqueAKey].Should().BeEquivalentTo("projectA");

        var uniqueBKey = "uniqueb|3.0.0";
        result.ProjectsByPackage.Should().ContainKey(uniqueBKey);
        result.ProjectsByPackage[uniqueBKey].Should().BeEquivalentTo("projectB");
    }

    [Fact]
    public async Task ParseDirectoryAsync_WithCsprojAndLockFile_UsesLockFileAndBuildsProjectInfo()
    {
        var root = CreateTempDir();
        var projectDir = Path.Combine(root, "MyApp");
        Directory.CreateDirectory(projectDir);

        var csprojPath = Path.Combine(projectDir, "MyApp.csproj");
        File.WriteAllText(csprojPath, "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");

        CreateLockFile(projectDir, """
        {
          "version": 1,
          "dependencies": {
            "net10.0": {
              "PackageA": { "type": "Direct", "resolved": "1.0.0" },
              "PackageB": { "type": "Transitive", "resolved": "2.0.0" }
            }
          }
        }
        """);

        _projectFileParser.IsPackageLockEnabled(csprojPath).Returns(true);
        _projectFileParser.GetCustomLockFilePath(csprojPath).Returns((string?)null);

        var result = await _parser.ParseDirectoryAsync(root);

        result.Packages.Should().HaveCount(2);
        result.LockFileCount.Should().Be(1);
        result.Projects.Should().HaveCount(1);

        var project = result.Projects[0];
        project.ProjectName.Should().Be("MyApp");
        project.PackageCount.Should().Be(2);
        project.LockFileEnabled.Should().BeTrue();
        project.HasLockFile.Should().BeTrue();
    }

    [Fact]
    public async Task ParseDirectoryAsync_WithCsprojNoLockFile_UsesDotnetListPackage()
    {
        var root = CreateTempDir();
        var projectDir = Path.Combine(root, "MyLib");
        Directory.CreateDirectory(projectDir);

        var csprojPath = Path.Combine(projectDir, "MyLib.csproj");
        File.WriteAllText(csprojPath, "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");

        // No packages.lock.json

        _projectFileParser.IsPackageLockEnabled(csprojPath).Returns(false);
        _projectFileParser.GetCustomLockFilePath(csprojPath).Returns((string?)null);
        _projectFileParser.GetPackagesAsync(csprojPath, Arg.Any<CancellationToken>())
            .Returns(new List<PackageReference>
            {
                new("SomePackage", "3.0.0", "Direct", "net10.0"),
                new("TransPackage", "1.0.0", "Transitive", "net10.0")
            });

        var result = await _parser.ParseDirectoryAsync(root);

        result.Packages.Should().HaveCount(2);
        result.LockFileCount.Should().Be(0);
        result.Projects.Should().HaveCount(1);

        var project = result.Projects[0];
        project.ProjectName.Should().Be("MyLib");
        project.PackageCount.Should().Be(2);
        project.LockFileEnabled.Should().BeFalse();
        project.HasLockFile.Should().BeFalse();
    }

    [Fact]
    public async Task ParseDirectoryAsync_WithFsprojFile_Supported()
    {
        var root = CreateTempDir();
        var projectDir = Path.Combine(root, "MyFSharpLib");
        Directory.CreateDirectory(projectDir);

        var fsprojPath = Path.Combine(projectDir, "MyFSharpLib.fsproj");
        File.WriteAllText(fsprojPath, "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");

        CreateLockFile(projectDir, """
        {
          "version": 1,
          "dependencies": {
            "net10.0": {
              "FSharpPackage": { "type": "Direct", "resolved": "5.0.0" }
            }
          }
        }
        """);

        _projectFileParser.IsPackageLockEnabled(fsprojPath).Returns(false);
        _projectFileParser.GetCustomLockFilePath(fsprojPath).Returns((string?)null);

        var result = await _parser.ParseDirectoryAsync(root);

        result.Packages.Should().HaveCount(1);
        result.Packages[0].Id.Should().Be("FSharpPackage");
        result.Projects.Should().HaveCount(1);
        result.Projects[0].ProjectName.Should().Be("MyFSharpLib");
        result.Projects[0].HasLockFile.Should().BeTrue();
    }

    public void Dispose()
    {
        foreach (var file in _tempFiles)
        {
            try { File.Delete(file); }
            catch { /* ignore cleanup errors */ }
        }
        foreach (var dir in _tempDirs)
        {
            try { Directory.Delete(dir, recursive: true); }
            catch { /* ignore cleanup errors */ }
        }
    }
}
