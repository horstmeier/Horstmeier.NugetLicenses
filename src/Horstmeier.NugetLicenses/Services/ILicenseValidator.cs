using Horstmeier.NugetLicenses.Models;

namespace Horstmeier.NugetLicenses.Services;

public interface ILicenseValidator
{
    LicenseValidationResult Validate(IReadOnlyList<LicenseInfo> licenses);
}
