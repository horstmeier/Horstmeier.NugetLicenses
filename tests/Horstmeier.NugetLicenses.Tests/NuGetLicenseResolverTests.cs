using FluentAssertions;
using Horstmeier.NugetLicenses.Configuration;
using Horstmeier.NugetLicenses.Models;
using Horstmeier.NugetLicenses.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Horstmeier.NugetLicenses.Tests;

[Trait("Category", "Integration")]
public class NuGetLicenseResolverTests
{
    private readonly NuGetLicenseResolver _resolver = new(new LicenseCheckSettings(), NullLogger<NuGetLicenseResolver>.Instance);

    [Fact]
    public async Task ResolveAsync_KnownPackageWithSpdx_ReturnsLicenseExpression()
    {
        var packages = new List<PackageReference>
        {
            new("Newtonsoft.Json", "13.0.3", "Direct", "net8.0")
        };

        var result = await _resolver.ResolveAsync(packages);

        result.Should().HaveCount(1);
        result[0].PackageId.Should().Be("Newtonsoft.Json");
        result[0].LicenseExpression.Should().Be("MIT");
    }

    [Fact]
    public async Task ResolveAsync_NonExistentPackage_ReturnsNullLicense()
    {
        var packages = new List<PackageReference>
        {
            new("This.Package.Does.Not.Exist.12345", "1.0.0", "Direct", "net8.0")
        };

        var result = await _resolver.ResolveAsync(packages);

        result.Should().HaveCount(1);
        result[0].LicenseExpression.Should().BeNull();
    }

    [Fact]
    public async Task ResolveAsync_MultiplePackages_ReturnsAll()
    {
        var packages = new List<PackageReference>
        {
            new("Newtonsoft.Json", "13.0.3", "Direct", "net8.0"),
            new("Serilog", "4.0.0", "Direct", "net8.0")
        };

        var result = await _resolver.ResolveAsync(packages);

        result.Should().HaveCount(2);
        result.Should().AllSatisfy(r => r.PackageId.Should().NotBeNullOrEmpty());
    }
}
