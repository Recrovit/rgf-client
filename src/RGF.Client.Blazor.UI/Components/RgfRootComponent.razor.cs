using Microsoft.AspNetCore.Components;

namespace Recrovit.RecroGridFramework.Client.Blazor.UI.Components;

public partial class RgfRootComponent
{
    [Inject]
    private IServiceProvider _serviceProvider { get; set; } = default!;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);

        if (firstRender)
        {
            await RgfBlazorConfigurationExtension.LoadResourcesAsync(_serviceProvider);
            await RGFClientBlazorUIConfiguration.LoadResourcesAsync(_serviceProvider);
        }
    }
}
