using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Recrovit.RecroGridFramework.Client.AI;

namespace Recrovit.RecroGridFramework.Client.Blazor.UI.Components;

public partial class RgfRootComponent
{
    [Inject]
    private IServiceProvider _serviceProvider { get; set; } = default!;

    private bool _recrobyEnabled;

    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();
        if (EnableRecroby)
        {
            var capability = _serviceProvider.GetService<RgfRecrobyCapabilityService>();
            _recrobyEnabled = capability is null || await capability.GetEnabledAsync();
        }
    }

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
