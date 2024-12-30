namespace Recrovit.RecroGridFramework.Client.Blazor.UI.Components.Base;

public class RgfTooltipOptions
{
    public RgfTooltipOptions(string? title, string? customClass = null)
    {
        Title = title;
        CustomClass = customClass ?? "rgf-tooltip-400";
        Placement = "top";
        Trigger = "hover";
        AllowHtml = true;
    }

    public string? Title { get; set; }

    public string CustomClass { get; set; }

    public string Placement { get; set; }

    public string Trigger { get; set; }

    public bool AllowHtml { get; set; }
}