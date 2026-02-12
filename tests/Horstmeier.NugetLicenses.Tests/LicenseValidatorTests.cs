using FluentAssertions;
using Horstmeier.NugetLicenses.Configuration;
using Horstmeier.NugetLicenses.Models;
using Horstmeier.NugetLicenses.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Horstmeier.NugetLicenses.Tests;

public class LicenseValidatorTests
{
    private static LicenseValidator CreateValidator(
        string[]? permitted = null,
        ExemptPackage[]? exempt = null)
    {
        var settings = new LicenseCheckSettings
        {
            PermittedLicenses = permitted ?? ["MIT", "Apache-2.0"],
            ExemptPackages = exempt ?? []
        };
        return new LicenseValidator(settings, NullLogger<LicenseValidator>.Instance);
    }

    [Fact]
    public void Validate_PermittedLicense_NoViolations()
    {
        var validator = CreateValidator();
        var licenses = new List<LicenseInfo>
        {
            new("PackageA", "1.0.0", "MIT", null)
        };

        var result = validator.Validate(licenses);

        result.HasViolations.Should().BeFalse();
        result.TotalPackages.Should().Be(1);
        result.ValidPackages.Should().Be(1);
    }

    [Fact]
    public void Validate_DeniedLicense_ReturnsViolation()
    {
        var validator = CreateValidator();
        var licenses = new List<LicenseInfo>
        {
            new("PackageA", "1.0.0", "GPL-3.0", null)
        };

        var result = validator.Validate(licenses);

        result.HasViolations.Should().BeTrue();
        result.Violations.Should().HaveCount(1);
        result.Violations[0].PackageId.Should().Be("PackageA");
        result.Violations[0].Reason.Should().Contain("GPL-3.0");
    }

    [Fact]
    public void Validate_ExemptPackage_SkipsValidation()
    {
        var validator = CreateValidator(exempt: [new ExemptPackage { PackageName = "PackageA", Reason = "Internal" }]);
        var licenses = new List<LicenseInfo>
        {
            new("PackageA", "1.0.0", "GPL-3.0", null)
        };

        var result = validator.Validate(licenses);

        result.HasViolations.Should().BeFalse();
    }

    [Fact]
    public void Validate_ExemptPackage_CaseInsensitive()
    {
        var validator = CreateValidator(exempt: [new ExemptPackage { PackageName = "packagea" }]);
        var licenses = new List<LicenseInfo>
        {
            new("PackageA", "1.0.0", "GPL-3.0", null)
        };

        var result = validator.Validate(licenses);

        result.HasViolations.Should().BeFalse();
    }

    [Fact]
    public void Validate_ExemptPackage_WithVersion_MatchesOnlyThatVersion()
    {
        var validator = CreateValidator(exempt: [new ExemptPackage { PackageName = "PackageA", Version = "1.0.0" }]);
        var licenses = new List<LicenseInfo>
        {
            new("PackageA", "1.0.0", "GPL-3.0", null),
            new("PackageA", "2.0.0", "GPL-3.0", null)
        };

        var result = validator.Validate(licenses);

        result.HasViolations.Should().BeTrue();
        result.Violations.Should().HaveCount(1);
        result.Violations[0].Version.Should().Be("2.0.0");
    }

    [Fact]
    public void Validate_ExemptPackage_WithoutVersion_MatchesAllVersions()
    {
        var validator = CreateValidator(exempt: [new ExemptPackage { PackageName = "PackageA" }]);
        var licenses = new List<LicenseInfo>
        {
            new("PackageA", "1.0.0", "GPL-3.0", null),
            new("PackageA", "2.0.0", "GPL-3.0", null)
        };

        var result = validator.Validate(licenses);

        result.HasViolations.Should().BeFalse();
    }

    [Fact]
    public void Validate_NoLicenseExpression_ReturnsViolation()
    {
        var validator = CreateValidator();
        var licenses = new List<LicenseInfo>
        {
            new("PackageA", "1.0.0", null, "https://example.com/license")
        };

        var result = validator.Validate(licenses);

        result.HasViolations.Should().BeTrue();
        result.Violations[0].Reason.Should().Contain("No SPDX license expression");
        result.Violations[0].LicenseUrl.Should().Be("https://example.com/license");
    }

    [Fact]
    public void Validate_EmptyLicenseExpression_ReturnsViolation()
    {
        var validator = CreateValidator();
        var licenses = new List<LicenseInfo>
        {
            new("PackageA", "1.0.0", "  ", null)
        };

        var result = validator.Validate(licenses);

        result.HasViolations.Should().BeTrue();
    }

    [Fact]
    public void Validate_OrExpression_OnePermitted_NoViolation()
    {
        var validator = CreateValidator();
        var licenses = new List<LicenseInfo>
        {
            new("PackageA", "1.0.0", "MIT OR GPL-3.0", null)
        };

        var result = validator.Validate(licenses);

        result.HasViolations.Should().BeFalse();
    }

    [Fact]
    public void Validate_OrExpression_NonePermitted_Violation()
    {
        var validator = CreateValidator();
        var licenses = new List<LicenseInfo>
        {
            new("PackageA", "1.0.0", "GPL-3.0 OR LGPL-3.0", null)
        };

        var result = validator.Validate(licenses);

        result.HasViolations.Should().BeTrue();
    }

    [Fact]
    public void Validate_AndExpression_AllPermitted_NoViolation()
    {
        var validator = CreateValidator();
        var licenses = new List<LicenseInfo>
        {
            new("PackageA", "1.0.0", "MIT AND Apache-2.0", null)
        };

        var result = validator.Validate(licenses);

        result.HasViolations.Should().BeFalse();
    }

    [Fact]
    public void Validate_AndExpression_OneNotPermitted_Violation()
    {
        var validator = CreateValidator();
        var licenses = new List<LicenseInfo>
        {
            new("PackageA", "1.0.0", "MIT AND GPL-3.0", null)
        };

        var result = validator.Validate(licenses);

        result.HasViolations.Should().BeTrue();
    }

    [Fact]
    public void Validate_PermittedLicense_CaseInsensitive()
    {
        var validator = CreateValidator();
        var licenses = new List<LicenseInfo>
        {
            new("PackageA", "1.0.0", "mit", null)
        };

        var result = validator.Validate(licenses);

        result.HasViolations.Should().BeFalse();
    }

    [Fact]
    public void Validate_MultiplePackages_MixedResults()
    {
        var validator = CreateValidator();
        var licenses = new List<LicenseInfo>
        {
            new("PackageA", "1.0.0", "MIT", null),
            new("PackageB", "2.0.0", "GPL-3.0", null),
            new("PackageC", "3.0.0", "Apache-2.0", null)
        };

        var result = validator.Validate(licenses);

        result.HasViolations.Should().BeTrue();
        result.Violations.Should().HaveCount(1);
        result.Violations[0].PackageId.Should().Be("PackageB");
        result.TotalPackages.Should().Be(3);
        result.ValidPackages.Should().Be(2);
    }

    [Fact]
    public void IsExpressionPermitted_ParenthesizedOr_Works()
    {
        var validator = CreateValidator();

        validator.IsExpressionPermitted("( MIT OR GPL-3.0 )").Should().BeTrue();
    }

    [Fact]
    public void IsExpressionPermitted_NestedExpression_Works()
    {
        var validator = CreateValidator();

        // (MIT AND Apache-2.0) OR GPL-3.0 — the left branch is fully permitted
        validator.IsExpressionPermitted("( MIT AND Apache-2.0 ) OR GPL-3.0").Should().BeTrue();
    }

    [Fact]
    public void Validate_WithException_BasePermitted_NoViolation()
    {
        var validator = CreateValidator();
        var licenses = new List<LicenseInfo>
        {
            new("PackageA", "1.0.0", "Apache-2.0 WITH LLVM-exception", null)
        };

        var result = validator.Validate(licenses);

        result.HasViolations.Should().BeFalse();
    }

    [Fact]
    public void Validate_WithException_BaseNotPermitted_Violation()
    {
        var validator = CreateValidator();
        var licenses = new List<LicenseInfo>
        {
            new("PackageA", "1.0.0", "GPL-3.0 WITH Classpath-exception-2.0", null)
        };

        var result = validator.Validate(licenses);

        result.HasViolations.Should().BeTrue();
        result.Violations[0].Reason.Should().Contain("GPL-3.0 WITH Classpath-exception-2.0");
    }

    [Fact]
    public void Validate_WithException_InCompoundExpression_Works()
    {
        var validator = CreateValidator();
        var licenses = new List<LicenseInfo>
        {
            new("PackageA", "1.0.0", "MIT AND Apache-2.0 WITH LLVM-exception", null)
        };

        var result = validator.Validate(licenses);

        result.HasViolations.Should().BeFalse();
    }

    [Fact]
    public void Validate_ExemptPackage_VersionWildcard_MatchesAllVersions()
    {
        var validator = CreateValidator(exempt: [new ExemptPackage { PackageName = "PackageA", Version = "*" }]);
        var licenses = new List<LicenseInfo>
        {
            new("PackageA", "1.0.0", "GPL-3.0", null),
            new("PackageA", "2.0.0", "GPL-3.0", null)
        };

        var result = validator.Validate(licenses);

        result.HasViolations.Should().BeFalse();
    }

    [Fact]
    public void Validate_ExemptPackage_TrailingWildcard_MatchesPrefix()
    {
        var validator = CreateValidator(exempt: [new ExemptPackage { PackageName = "MyCompany.*" }]);
        var licenses = new List<LicenseInfo>
        {
            new("MyCompany.Core", "1.0.0", "GPL-3.0", null),
            new("MyCompany.Utils", "2.0.0", "GPL-3.0", null),
            new("OtherPackage", "1.0.0", "GPL-3.0", null)
        };

        var result = validator.Validate(licenses);

        result.HasViolations.Should().BeTrue();
        result.Violations.Should().HaveCount(1);
        result.Violations[0].PackageId.Should().Be("OtherPackage");
    }

    [Fact]
    public void Validate_ExemptPackage_TrailingWildcard_CaseInsensitive()
    {
        var validator = CreateValidator(exempt: [new ExemptPackage { PackageName = "mycompany.*" }]);
        var licenses = new List<LicenseInfo>
        {
            new("MyCompany.Core", "1.0.0", "GPL-3.0", null)
        };

        var result = validator.Validate(licenses);

        result.HasViolations.Should().BeFalse();
    }

    [Fact]
    public void Validate_ExemptPackage_TrailingWildcardAndVersionWildcard_Combined()
    {
        var validator = CreateValidator(exempt: [new ExemptPackage { PackageName = "Internal.*", Version = "*" }]);
        var licenses = new List<LicenseInfo>
        {
            new("Internal.Lib", "1.0.0", "GPL-3.0", null),
            new("Internal.Api", "3.5.0", "AGPL-3.0", null),
            new("External.Lib", "1.0.0", "GPL-3.0", null)
        };

        var result = validator.Validate(licenses);

        result.HasViolations.Should().BeTrue();
        result.Violations.Should().HaveCount(1);
        result.Violations[0].PackageId.Should().Be("External.Lib");
    }
}
