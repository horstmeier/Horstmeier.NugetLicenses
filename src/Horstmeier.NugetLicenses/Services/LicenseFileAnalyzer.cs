using Microsoft.Extensions.Logging;

namespace Horstmeier.NugetLicenses.Services;

public class LicenseFileAnalyzer : ILicenseFileAnalyzer
{
    private static readonly TimeSpan HttpTimeout = TimeSpan.FromSeconds(5);

    private static readonly Uri NuGetLicensesBaseUri = new("https://licenses.nuget.org/");

    private static readonly List<(string SpdxId, Func<string, bool> Match)> Fingerprints =
    [
        ("MIT", text => text.Contains("permission is hereby granted, free of charge")),
        ("Apache-2.0", text => text.Contains("apache license") && text.Contains("version 2.0")),
        ("BSD-3-Clause", text => text.Contains("redistribution and use in source and binary forms") && text.Contains("neither the name")),
        ("BSD-2-Clause", text => text.Contains("redistribution and use in source and binary forms") && !text.Contains("neither the name")),
        ("ISC", text => text.Contains("permission to use, copy, modify, and/or distribute")),
        ("Unlicense", text => text.Contains("this is free and unencumbered software")),
        ("MS-PL", text => text.Contains("microsoft public license")),
        ("MPL-2.0", text => text.Contains("mozilla public license") && text.Contains("version 2.0")),
        ("LGPL-2.1", text => text.Contains("gnu lesser general public license") && text.Contains("version 2.1")),
        ("GPL-3.0", text => text.Contains("gnu general public license") && text.Contains("version 3")),
        ("GPL-2.0", text => text.Contains("gnu general public license") && text.Contains("version 2") && !text.Contains("lesser") && !text.Contains("version 3")),
    ];

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<LicenseFileAnalyzer> _logger;

    public LicenseFileAnalyzer(IHttpClientFactory httpClientFactory, ILogger<LicenseFileAnalyzer> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<string?> TryIdentifyFromUrlAsync(string licenseUrl, CancellationToken ct = default)
    {
        // Stage 1: URL pattern matching for licenses.nuget.org
        if (Uri.TryCreate(licenseUrl, UriKind.Absolute, out var uri)
            && NuGetLicensesBaseUri.IsBaseOf(uri))
        {
            var spdx = uri.AbsolutePath.TrimStart('/');
            if (!string.IsNullOrEmpty(spdx))
            {
                _logger.LogDebug("Extracted SPDX from NuGet URL: {Spdx}", spdx);
                return spdx;
            }
        }

        // Stage 2: Fetch and analyze content
        try
        {
            using var client = _httpClientFactory.CreateClient();
            client.Timeout = HttpTimeout;

            using var response = await client.GetAsync(licenseUrl, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug("HTTP {StatusCode} fetching license from {Url}", (int)response.StatusCode, licenseUrl);
                return null;
            }

            var contentType = response.Content.Headers.ContentType?.MediaType;
            if (contentType is not null && !contentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug("Non-text content type {ContentType} from {Url}", contentType, licenseUrl);
                return null;
            }

            var text = await response.Content.ReadAsStringAsync(ct);
            var lowerText = text.ToLowerInvariant();

            foreach (var (spdxId, match) in Fingerprints)
            {
                if (match(lowerText))
                {
                    _logger.LogDebug("Identified {SpdxId} from content at {Url}", spdxId, licenseUrl);
                    return spdxId;
                }
            }

            _logger.LogDebug("Could not identify license from content at {Url}", licenseUrl);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.LogDebug("Timeout fetching license from {Url}", licenseUrl);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error fetching license from {Url}", licenseUrl);
        }

        return null;
    }
}
