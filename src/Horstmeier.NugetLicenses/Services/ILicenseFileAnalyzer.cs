namespace Horstmeier.NugetLicenses.Services;

public interface ILicenseFileAnalyzer
{
    Task<string?> TryIdentifyFromUrlAsync(string licenseUrl, CancellationToken ct = default);
}
