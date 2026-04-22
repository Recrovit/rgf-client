using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Recrovit.AspNetCore.Authentication.OpenIdConnect.Configuration;
using Recrovit.AspNetCore.Authentication.OpenIdConnect.Proxy;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Constants;

namespace Recrovit.RecroGridFramework.Client.Blazor.Host.OpenIdConnect.Proxy;

/// <summary>
/// Maps the standard RGF proxy endpoints used by SSR hosts.
/// </summary>
public static class RgfProxyEndpoints
{
    private const string DownstreamApiName = "RgfApi";

    public static IEndpointRouteBuilder MapRgfProxyEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/rgf/api/RGFSriptReferences/{**suffix}", ProxyAnonymousGetAsync)
            .AsProxyEndpoint()
            .WithSummary("Proxies RGF script reference metadata calls.");

        endpoints.MapGet("/rgf/api/RGFStylesheetsReferences", ProxyAnonymousGetAsync)
            .AsProxyEndpoint()
            .WithSummary("Proxies RGF stylesheet reference calls.");

        endpoints.MapGet("/rgf/api/RecroDict/{**path}", ProxyAnonymousGetAsync)
            .AsProxyEndpoint()
            .WithSummary("Proxies RecroDict calls.");

        endpoints.MapGet("/rgf/styles.{environment}/{**path}", ProxyAnonymousGetAsync)
            .AsProxyEndpoint()
            .WithSummary("Proxies RGF style calls.");

        endpoints.MapGet("/rgf/resource/{**path}", ProxyAnonymousGetAsync)
            .AsProxyEndpoint()
            .WithSummary("Proxies static RGF resources.");

        endpoints.MapMethods(RgfSignalR.RgfProgressHubEndpoint, ProxyEndpointConventionBuilderExtensions.ProxyTransportMethods, ProxyProgressHubAsync)
            .AsProxyEndpoint()
            .WithSummary("Proxies RGF progress hub calls.");

        endpoints.MapMethods($"{RgfSignalR.RgfProgressHubEndpoint}/{{**path}}", ProxyEndpointConventionBuilderExtensions.ProxyTransportMethods, ProxyProgressHubAsync)
            .AsProxyEndpoint()
            .WithSummary("Proxies RGF progress hub calls.");

        endpoints.MapMethods("/RGF/base/{**path}", ProxyEndpointConventionBuilderExtensions.StandardProxyMethods, ProxyAuthorizedAsync)
            .AsProxyEndpoint()
            .RequireAuthorization()
            .DisableAuthRedirects()
            .WithSummary("Proxies RGF base calls.");

        endpoints.MapMethods("/RGF/RecroGrid/{**path}", ProxyEndpointConventionBuilderExtensions.StandardProxyMethods, ProxyAuthorizedAsync)
            .AsProxyEndpoint()
            .RequireAuthorization()
            .DisableAuthRedirects()
            .WithSummary("Proxies RGF grid calls.");

        endpoints.MapMethods("/rgf/api/{**path}", ProxyEndpointConventionBuilderExtensions.StandardProxyMethods, ProxyAuthorizedAsync)
            .AsProxyEndpoint()
            .RequireAuthorization()
            .DisableAuthRedirects()
            .WithSummary("Proxies authorized RGF API calls to the downstream API.");

        return endpoints;
    }

    private static Task ProxyAnonymousGetAsync(HttpContext context, IDownstreamHttpProxyClient proxyClient, CancellationToken cancellationToken) =>
        DownstreamProxyEndpointExecutor.ProxyHttpAsync(context, proxyClient, DownstreamApiName, user: null, cancellationToken);

    private static Task ProxyAuthorizedAsync(HttpContext context, IDownstreamHttpProxyClient proxyClient, CancellationToken cancellationToken) =>
        DownstreamProxyEndpointExecutor.ProxyHttpAsync(context, proxyClient, DownstreamApiName, context.User, cancellationToken);

    private static async Task ProxyProgressHubAsync(
        HttpContext context,
        IDownstreamHttpProxyClient httpProxyClient,
        IDownstreamTransportProxyClient transportProxyClient,
        CancellationToken cancellationToken)
    {
        if (context.WebSockets.IsWebSocketRequest)
        {
            await transportProxyClient.ProxyWebSocketAsync(
                context,
                DownstreamApiName,
                $"{context.Request.Path}{context.Request.QueryString}",
                context.User,
                cancellationToken);
            return;
        }

        await DownstreamProxyEndpointExecutor.ProxyHttpAsync(context, httpProxyClient, DownstreamApiName, context.User, cancellationToken);
    }
}
