using Microsoft.Extensions.DependencyInjection;
using Recrovit.RecroGridFramework.Client.Blazor.Services;
using Recrovit.RecroGridFramework.Client.Blazor.SessionAuth.Authentication.Session;

namespace Recrovit.RecroGridFramework.Client.Blazor.SessionAuth.Authentication.Initialization;

internal sealed class RgfSessionAuthInitializationHook : IRgfBlazorInitializationHook
{
    public async Task InitializeAsync(IServiceProvider serviceProvider, bool clientSideRendering, CancellationToken cancellationToken)
    {
        if (!clientSideRendering)
        {
            return;
        }

        var authenticationSessionMonitor = serviceProvider.GetService<IRgfAuthenticationSessionMonitor>();
        if (authenticationSessionMonitor is not null)
        {
            await authenticationSessionMonitor.ProbeAsync(cancellationToken);
        }
    }
}
