using Microsoft.AspNetCore.Components;
using Recrovit.RecroGridFramework.Client.Blazor.SessionAuth.Authentication.Session;

namespace Recrovit.RecroGridFramework.Client.Blazor.SessionAuth.Authorization.RouteAccess;

public sealed class RgfRouteAuthenticationTracker : ComponentBase
{
    [Inject]
    public IRgfAuthenticationSessionMonitor SessionMonitor { get; set; } = null!;

    [Parameter, EditorRequired]
    public Type PageType { get; set; } = null!;

    protected override void OnParametersSet()
    {
        SessionMonitor.SetRouteAuthenticationRequired(RgfRouteAuthorizationMetadata.RequiresAuthentication(PageType));
    }
}
