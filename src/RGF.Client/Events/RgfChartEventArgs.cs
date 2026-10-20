namespace Recrovit.RecroGridFramework.Client.Events;

public enum RgfChartEventKind
{
    ShowChart = 1,
    Rendered = 2,
    Initialized = 3,
    LayoutCommitted = 4,
    /// <summary>A workspace chart has become visible in the rendered DOM; resize without resetting state.</summary>
    WorkspaceActivated = 5,
    /// <summary>Chart data was invalidated; discard the displayed visualization.</summary>
    DataInvalidated = 6,
    /// <summary>The workspace aggregate source changed after its query state was applied.</summary>
    AggregateSourceChanged = 7
}

public class RgfChartEventArgs : EventArgs
{
    public RgfChartEventArgs(RgfChartEventKind eventKind)
    {
        EventKind = eventKind;
    }

    public static RgfChartEventArgs CreateAfterRenderEvent(bool firstRender) => new(RgfChartEventKind.Rendered) { FirstRender = firstRender };

    public RgfChartEventKind EventKind { get; }

    public bool FirstRender { get; internal set; }

    /// <summary>Whether the aggregate source change came from applying a saved Chart filter whose generation is handled by settings loading.</summary>
    public bool IsChartSettingsChange { get; init; }
}
