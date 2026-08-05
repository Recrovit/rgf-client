using Recrovit.RecroGridFramework.Client.Events;

namespace Recrovit.RecroGridFramework.Client.Blazor.Parameters;

public class RgfChartParameters
{
    public RgfDialogParameters DialogParameters { get; set; } = new();

    public bool SuppressAutomaticChartToast { get; set; }

    public RgfEventDispatcher<RgfChartEventKind, RgfChartEventArgs> EventDispatcher { get; } = new();
}
