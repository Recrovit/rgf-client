namespace Recrovit.RecroGridFramework.Client.Events;

public enum RgfChartEventKind
{
    ShowChart = 1,
    Rendered = 2,
    Initialized = 3,
    LayoutCommitted = 4
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
}
