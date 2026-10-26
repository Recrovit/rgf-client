using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Recrovit.RecroGridFramework.Client.Handlers;
using Recrovit.RecroGridFramework.Client.Services;

namespace Recrovit.RecroGridFramework.Client.Blazor.SessionAuth.Tests.Proxy;

public sealed class RgfServerProxyOriginTests
{
    [Theory]
    [InlineData("GET", "https://public.example.test/RGF/base/test", null, null, "https://public.example.test", true)]
    [InlineData("POST", "https://public.example.test/rgf/api/test", null, null, "https://public.example.test", true)]
    [InlineData("POST", "https://api.example.test/rgf/api/test", null, null, null, false)]
    [InlineData("GET", "http://public.example.test/rgf/api/test", null, null, null, false)]
    [InlineData("GET", "https://public.example.test:444/rgf/api/test", null, null, null, false)]
    [InlineData("POST", "https://public.example.test/rgf/api/test", "https://foreign.example.test", null, "https://foreign.example.test", true)]
    [InlineData("POST", "https://public.example.test/rgf/api/test", null, "cross-site", null, true)]
    [InlineData("POST", "https://public.example.test/rgf/api/test", "null", "same-origin", "null", true)]
    [InlineData("POST", "https://public.example.test/rgf/api/test", null, "", null, true)]
    public async Task SsrAddsIntentAndCookieOnlyToConfiguredHost(string method, string target, string? origin,
        string? fetch, string? expectedOrigin, bool cookieExpected)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRgfBlazorSessionAuthSsrServices(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Recrovit:RecroGridFramework:API:ProxyBaseAddress"] = "https://public.example.test/application",
            ["Recrovit:RecroGridFramework:API:BaseAddress"] = "https://api.example.test"
        }).Build());
        services.AddScoped<IRgfServerRequestCookieAccessor, CookieAccessor>();
        var capture = new CaptureHandler();
        services.AddHttpClient(ApiService.RgfApiClientName).ConfigurePrimaryHttpMessageHandler(() => capture);
        using var provider = services.BuildServiceProvider();
        using var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient(ApiService.RgfApiClientName);
        using var request = new HttpRequestMessage(new HttpMethod(method), target);
        if (origin is not null) request.Headers.TryAddWithoutValidation("Origin", origin);
        if (fetch is not null) request.Headers.TryAddWithoutValidation("Sec-Fetch-Site", fetch);
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(expectedOrigin, capture.Origin);
        Assert.Equal(fetch, capture.Fetch);
        Assert.Equal(cookieExpected ? "session=test" : null, capture.Cookie);
    }

    private sealed class CookieAccessor : IRgfServerRequestCookieAccessor
    {
        public string GetCookieHeader() => "session=test";
    }

    private sealed class CaptureHandler : HttpMessageHandler
    {
        public string? Origin { get; private set; }
        public string? Fetch { get; private set; }
        public string? Cookie { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Origin = Read("Origin");
            Fetch = Read("Sec-Fetch-Site");
            Cookie = Read("Cookie");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
            string? Read(string name) => request.Headers.TryGetValues(name, out var values) ? string.Join(",", values) : null;
        }
    }
}
