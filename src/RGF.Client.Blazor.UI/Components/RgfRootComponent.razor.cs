using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Recrovit.RecroGridFramework.Client.AI;

namespace Recrovit.RecroGridFramework.Client.Blazor.UI.Components;

public partial class RgfRootComponent
{
    [Inject]
    private IServiceProvider _serviceProvider { get; set; } = default!;

    private bool _recrobyEnabled;

    protected override void OnInitialized()
    {
        base.OnInitialized();
        RGFClientBlazorUIConfiguration.RegisterResourceLoad(_serviceProvider.GetRequiredService<IJSRuntime>());
    }

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
            await RGFClientBlazorUIConfiguration.LoadResourcesAsync(_serviceProvider, themeName: null, loadBaseResources: true);
        }
    }
}
