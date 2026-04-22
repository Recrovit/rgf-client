namespace Recrovit.RecroGridFramework.Client.Blazor.Services;

public interface IRgfBlazorInitializationHook
{
    Task InitializeAsync(IServiceProvider serviceProvider, bool clientSideRendering, CancellationToken cancellationToken);
}
