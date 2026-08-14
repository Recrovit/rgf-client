using Microsoft.AspNetCore.Components;
using Recrovit.RecroGridFramework.Client.Blazor.Services;
using Recrovit.RecroGridFramework.Client.Blazor.SessionAuth.Authentication.Session;

namespace Recrovit.RecroGridFramework.Client.Blazor.SessionAuth.Authorization.RouteAccess;

public sealed class RgfRouteAuthenticationTracker : ComponentBase
{
    [Inject]
    public IRgfAuthenticationSessionMonitor SessionMonitor { get; set; } = null!;

    [Inject]
    public NavigationManager NavigationManager { get; set; } = null!;

    [Inject]
    public RgfAuthenticationEndpointResolver AuthenticationEndpoints { get; set; } = null!;

    [Parameter, EditorRequired]
    public Type PageType { get; set; } = null!;

    protected override void OnParametersSet()
    {
        var requiresAuthentication = RgfRouteAuthorizationMetadata.RequiresAuthentication(PageType);
        if (!requiresAuthentication && IsAuthenticationEndpointNavigation())
        {
            return;
        }

        SessionMonitor.SetRouteAuthenticationRequired(requiresAuthentication);
    }

    private bool IsAuthenticationEndpointNavigation()
    {
        var currentUri = new Uri(NavigationManager.Uri, UriKind.Absolute);
        return currentUri.AbsolutePath.StartsWith(AuthenticationEndpoints.BasePath, StringComparison.Ordinal);
    }
}
