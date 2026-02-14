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

        Assert.Equal(new[] { "MIT", "Apache-2.0" }, settings.PermittedLicenses);
        Assert.Single(settings.ExemptPackages);
        Assert.Equal("MyInternal.Pkg", settings.ExemptPackages[0].PackageName);
        Assert.Equal("1.0.0", settings.ExemptPackages[0].Version);
        Assert.Equal("Internal package", settings.ExemptPackages[0].Reason);
        Assert.Equal("/some/path", settings.ProjectPath);
    }

    [Fact]
    public void Settings_Defaults_AreReasonable()
    {
        var settings = new LicenseCheckSettings();

        Assert.Empty(settings.PermittedLicenses);
        Assert.Empty(settings.ExemptPackages);
        Assert.Equal(".", settings.ProjectPath);
        Assert.False(settings.ShowAllPackages);
        Assert.Equal("console", settings.OutputFormat);
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

        Assert.True(settings.ShowAllPackages);
        Assert.Equal("markdown", settings.OutputFormat);
    }

    [Fact]
    public void CommandLineOptions_MergeWithSettings_OverridesCorrectly()
    {
        // Arrange - base settings from configuration
        var settings = new LicenseCheckSettings
        {
            ProjectPath = "/default/path",
            ShowAllPackages = false,
            OutputFormat = "console",
            EnableCache = true,
            CacheDurationDays = 7
        };

        // CLI options with overrides
        var cliOptions = new CommandLineOptions
        {
            ProjectPath = "/cli/path",
            ShowAllPackages = true,
            OutputFormat = "json"
            // DisableCache and CacheDurationDays not specified (null)
        };

        // Act - merge CLI options into settings
        MergeSettingsForTest(settings, cliOptions);

        // Assert - CLI values override, nulls don't change settings
        Assert.Equal("/cli/path", settings.ProjectPath);
        Assert.True(settings.ShowAllPackages);
        Assert.Equal("json", settings.OutputFormat);
        Assert.True(settings.EnableCache); // Unchanged
        Assert.Equal(7, settings.CacheDurationDays); // Unchanged
    }

    [Fact]
    public void CommandLineOptions_NullValues_DontOverrideSettings()
    {
        // Arrange
        var settings = new LicenseCheckSettings
        {
            ProjectPath = "/default/path",
            ShowAllPackages = true,
            EnableCache = false
        };

        var cliOptions = new CommandLineOptions
        {
            // All null - no overrides
        };

        // Act
        MergeSettingsForTest(settings, cliOptions);

        // Assert - nothing changed
        Assert.Equal("/default/path", settings.ProjectPath);
        Assert.True(settings.ShowAllPackages);
        Assert.False(settings.EnableCache);
    }

    [Fact]
    public void CommandLineOptions_DisableCacheFlag_DisablesCaching()
    {
        // Arrange - cache is enabled by default
        var settings = new LicenseCheckSettings { EnableCache = true };

        // Act - set DisableCache flag
        var cliOptions = new CommandLineOptions { DisableCache = true };
        MergeSettingsForTest(settings, cliOptions);

        // Assert - cache should now be disabled
        Assert.False(settings.EnableCache);
    }

    [Fact]
    public void CommandLineOptions_QuietFlag_ReturnsCorrectly()
    {
        var cliOptions = new CommandLineOptions { Quiet = true };
        var quiet = MergeSettingsForTest(new LicenseCheckSettings(), cliOptions);
        Assert.True(quiet);

        cliOptions = new CommandLineOptions { Quiet = false };
        quiet = MergeSettingsForTest(new LicenseCheckSettings(), cliOptions);
        Assert.False(quiet);

        cliOptions = new CommandLineOptions { Quiet = null };
        quiet = MergeSettingsForTest(new LicenseCheckSettings(), cliOptions);
        Assert.False(quiet); // Default
    }

    [Fact]
    public void Settings_Defaults_CheckUpdatesFalse()
    {
        var settings = new LicenseCheckSettings();

        Assert.False(settings.CheckUpdates);
        Assert.False(settings.CheckUpdatesAll);
    }

    [Fact]
    public void Settings_BindCheckUpdatesFromJson()
    {
        var json = """
        {
          "LicenseCheck": {
            "CheckUpdates": true,
            "CheckUpdatesAll": true
          }
        }
        """;

        var path = CreateTempJsonFile(json);
        var config = new ConfigurationBuilder()
            .AddJsonFile(path)
            .Build();

        var settings = new LicenseCheckSettings();
        config.GetSection("LicenseCheck").Bind(settings);

        Assert.True(settings.CheckUpdates);
        Assert.True(settings.CheckUpdatesAll);
    }

    [Fact]
    public void CommandLineOptions_CheckUpdates_MergesIntoSettings()
    {
        var settings = new LicenseCheckSettings { CheckUpdates = false, CheckUpdatesAll = false };
        var cli = new CommandLineOptions { CheckUpdates = true };

        MergeSettingsForTest(settings, cli);

        Assert.True(settings.CheckUpdates);
        Assert.False(settings.CheckUpdatesAll);
    }

    [Fact]
    public void CommandLineOptions_CheckUpdatesAll_ImpliesCheckUpdates()
    {
        // When CheckUpdatesAll is set, the merge logic also forces CheckUpdates = true
        var settings = new LicenseCheckSettings { CheckUpdates = false, CheckUpdatesAll = false };
        var cli = new CommandLineOptions { CheckUpdatesAll = true };

        MergeSettingsForTest(settings, cli);

        Assert.True(settings.CheckUpdates);
        Assert.True(settings.CheckUpdatesAll);
    }

    [Fact]
    public void CommandLineOptions_NullCheckUpdates_DoesNotOverrideSettings()
    {
        var settings = new LicenseCheckSettings { CheckUpdates = true, CheckUpdatesAll = true };
        var cli = new CommandLineOptions { CheckUpdates = null, CheckUpdatesAll = null };

        MergeSettingsForTest(settings, cli);

        Assert.True(settings.CheckUpdates);
        Assert.True(settings.CheckUpdatesAll);
    }

    [Fact]
    public void Settings_Defaults_NewPropertiesHaveExpectedValues()
    {
        var settings = new LicenseCheckSettings();

        Assert.Null(settings.NuGetApiKey);
        Assert.Null(settings.CacheDirectory);
        Assert.False(settings.RequireLockFiles);
    }

    [Fact]
    public void CommandLineOptions_NuGetApiKey_MergesIntoSettings()
    {
        var settings = new LicenseCheckSettings();
        var cli = new CommandLineOptions { NuGetApiKey = "my-secret-key" };

        MergeSettingsForTest(settings, cli);

        Assert.Equal("my-secret-key", settings.NuGetApiKey);
    }

    [Fact]
    public void CommandLineOptions_CacheDir_MergesIntoSettings()
    {
        var settings = new LicenseCheckSettings();
        var cli = new CommandLineOptions { CacheDir = "/mnt/cache/nuget" };

        MergeSettingsForTest(settings, cli);

        Assert.Equal("/mnt/cache/nuget", settings.CacheDirectory);
    }

    [Fact]
    public void CommandLineOptions_RequireLockFiles_MergesIntoSettings()
    {
        var settings = new LicenseCheckSettings { RequireLockFiles = false };
        var cli = new CommandLineOptions { RequireLockFiles = true };

        MergeSettingsForTest(settings, cli);

        Assert.True(settings.RequireLockFiles);
    }

    // Helper method that mimics the MergeSettings logic from Program.cs
    private static bool MergeSettingsForTest(LicenseCheckSettings settings, CommandLineOptions cli)
    {
        if (cli.ProjectPath != null) settings.ProjectPath = cli.ProjectPath;
        if (cli.ShowAllPackages != null) settings.ShowAllPackages = cli.ShowAllPackages.Value;
        if (cli.OutputFormat != null) settings.OutputFormat = cli.OutputFormat;
        if (cli.DisableCache != null) settings.EnableCache = !cli.DisableCache.Value;
        if (cli.CacheDurationDays != null) settings.CacheDurationDays = cli.CacheDurationDays.Value;
        if (cli.NuGetSource != null) settings.NuGetSource = cli.NuGetSource;
        if (cli.CheckUpdates != null) settings.CheckUpdates = cli.CheckUpdates.Value;
        if (cli.CheckUpdatesAll != null) settings.CheckUpdatesAll = cli.CheckUpdatesAll.Value;
        if (cli.NuGetApiKey != null) settings.NuGetApiKey = cli.NuGetApiKey;
        if (cli.CacheDir != null) settings.CacheDirectory = cli.CacheDir;
        if (cli.RequireLockFiles != null) settings.RequireLockFiles = cli.RequireLockFiles.Value;
        if (settings.CheckUpdatesAll) settings.CheckUpdates = true;

        return cli.Quiet ?? false;
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
