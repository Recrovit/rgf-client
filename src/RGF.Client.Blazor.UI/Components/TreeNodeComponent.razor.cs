using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Services;
using Recrovit.RecroGridFramework.Abstraction.Models;
using Recrovit.RecroGridFramework.Client.Blazor.Parameters;
using Recrovit.RecroGridFramework.Client.Events;
using Recrovit.RecroGridFramework.Client.Handlers;
using System.Text;

namespace Recrovit.RecroGridFramework.Client.Blazor.UI.Components;

public partial class TreeNodeComponent : IDisposable
{
    [Inject]
    private ILogger<TreeComponent> _logger { get; set; } = default!;

    private RgfEntityParameters? EntityParameters => Node.EntityParameters;

    private IRgManager Manager => TreeComponent.Manager;

    protected override void OnParametersSet()
    {
        base.OnParametersSet();

        if (Node.Property != null && Node.Property.ListType != PropertyListType.RecroGrid)
        {
            StringBuilder str = new();
            str.Append("<div class='rgf-tree-tooltip'>");
            foreach (var prop in Manager.EntityDesc.SortedVisibleColumns)
            {
                var data = Node.RowData.GetItemData(prop.Alias).StringValue;
                str.Append("<div class='row justify-content-start align-items-center'>")
                .AppendFormat("<div class='rgf-tooltip-title col-3 text-end text-truncate'>{0}</div>", prop.ColTitle)
                .Append("<div class='col-auto'>:</div>")
                .AppendFormat("<div class='rgf-tooltip-data col text-start text-truncate'>{0}</div>", data)
                .Append("</div>");
            }
            str.Append("</div>");
            Node.TooltipText = str.ToString();
        }
        else
        {
            Node.TooltipText = null;
        }
    }

    protected override void OnAfterRender(bool firstRender)
    {
        base.OnAfterRender(firstRender);
        if (firstRender)
        {
            EntityParameters?.EventDispatcher.Subscribe(RgfEntityEventKind.Initialized, OnEntityInitialized);
        }
    }

    private void OnEntityInitialized(IRgfEventArgs<RgfEntityEventArgs> args)
    {
        StateHasChanged();
    }

    private void OnEmbedded(MouseEventArgs arg)
    {
        if (EntityParameters != null)
        {
            EntityParameters.DisplayMode = EntityParameters.DisplayMode == RfgDisplayMode.Tree ? RfgDisplayMode.Grid : RfgDisplayMode.Tree;
        }
    }

    private void OnClickData(MouseEventArgs arg)
    {
        var eventArgs = new RgfEventArgs<RgfToolbarEventArgs>(this, new RgfToolbarEventArgs(RgfToolbarEventKind.Read, Node.RowData));
        _ = EntityParameters?.ToolbarParameters.EventDispatcher.DispatchEventAsync(eventArgs.Args.EventKind, eventArgs);
    }

    public void Dispose()
    {
        EntityParameters?.EventDispatcher.Unsubscribe(RgfEntityEventKind.Initialized, OnEntityInitialized);
    }
}