using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;

namespace Recrovit.RecroGridFramework.Client.Blazor.Components.Dashboard;

public class RgfDashboardRenderGuard : ErrorBoundary
{
    [Inject]
    private ILogger<RgfDashboardRenderGuard> Logger { get; set; } = null!;

    [Parameter]
    public RenderFragment? FallbackContent { get; set; }

    [Parameter]
    public string? ParentPaneId { get; set; }

    [Parameter]
    public string? PaneId { get; set; }

    [Parameter]
    public int? DashboardItemId { get; set; }

    [Parameter]
    public string LogMessage { get; set; } = "Dashboard render guard caught an exception.";

    protected override void OnParametersSet()
    {
        ErrorContent = _ => FallbackContent ?? EmptyFallback;
        base.OnParametersSet();
    }

    protected override Task OnErrorAsync(Exception exception)
    {
        Logger.LogError(
            exception,
            "{LogMessage} ParentPaneId={ParentPaneId}, PaneId={PaneId}, DashboardItemId={DashboardItemId}.",
            LogMessage,
            ParentPaneId,
            PaneId,
            DashboardItemId);

        return Task.CompletedTask;
    }

    private static RenderFragment EmptyFallback => builder => { };
}
