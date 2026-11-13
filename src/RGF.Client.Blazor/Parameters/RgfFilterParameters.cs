using Recrovit.RecroGridFramework.Client.Events;

namespace Recrovit.RecroGridFramework.Client.Blazor.Parameters;

public class RgfFilterParameters
{
    // RGF-DOC: rgf.client.blazor.filtering
    /// <summary>Dispatches events for this Grid's filter UI.</summary>
    public RgfEventDispatcher<RgfFilterEventKind, RgfFilterEventArgs> EventDispatcher { get; } = new();

    public RgfDialogParameters DialogParameters { get; set; } = new();
}
