using Horstmeier.NugetLicenses.Models;

namespace Horstmeier.NugetLicenses.Services;

public interface IReportGenerator
{
    string Generate(IReadOnlyList<PackageReportEntry> entries, LicenseValidationResult result,
        IReadOnlyList<ProjectInfo>? projects = null);
}
