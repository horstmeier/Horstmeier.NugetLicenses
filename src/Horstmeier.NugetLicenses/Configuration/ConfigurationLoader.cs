using System.Text.Json;

namespace Horstmeier.NugetLicenses.Configuration;

internal static class ConfigurationLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    /// <summary>
    /// Returns the platform-appropriate global config path:
    /// ~/.config/nuget-licenses/config.json (Linux/macOS) or
    /// %APPDATA%\nuget-licenses\config.json (Windows).
    /// </summary>
    public static string GetGlobalConfigPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "nuget-licenses", "config.json");
    }

    /// <summary>
    /// Walks up the directory tree from <paramref name="startDirectory"/> looking for
    /// a <c>nuget-licenses.json</c> file, stopping at the filesystem root.
    /// Returns the first path found, or null if none exists.
    /// </summary>
    public static string? FindProjectConfig(string startDirectory)
    {
        var current = Path.GetFullPath(startDirectory);
        while (true)
        {
            var candidate = Path.Combine(current, "nuget-licenses.json");
            if (File.Exists(candidate))
                return candidate;

            var parent = Path.GetDirectoryName(current);
            if (parent == null || parent == current)
                return null;

            current = parent;
        }
    }

    /// <summary>
    /// Deserializes a nuget-licenses.json (or global config.json) file.
    /// Returns null and writes an error to stderr if the file is malformed.
    /// Throws <see cref="FileNotFoundException"/> if the file does not exist.
    /// </summary>
    public static NugetLicensesConfig? LoadConfigFile(string path)
    {
        var json = File.ReadAllText(path);
        try
        {
            return JsonSerializer.Deserialize<NugetLicensesConfig>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            Console.Error.WriteLine($"Error: Could not parse config file '{path}': {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Applies non-null values from <paramref name="config"/> onto <paramref name="settings"/>,
    /// replacing (not merging) arrays such as PermittedLicenses and ExemptPackages.
    /// </summary>
    public static void Apply(LicenseCheckSettings settings, NugetLicensesConfig config)
    {
        if (config.PermittedLicenses != null) settings.PermittedLicenses = config.PermittedLicenses;
        if (config.ExemptPackages != null) settings.ExemptPackages = config.ExemptPackages;
        if (config.NuGetSource != null) settings.NuGetSource = config.NuGetSource;
        if (config.EnableCache != null) settings.EnableCache = config.EnableCache.Value;
        if (config.CacheDurationDays != null) settings.CacheDurationDays = config.CacheDurationDays.Value;
        if (config.EnableLicenseFileHeuristics != null) settings.EnableLicenseFileHeuristics = config.EnableLicenseFileHeuristics.Value;
    }
}
