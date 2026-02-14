using System.Net;
using Horstmeier.NugetLicenses.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Horstmeier.NugetLicenses.Tests;

public class LicenseFileAnalyzerTests
{
    private static LicenseFileAnalyzer CreateAnalyzer(HttpMessageHandler handler)
    {
        var factory = new TestHttpClientFactory(handler);
        return new LicenseFileAnalyzer(factory, NullLogger<LicenseFileAnalyzer>.Instance);
    }

    [Fact]
    public async Task TryIdentify_NuGetOrgUrl_ExtractsSpdxDirectly()
    {
        var analyzer = CreateAnalyzer(new FakeHandler(HttpStatusCode.OK, ""));

        var result = await analyzer.TryIdentifyFromUrlAsync("https://licenses.nuget.org/MIT");

        Assert.Equal("MIT", result);
    }

    [Fact]
    public async Task TryIdentify_NuGetOrgUrl_CompoundExpression()
    {
        var analyzer = CreateAnalyzer(new FakeHandler(HttpStatusCode.OK, ""));

        var result = await analyzer.TryIdentifyFromUrlAsync("https://licenses.nuget.org/Apache-2.0");

        Assert.Equal("Apache-2.0", result);
    }

    [Fact]
    public async Task TryIdentify_MitLicenseText_ReturnsMit()
    {
        var mitText = """
            MIT License

            Copyright (c) 2024 Example

            Permission is hereby granted, free of charge, to any person obtaining a copy
            of this software and associated documentation files (the "Software"), to deal
            in the Software without restriction, including without limitation the rights
            to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
            copies of the Software.
            """;

        var analyzer = CreateAnalyzer(new FakeHandler(HttpStatusCode.OK, mitText));

        var result = await analyzer.TryIdentifyFromUrlAsync("https://example.com/license.txt");

        Assert.Equal("MIT", result);
    }

    [Fact]
    public async Task TryIdentify_ApacheLicenseText_ReturnsApache()
    {
        var apacheText = """
            Apache License
            Version 2.0, January 2004

            TERMS AND CONDITIONS FOR USE, REPRODUCTION, AND DISTRIBUTION
            ...
            """;

        var analyzer = CreateAnalyzer(new FakeHandler(HttpStatusCode.OK, apacheText));

        var result = await analyzer.TryIdentifyFromUrlAsync("https://example.com/LICENSE");

        Assert.Equal("Apache-2.0", result);
    }

    [Fact]
    public async Task TryIdentify_Bsd3LicenseText_ReturnsBsd3()
    {
        var bsd3Text = """
            Redistribution and use in source and binary forms, with or without modification,
            are permitted provided that the following conditions are met:

            1. Redistributions of source code must retain the above copyright notice.
            2. Redistributions in binary form must reproduce the above copyright notice.
            3. Neither the name of the copyright holder nor the names of its contributors
               may be used to endorse or promote products.
            """;

        var analyzer = CreateAnalyzer(new FakeHandler(HttpStatusCode.OK, bsd3Text));

        var result = await analyzer.TryIdentifyFromUrlAsync("https://example.com/LICENSE");

        Assert.Equal("BSD-3-Clause", result);
    }

    [Fact]
    public async Task TryIdentify_Bsd2LicenseText_ReturnsBsd2()
    {
        var bsd2Text = """
            Redistribution and use in source and binary forms, with or without modification,
            are permitted provided that the following conditions are met:

            1. Redistributions of source code must retain the above copyright notice.
            2. Redistributions in binary form must reproduce the above copyright notice.
            """;

        var analyzer = CreateAnalyzer(new FakeHandler(HttpStatusCode.OK, bsd2Text));

        var result = await analyzer.TryIdentifyFromUrlAsync("https://example.com/LICENSE");

        Assert.Equal("BSD-2-Clause", result);
    }

    [Fact]
    public async Task TryIdentify_IscLicenseText_ReturnsIsc()
    {
        var iscText = """
            ISC License

            Permission to use, copy, modify, and/or distribute this software for any
            purpose with or without fee is hereby granted.
            """;

        var analyzer = CreateAnalyzer(new FakeHandler(HttpStatusCode.OK, iscText));

        var result = await analyzer.TryIdentifyFromUrlAsync("https://example.com/LICENSE");

        Assert.Equal("ISC", result);
    }

    [Fact]
    public async Task TryIdentify_UnrecognizedText_ReturnsNull()
    {
        var unknownText = "This is some custom proprietary license with no known patterns.";

        var analyzer = CreateAnalyzer(new FakeHandler(HttpStatusCode.OK, unknownText));

        var result = await analyzer.TryIdentifyFromUrlAsync("https://example.com/LICENSE");

        Assert.Null(result);
    }

    [Fact]
    public async Task TryIdentify_HttpError_ReturnsNull()
    {
        var analyzer = CreateAnalyzer(new FakeHandler(HttpStatusCode.NotFound, "Not Found"));

        var result = await analyzer.TryIdentifyFromUrlAsync("https://example.com/LICENSE");

        Assert.Null(result);
    }

    [Fact]
    public async Task TryIdentify_HttpException_ReturnsNull()
    {
        var analyzer = CreateAnalyzer(new ThrowingHandler());

        var result = await analyzer.TryIdentifyFromUrlAsync("https://example.com/LICENSE");

        Assert.Null(result);
    }

    private class FakeHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _content;

        public FakeHandler(HttpStatusCode statusCode, string content)
        {
            _statusCode = statusCode;
            _content = content;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_content)
            };
            return Task.FromResult(response);
        }
    }

    private class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            throw new HttpRequestException("Connection refused");
        }
    }

    private class TestHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;

        public TestHttpClientFactory(HttpMessageHandler handler)
        {
            _handler = handler;
        }

        public HttpClient CreateClient(string name) => new(_handler, disposeHandler: false);
    }
}
