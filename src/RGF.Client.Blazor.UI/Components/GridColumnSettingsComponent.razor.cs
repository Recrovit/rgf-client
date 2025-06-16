using Microsoft.AspNetCore.Components;
using Recrovit.RecroGridFramework.Abstraction.Models;
using Recrovit.RecroGridFramework.Client.Blazor.Components;

namespace Recrovit.RecroGridFramework.Client.Blazor.UI.Components;

public partial class GridColumnSettingsComponent
{
    private RgfGridColumnSettingsComponent _rgfColSettingsRef { get; set; } = null!;

    private IEnumerable<RgfGridColumnSettings>? Columns { get; set; }

    private void Sort(int col)
    {
        if (col == 1)
        {
            Columns = _rgfColSettingsRef.Columns.OrderBy(e => e.PathTitle ?? e.Property.ColTitle);
        }
        else if (col == 2)
        {
            Columns = _rgfColSettingsRef.Columns.OrderBy(e => e.ColPosOrNull > 0 ? e.ColPosOrNull : int.MaxValue);
        }
        StateHasChanged();
    }

    public RenderFragment RenderProperties(IEnumerable<RgfGridColumnSettings> columnSettings, RgfGridColumnSettings? parent = null) => builder =>
    {
        int seq = 0;
        var external = new List<RgfGridColumnSettings>();
        foreach (var settings in columnSettings)
        {
            if (settings.Property.ListType != PropertyListType.RecroGrid && settings.Property.FormType != PropertyFormType.Entity)
            {
                builder.AddContent(seq++, RenderProperty(settings));
            }
            else
            {
                external.Add(settings);
            }
        }
        foreach (var settings in external)
        {
            builder.AddContent(seq++, RenderEntity(settings, parent));
            if (settings.IsExpanded)
            {
                builder.AddContent(seq++, RenderProperties(settings.RelatedEntityColumnSettings, settings));
            }
        }
    };

    private async Task OnExternal(RgfGridColumnSettings property, RgfGridColumnSettings? parent)
    {
        if (property.BaseEntity == null)
        {
            await _rgfColSettingsRef.LoadPropertiesAsync(property, parent);
        }
        property.IsExpanded = !property.IsExpanded;
        StateHasChanged();
    }
}