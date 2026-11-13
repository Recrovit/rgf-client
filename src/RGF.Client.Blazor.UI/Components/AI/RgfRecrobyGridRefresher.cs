using Recrovit.RecroGridFramework.Client.Handlers;

namespace Recrovit.RecroGridFramework.Client.Blazor.UI.Components.AI;

// RGF-DOC: rgf.client.recroby.integration

internal sealed class RgfRecrobyGridRefresher(Action<Exception, string> logWarning)
{
    internal async Task RefreshAsync(IRgListHandler list, bool invalidateAggregates)
    {
        try
        {
            await list.RefreshDataAsync(null, invalidateAggregates);
        }
        catch (Exception error)
        {
            logWarning(error, "The Grid refresh failed.");
        }
    }
}
