using Recrovit.RecroGridFramework.Client.Events;

namespace Recrovit.RecroGridFramework.Client.Blazor.Parameters;

/// <summary>Presentation of a chart opened from an entity grid.</summary>
public enum RgfChartPresentationMode
{
    /// <summary>Persistent chart content in the standalone entity workspace.</summary>
    Workspace,
    /// <summary>Modal chart over an entity.</summary>
    Dialog
}

public class RgfChartParameters
{
    // RGF-DOC: rgf.client.blazor-ui.entity-ui
    /// <summary>Effective grid chart presentation, assigned by the entity during initialization.
    /// Direct chart consumers default to Dialog; dashboard Embedded presentation remains independent.</summary>
    public RgfChartPresentationMode PresentationMode { get; internal set; } = RgfChartPresentationMode.Dialog;

    /// <summary>Declares that the selected custom Chart implementation supports workspace rendering.
    /// Defaults to false; the standard UI entity enables it automatically for its registered UI chart.
    /// Enable explicitly for compatible custom templates; the standalone ApexCharts provider supports Dialog and Embedded only.
    /// Set before entity initialization.</summary>
    public bool SupportsWorkspace { get; set; }

    public RgfDialogParameters DialogParameters { get; set; } = new();

    public bool SuppressAutomaticChartToast { get; set; }

    public RgfEventDispatcher<RgfChartEventKind, RgfChartEventArgs> EventDispatcher { get; } = new();
}
