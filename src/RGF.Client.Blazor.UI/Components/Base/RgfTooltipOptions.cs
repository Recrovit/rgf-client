namespace Recrovit.RecroGridFramework.Client.Blazor.UI.Components.Base;

public class RgfTooltipOptions
{
    public RgfTooltipOptions(string? title, string? customClass = null)
    {
        Title = title;
        CustomClass = customClass ?? "rgf-tooltip-400";
    }

    public string? Title { get; set; }

    public string CustomClass { get; set; }

    public string Placement { get; set; } = "top";

    public string Trigger { get; set; } = "hover";

    public bool AllowHtml { get; set; } = true;

    public int DelayShow { get; set; } = 500;

    public int DelayHide { get; set; } = 100;
}