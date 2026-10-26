using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Recrovit.AspNetCore.Authentication.OpenIdConnect.Configuration;

namespace Recrovit.RecroGridFramework.Client.Blazor.SessionAuth.Tests.Proxy;

internal static class ProxyProtectionTestServices
{
    public static void Add(WebApplicationBuilder builder)
    {
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Recrovit:OpenIdConnect:Provider"] = "Test",
            ["Recrovit:OpenIdConnect:Providers:Test:Authority"] = "https://idp.example.test",
            ["Recrovit:OpenIdConnect:Providers:Test:ClientId"] = "test",
            ["Recrovit:OpenIdConnect:Providers:Test:ClientSecret"] = "test",
            ["Recrovit:OpenIdConnect:DownstreamApis:RgfApi:BaseUrl"] = "https://api.example.test",
            ["Recrovit:OpenIdConnect:DownstreamApis:RgfApi:Scopes:0"] = "api.scope",
            ["Recrovit:OpenIdConnect:Host:SessionValidationDownstreamApiName"] = "RgfApi"
        });
        builder.Services.AddOidcAuthenticationInfrastructure(builder.Configuration, builder.Environment);
    }
}
