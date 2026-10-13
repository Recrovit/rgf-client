namespace Recrovit.RecroGridFramework.Client.Blazor.Services;

/// <summary>Integration initialization invoked after shared client initialization and before browser resource loading.</summary>
public interface IRgfBlazorInitializationHook
{
    /// <summary>Initializes the integration for the selected rendering environment.</summary>
    /// <param name="serviceProvider">The built client provider.</param>
    /// <param name="clientSideRendering">Whether browser resource loading is enabled.</param>
    /// <param name="cancellationToken">Caller cancellation; standard initialization supplies CancellationToken.None.</param>
    /// <returns>A task completing before subsequent hooks run.</returns>
    Task InitializeAsync(IServiceProvider serviceProvider, bool clientSideRendering, CancellationToken cancellationToken);
}
