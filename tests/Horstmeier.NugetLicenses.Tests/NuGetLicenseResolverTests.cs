using Horstmeier.NugetLicenses.Configuration;
using Horstmeier.NugetLicenses.Models;
using Horstmeier.NugetLicenses.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Horstmeier.NugetLicenses.Tests;

[Trait("Category", "Integration")]
public class NuGetLicenseResolverTests
{
    private readonly NuGetLicenseResolver _resolver;

    public NuGetLicenseResolverTests()
    {
        var httpFactory = new DefaultHttpClientFactory();
        var analyzer = new LicenseFileAnalyzer(httpFactory, NullLogger<LicenseFileAnalyzer>.Instance);
        _resolver = new NuGetLicenseResolver(new LicenseCheckSettings(), analyzer, NullLogger<NuGetLicenseResolver>.Instance);
    }

    private class DefaultHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }

    [Fact]
    public async Task ResolveAsync_KnownPackageWithSpdx_ReturnsLicenseExpression()
    {
        var packages = new List<PackageReference>
        {
            new("Newtonsoft.Json", "13.0.3", "Direct", "net8.0")
        };

        var result = await _resolver.ResolveAsync(packages);

        Assert.Single(result);
        Assert.Equal("Newtonsoft.Json", result[0].PackageId);
        Assert.Equal("MIT", result[0].LicenseExpression);
    }

    [Fact]
    public async Task ResolveAsync_NonExistentPackage_ReturnsNullLicense()
    {
        var packages = new List<PackageReference>
        {
            new("This.Package.Does.Not.Exist.12345", "1.0.0", "Direct", "net8.0")
        };

        var result = await _resolver.ResolveAsync(packages);

        Assert.Single(result);
        Assert.Null(result[0].LicenseExpression);
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

        Assert.Equal(2, result.Count);
        Assert.All(result, r => Assert.False(string.IsNullOrEmpty(r.PackageId)));
    }
}
